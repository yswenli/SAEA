/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.Http.Base
*文件名： MultipartStreamParser
*版本号： v26.11.24.1
*唯一标识：3f6b9d24-7c81-4a2e-9f53-0b6c8d1e2a47
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/24 15:59:54
*描述：multipart 请求体单遍流式解析，文件分段超过阈值时转写临时文件
*
*=====================================================================
*修改标记
*修改时间：2026/11/24 16:23:06
*修改人： yswenli
*版本号： v26.11.24.1
*描述：起始 marker 在窗口末尾保留 boundary 长度减一的字节，避免分片读取时丢字节
*
*****************************************************************************/
using System;
using System.IO;
using System.Text;

using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Http.Model;

namespace SAEA.Http.Base
{
    /// <summary>
    /// multipart 请求体单遍流式解析：按固定大小缓冲顺序读取，定界串可以跨读边界，
    /// 文件分段累计字节超过阈值时转写临时文件，解析结束由消息对象统一释放。
    /// </summary>
    internal sealed class MultipartStreamParser
    {
        // 读取缓冲固定 64KB 且来自内存池，解析期间大小不随请求体增长
        const int BufferSize = 64 * 1024;

        /// <summary>
        /// 单个 multipart 请求允许的最大分段数
        /// </summary>
        const int MaxParts = 1024;

        /// <summary>
        /// 单个 multipart 分段允许的报头块最大字节数
        /// </summary>
        const int MaxPartHeaderBytes = 16 * 1024;

        static readonly byte[] _crlf = new byte[] { 13, 10 };

        static readonly byte[] _headerTerminator = new byte[] { 13, 10, 13, 10 };

        readonly Stream _body;

        readonly HttpMessage _message;

        readonly byte[] _startMarker;

        readonly byte[] _delimiter;

        readonly int _largeBodyThreshold;

        readonly string _tempDirectory;

        readonly string _channelId;

        byte[] _buffer;

        int _head;

        int _tail;

        // 缓冲区下标 0 在请求体流中的绝对位置；只有压缩缓冲时才向前累加
        long _bufferOffset;

        bool _endOfStream;

        MultipartStreamParser(Stream body, HttpMessage message)
        {
            _body = body;
            _message = message;

            // Boundary 已带前导 "--"，本身就是起始 marker；分段之间的定界串是「CRLF + Boundary」
            _startMarker = Encoding.ASCII.GetBytes(message.Boundary);

            _delimiter = new byte[_startMarker.Length + 2];

            _delimiter[0] = (byte)'\r';
            _delimiter[1] = (byte)'\n';

            Buffer.BlockCopy(_startMarker, 0, _delimiter, 2, _startMarker.Length);

            _largeBodyThreshold = message.LargeBodyThreshold;
            _tempDirectory = message.TempDirectory;
            _channelId = message.ID;
        }

        /// <summary>
        /// 解析 multipart 请求体，把表单字段与文件分段写入消息对象
        /// </summary>
        /// <param name="body">已定位到请求体开头的可读流</param>
        /// <param name="message">接收解析结果的消息对象，需已带 boundary</param>
        /// <returns>解析到结束 marker 返回 true；缺少 boundary、截断或畸形返回 false</returns>
        internal static bool Parse(Stream body, HttpMessage message)
        {
            if (body == null || message == null) return false;

            if (string.IsNullOrEmpty(message.Boundary)) return false;

            var parser = new MultipartStreamParser(body, message);

            return parser.Run();
        }

        bool Run()
        {
            _buffer = MemoryPoolManager.Rent(BufferSize);

            try
            {
                return ParseCore();
            }
            finally
            {
                // 临时文件创建等失败按异常交给 ParseBody 统一记录，这里只保证缓冲归还
                MemoryPoolManager.Return(_buffer, BufferSize);
            }
        }

        bool ParseCore()
        {
            if (!TryFindStartMarker(out var markerStart)) return false;

            _head = markerStart + _startMarker.Length;

            var anyPart = false;

            var partCount = 0;

            while (true)
            {
                // boundary 之后跟 "--" 表示全部结束，跟 CRLF 表示还有下一个分段
                if (!EnsureAvailable(2)) break;

                if (_buffer[_head] == (byte)'-' && _buffer[_head + 1] == (byte)'-')
                {
                    return true;
                }

                if (_buffer[_head] != (byte)'\r' || _buffer[_head + 1] != (byte)'\n') break;

                _head += 2;

                if (!TryReadPartHeader(out var name, out var fileName, out var type)) break;

                // 结构完整的畸形分段同样计数，避免超多小分段把后续处理拖垮
                partCount++;

                if (partCount > MaxParts)
                {
                    LogHelper.Error("MultipartStreamParser.Parse", new InvalidDataException("multipart 分段数超过 1024，已停止解析并保留已解析结果。"));

                    return anyPart;
                }

                // 文本字段始终留在内存；只有带 filename 的文件分段才按阈值落盘，
                // 否则大文本字段会被无故写文件再读回
                using (var sink = new PartSink(fileName != null ? _largeBodyThreshold : 0, _tempDirectory, _channelId))
                {
                    if (!ReadPartData(sink)) break;

                    if (!string.IsNullOrEmpty(name))
                    {
                        if (fileName == null)
                        {
                            // 文本字段按 UTF-8 原样解码，字段值两侧空白属于内容，不做裁剪
                            _message.Forms[name] = sink.GetText();
                        }
                        else
                        {
                            _message.PostFiles.Add(new FilePart(name, fileName, type, sink.Detach()));
                        }

                        anyPart = true;
                    }
                }
            }

            // 未读到结束 marker：当前未完成的分段随 using 丢弃，之前已完整的分段照常保留
            LogHelper.Error("MultipartStreamParser.Parse", new InvalidDataException("multipart 请求体不完整，已丢弃未完成的分段并保留已解析结果。"));

            return false;
        }

        /// <summary>
        /// 查找合法的起始 marker：位于请求体开头或紧跟 CRLF，且其后是 CRLF 或 "--"。
        /// 前导内容按 RFC 忽略。
        /// </summary>
        bool TryFindStartMarker(out int markerStart)
        {
            markerStart = -1;

            while (true)
            {
                var window = new ReadOnlySpan<byte>(_buffer, _head, _tail - _head);

                var relative = window.IndexOf(_startMarker);

                if (relative < 0)
                {
                    // 窗口里没有完整 marker：候选必须紧跟 CRLF。有 CRLF 时只保留最后一个 CRLF 及其后的字节；
                    // 一个 CRLF 都没有时不能整窗丢掉，否则跨读边界被拆开的 marker 会被一起丢弃，
                    // 这里保留窗口末尾 marker 长度减一的字节，等下一块数据补齐。
                    var lastCrlf = LastIndexOfCrlf(window);

                    _head = lastCrlf >= 0
                        ? _head + lastCrlf
                        : Math.Max(_head, _tail - (_startMarker.Length - 1));

                    if (!Fill()) return false;

                    continue;
                }

                var candidate = _head + relative;

                var atLineStart = _bufferOffset + candidate == 0
                    || (candidate >= 2 && _buffer[candidate - 2] == (byte)'\r' && _buffer[candidate - 1] == (byte)'\n');

                if (!atLineStart)
                {
                    _head = candidate + 1;

                    continue;
                }

                var after = candidate + _startMarker.Length;

                if (after + 1 >= _tail)
                {
                    // marker 之后还差两个字节才能判定，保留候选与前置 CRLF 等下一块数据
                    if (candidate >= 2) _head = candidate - 2;

                    if (!Fill()) return false;

                    continue;
                }

                if ((_buffer[after] == (byte)'\r' && _buffer[after + 1] == (byte)'\n')
                    || (_buffer[after] == (byte)'-' && _buffer[after + 1] == (byte)'-'))
                {
                    markerStart = candidate;

                    return true;
                }

                // 近似串不是合法 marker，跳过 1 字节继续找
                _head = candidate + 1;
            }
        }

        /// <summary>
        /// 读取当前分段的报头块并解析出字段名、文件名与 MIME 类型；报头可以跨读边界。
        /// </summary>
        bool TryReadPartHeader(out string name, out string fileName, out string type)
        {
            name = null;
            fileName = null;
            type = string.Empty;

            if (!EnsureAvailable(2)) return false;

            // 空行表示该分段没有报头，数据紧随其后
            if (_buffer[_head] == (byte)'\r' && _buffer[_head + 1] == (byte)'\n')
            {
                _head += 2;

                return true;
            }

            while (true)
            {
                var window = new ReadOnlySpan<byte>(_buffer, _head, _tail - _head);

                var terminator = window.IndexOf(_headerTerminator);

                if (terminator >= 0)
                {
                    // 报头块过大只可能是畸形或恶意输入，停止解析并保留已解析结果
                    if (terminator > MaxPartHeaderBytes)
                    {
                        LogHelper.Error("MultipartStreamParser.TryReadPartHeader", new InvalidDataException("multipart 分段报头超过 16KB，已停止解析并保留已解析结果。"));

                        return false;
                    }

                    RequestDataReader.TryParsePartHeaders(_buffer, _head, terminator, out name, out fileName, out type);

                    _head += terminator + _headerTerminator.Length;

                    return true;
                }

                if (_tail - _head > MaxPartHeaderBytes)
                {
                    LogHelper.Error("MultipartStreamParser.TryReadPartHeader", new InvalidDataException("multipart 分段报头超过 16KB，已停止解析并保留已解析结果。"));

                    return false;
                }

                if (!Fill()) return false;
            }
        }

        /// <summary>
        /// 把当前分段的数据流式写入 sink，直到遇到下一个定界串。
        /// </summary>
        bool ReadPartData(PartSink sink)
        {
            // 定界串之后还要看两个字节（CRLF 或 "--"）才能确认它是真的分隔
            var lookahead = _delimiter.Length + 2;

            while (true)
            {
                var window = new ReadOnlySpan<byte>(_buffer, _head, _tail - _head);

                var relative = window.IndexOf(_delimiter);

                if (relative < 0)
                {
                    // 没有完整定界串：除末尾可能拼出定界串的少量字节外，其余都是分段数据
                    var flush = _tail - _head - lookahead;

                    if (flush > 0)
                    {
                        sink.Write(_buffer, _head, flush);

                        _head += flush;
                    }

                    if (!Fill()) return false;

                    continue;
                }

                var candidate = _head + relative;

                var after = candidate + _delimiter.Length;

                if (after + 1 >= _tail)
                {
                    // 定界串后还差两个字节，先落盘候选之前的数据，保留候选等下一块
                    var flush = candidate - _head;

                    if (flush > 0)
                    {
                        sink.Write(_buffer, _head, flush);

                        _head = candidate;
                    }

                    if (!Fill()) return false;

                    continue;
                }

                if ((_buffer[after] == (byte)'\r' && _buffer[after + 1] == (byte)'\n')
                    || (_buffer[after] == (byte)'-' && _buffer[after + 1] == (byte)'-'))
                {
                    var length = candidate - _head;

                    if (length > 0) sink.Write(_buffer, _head, length);

                    _head = after;

                    return true;
                }

                // 近似定界串：只消费 1 字节继续找，保证每个字节只被扫描常数次
                var skip = candidate - _head + 1;

                sink.Write(_buffer, _head, skip);

                _head += skip;
            }
        }

        /// <summary>
        /// 把未处理字节压到缓冲头部并继续读流；流结束或缓冲已满返回 false
        /// </summary>
        bool Fill()
        {
            if (_endOfStream) return false;

            if (_head > 0)
            {
                var remain = _tail - _head;

                if (remain > 0) Buffer.BlockCopy(_buffer, _head, _buffer, 0, remain);

                _bufferOffset += _head;
                _head = 0;
                _tail = remain;
            }

            if (_tail >= _buffer.Length) return false;

            var read = _body.Read(_buffer, _tail, _buffer.Length - _tail);

            if (read <= 0)
            {
                _endOfStream = true;

                return false;
            }

            _tail += read;

            return true;
        }

        /// <summary>
        /// 保证缓冲内至少有指定字节可读；不足时继续从流里读
        /// </summary>
        bool EnsureAvailable(int count)
        {
            while (_tail - _head < count)
            {
                if (!Fill()) return false;
            }

            return true;
        }

        static int LastIndexOfCrlf(ReadOnlySpan<byte> window)
        {
            for (var i = window.Length - 2; i >= 0; i--)
            {
                if (window[i] == (byte)'\r' && window[i + 1] == (byte)'\n') return i;
            }

            return -1;
        }

        /// <summary>
        /// 分段数据接收器：超过阈值时把已写内容与后续内容转写临时文件，释放时删除。
        /// </summary>
        sealed class PartSink : IDisposable
        {
            readonly int _threshold;

            readonly string _tempDirectory;

            readonly string _channelId;

            MemoryStream _memory = new MemoryStream();

            FileStream _file;

            string _filePath;

            long _length;

            internal PartSink(int threshold, string tempDirectory, string channelId)
            {
                _threshold = threshold;
                _tempDirectory = tempDirectory;
                _channelId = channelId;
            }

            internal void Write(byte[] buffer, int offset, int count)
            {
                if (count <= 0) return;

                // 严格超过阈值才落盘，等于阈值仍留在内存
                if (_file == null && _threshold > 0 && _length + count > _threshold) Spill();

                if (_file != null)
                    _file.Write(buffer, offset, count);
                else
                    _memory.Write(buffer, offset, count);

                _length += count;
            }

            internal string GetText()
            {
                var memory = _memory;

                if (memory == null) return string.Empty;

                return Encoding.UTF8.GetString(memory.GetBuffer(), 0, (int)memory.Length);
            }

            /// <summary>
            /// 交出内容流的所有权：文件载体返回请求体流，内存载体返回已归零的 MemoryStream。
            /// 交出后本对象不再负责释放。
            /// </summary>
            internal Stream Detach()
            {
                if (_file != null)
                {
                    var file = _file;
                    var path = _filePath;

                    _file = null;
                    _filePath = null;

                    file.Position = 0;

                    return HttpRequestBodyStream.FromFile(file, path);
                }

                var memory = _memory;

                _memory = null;

                memory.Position = 0;

                return memory;
            }

            void Spill()
            {
                string filePath = null;
                FileStream file = null;

                try
                {
                    file = HttpBodyStorage.CreateTempFile(_tempDirectory, _channelId, out filePath);

                    if (_memory.Length > 0)
                    {
                        _memory.Position = 0;

                        _memory.CopyTo(file);
                    }

                    _memory.Dispose();
                    _memory = null;

                    _file = file;
                    _filePath = filePath;
                }
                catch (Exception ex)
                {
                    // 运行期建文件失败按 IOException 向上抛，由 ParseBody 记日志并转成解析失败
                    if (file != null)
                    {
                        try
                        {
                            file.Dispose();
                        }
                        catch
                        {
                            // 原始异常更重要，关闭失败的次生异常直接忽略
                        }
                    }

                    HttpBodyStorage.TryDelete(filePath);

                    throw new IOException("multipart 文件分段写入临时文件失败。", ex);
                }
            }

            public void Dispose()
            {
                var memory = _memory;

                _memory = null;

                if (memory != null) memory.Dispose();

                var file = _file;
                var path = _filePath;

                _file = null;
                _filePath = null;

                if (file == null) return;

                try
                {
                    // 复用请求体流的释放语义：先关流再删临时文件
                    HttpRequestBodyStream.FromFile(file, path).Dispose();
                }
                catch (Exception ex)
                {
                    LogHelper.Error("MultipartStreamParser.PartSink.Dispose", ex);
                }
            }
        }
    }
}