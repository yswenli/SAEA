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
*命名空间：SAEA.Http
*文件名： HttpRequestSplitter
*版本号： v26.11.5.1
*唯一标识：0f8e106f-e55d-44f7-9da8-8d7e2fa2c9ae
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/9 13:12:01
*描述：HTTP 请求分帧器：先解析报头再按 Content-Length 等齐请求体
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 23:51:01
*修改人： yswenli
*版本号： v26.11.16.1
*描述：定帧改为按需扫描 Transfer-Encoding、Content-Length、Upgrade 三个报头字段，不再为每个请求建消息对象与报头字典；超限请求做完整解析后归还消息占用的池化报头缓冲
*
*****************************************************************************/
using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using SAEA.Common.Caching;
using SAEA.Http.Base;
using SAEA.Sockets.Pipeline;

namespace SAEA.Http
{
    /// <summary>
    /// HTTP 请求分帧器：先从缓冲里找报头结束符 <c>\r\n\r\n</c> 并只扫出定帧需要的报头字段，再按 <c>Content-Length</c> 等齐请求体，
    /// 只有整帧到齐才产出。报头与请求体各有独立上限，<c>Content-Length</c> 超限在报头扫描后立即判定，不等请求体。
    /// <para>
    /// 只切 HTTP 请求，不生成任何响应，也不做 chunked 解码：协议错误统一用
    /// <see cref="HttpProtocolErrorCodes"/> 的固定 token 抛 <see cref="FrameProtocolException"/>，
    /// 由链上的处理器转成 413 / 431 / 400 响应。
    /// </para>
    /// <para>
    /// 带 <c>Upgrade: websocket</c> 的请求产出后会把 <see cref="SwitchableSplitter.StopAfterCurrent"/> 置为 true，
    /// 让接收泵停止继续切帧，直到协议切换或业务复位屏障。实例保存连接级配置，每连接新建。
    /// </para>
    /// </summary>
    public sealed class HttpRequestSplitter : IFrameSplitter
    {
        private static readonly byte[] _headerEndMarker = new byte[] { 13, 10, 13, 10 };

        // 定帧只关心的三个头名与两个值子串，一律按小写 ASCII 逐字节比较，避免为目标名分配字符串。
        private static readonly byte[] _transferEncodingName = Encoding.ASCII.GetBytes("transfer-encoding");
        private static readonly byte[] _contentLengthName = Encoding.ASCII.GetBytes("content-length");
        private static readonly byte[] _upgradeName = Encoding.ASCII.GetBytes("upgrade");
        private static readonly byte[] _chunkedToken = Encoding.ASCII.GetBytes("chunked");
        private static readonly byte[] _webSocketToken = Encoding.ASCII.GetBytes("websocket");

        // KMP 回退表与目标子串一起静态构造，热路径上只做整数比较。
        private static readonly int[] _chunkedFallback = BuildTokenFallback(_chunkedToken);
        private static readonly int[] _webSocketFallback = BuildTokenFallback(_webSocketToken);

        private readonly int _maxRequestSize;
        private readonly int _maxHeaderSize;
        private readonly int _maxFrameLength;

        /// <summary>
        /// 创建 HTTP 请求分帧器。
        /// </summary>
        /// <param name="maxRequestSize">请求体上限，不含请求行与报头，必须大于 0</param>
        /// <param name="maxHeaderSize">请求行与报头上限，必须大于 0</param>
        /// <param name="barrier">协议切换屏障，可为 null；判定为升级请求时置其 StopAfterCurrent</param>
        public HttpRequestSplitter(
            int maxRequestSize = 64 * 1024 * 1024,
            int maxHeaderSize = 64 * 1024,
            SwitchableSplitter barrier = null)
        {
            if (maxRequestSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxRequestSize), "maxRequestSize 必须大于 0。");
            if (maxHeaderSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHeaderSize), "maxHeaderSize 必须大于 0。");
            if (maxRequestSize > int.MaxValue - maxHeaderSize)
                throw new ArgumentOutOfRangeException(nameof(maxRequestSize), "maxRequestSize 与 maxHeaderSize 之和超出 int 上限。");

            _maxRequestSize = maxRequestSize;
            _maxHeaderSize = maxHeaderSize;
            _maxFrameLength = maxRequestSize + maxHeaderSize;
            Barrier = barrier;
        }

        /// <summary>可接受的最大整帧长度，等于请求体上限加报头上限。</summary>
        public int MaxFrameLength
        {
            get { return _maxFrameLength; }
        }

        /// <summary>
        /// 协议切换屏障。构造后再用 <see cref="SwitchableSplitter"/> 包住本实例的装配方式需要回填该引用，
        /// 用来打破「分帧器要屏障、屏障要分帧器」的构造循环。
        /// </summary>
        public SwitchableSplitter Barrier { get; set; }

        /// <inheritdoc />
        public bool TrySplit(ReadOnlySequence<byte> buffer, out long consumed, out ReadOnlySequence<byte> frame)
        {
            consumed = 0;
            frame = default(ReadOnlySequence<byte>);

            if (buffer.IsEmpty) return false;

            var headerEnd = FindHeaderEnd(buffer);
            if (headerEnd < 0)
            {
                // 报头一直不结束也必须挡住，否则「只发头不发体」可以无限占住缓冲。
                if (buffer.Length > _maxHeaderSize)
                    throw new FrameProtocolException(HttpProtocolErrorCodes.RequestHeaderFieldsTooLarge);
                return false;
            }

            var headerLength = (int)(headerEnd + _headerEndMarker.Length);
            if (headerLength > _maxHeaderSize)
                throw new FrameProtocolException(HttpProtocolErrorCodes.RequestHeaderFieldsTooLarge);

            // 定帧只认 Transfer-Encoding、Content-Length、Upgrade 三个字段，直接扫字节即可；
            // 完整的 HttpMessage 与报头字典会在解码阶段再建一次，这里重复解析等于每请求白分配约 1.1 KB。
            var header = ScanHeaderFields(buffer, headerEnd, headerLength);

            // 报头结束符已到齐，请求行畸形不能当「数据不足」继续等，否则这类连接既收不到响应也不会断开。
            if (header.Malformed)
                throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);

            if (header.Chunked)
            {
                // 不做 chunked 解码：继续按 Content-Length 切会把 chunk 字节误当后续请求。
                throw new FrameProtocolException(HttpProtocolErrorCodes.ChunkedNotSupported);
            }

            if (header.ContentLengthInvalid)
                throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);

            // 必须在这里判超限：等请求体到齐再判，超限字节早已进入内存。
            if (header.ContentLength > _maxRequestSize)
            {
                // 413 比 400 更具体，而旧实现是先完整解析报头、解析失败即回 400，超限判定在其后。
                // 超限请求不是热路径，这里仍走一次完整解析，保证「畸形报文优先按 400 拒绝」的顺序不变。
                if (ParseHeader(buffer, headerLength) == null)
                    throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);

                throw new FrameProtocolException(HttpProtocolErrorCodes.RequestEntityTooLarge);
            }

            var contentLength = header.ContentLength;
            var frameLength = (long)headerLength + contentLength;
            if (buffer.Length < frameLength) return false;

            frame = buffer.Slice(0, frameLength);
            consumed = frameLength;

            if (header.UpgradeWebSocket)
            {
                // 分帧器无法预知业务是否接受升级，一律停切，避免紧跟其后的 WS 首帧被当成 HTTP 请求。
                var barrier = Barrier;
                if (barrier != null)
                    barrier.StopAfterCurrent = true;
            }

            return true;
        }

        /// <inheritdoc />
        public void Reset()
        {
            // 本实现不在实例上保存半帧状态（半帧留在接收泵的 Pipe 里），空实现且可重复调用。
        }

        /// <summary>在可能跨段的缓冲里查找 <c>\r\n\r\n</c> 的起始偏移；未找到返回 -1。</summary>
        /// <param name="buffer">自上次消费位置起的未消费缓冲</param>
        /// <returns>报头结束符首字节的绝对偏移</returns>
        private static long FindHeaderEnd(ReadOnlySequence<byte> buffer)
        {
            var matched = 0;
            var offset = 0L;

            foreach (var segment in buffer)
            {
                var span = segment.Span;
                for (var i = 0; i < span.Length; i++)
                {
                    var value = span[i];
                    if (value == _headerEndMarker[matched])
                    {
                        matched++;
                        if (matched == _headerEndMarker.Length)
                            return offset + i - (_headerEndMarker.Length - 1);
                    }
                    else
                    {
                        // 失配字节本身可能仍是新匹配的开头，例如 \r\r\n\r\n。
                        matched = value == _headerEndMarker[0] ? 1 : 0;
                    }
                }

                offset += span.Length;
            }

            return -1;
        }

        /// <summary>报头字段扫描结果：定帧只用到这几项，扁平结构避免为每个请求建消息对象与字典。</summary>
        private struct HeaderFields
        {
            /// <summary>请求行不是「方法 SP 目标 SP 协议」三段式</summary>
            public bool Malformed;

            /// <summary>Transfer-Encoding 的值里含 chunked</summary>
            public bool Chunked;

            /// <summary>Content-Length 不是非负整数</summary>
            public bool ContentLengthInvalid;

            /// <summary>Content-Length 的数值，字段缺失时为 0</summary>
            public int ContentLength;

            /// <summary>Upgrade 的值里含 websocket</summary>
            public bool UpgradeWebSocket;
        }

        /// <summary>
        /// 逐字节扫描报头行，结果写进 <see cref="HeaderFields"/>。
        /// <para>
        /// 头名按 ASCII 折大小写与三个目标名比较，值就地判定 chunked / websocket 子串与 Content-Length 数值；
        /// 状态全是值类型字段，所以跨段的头名或值可以续扫，不必把报头复制进池化缓冲。
        /// </para>
        /// </summary>
        private struct HeaderLineScanner
        {
            /// <summary>扫描结果，逐行累加</summary>
            public HeaderFields Fields;

            private bool _lineStarted;
            private bool _colonSeen;
            private int _colonCount;
            private int _transferNameProgress;
            private int _contentLengthNameProgress;
            private int _upgradeNameProgress;
            private bool _chunkedSeen;
            private bool _webSocketSeen;
            private int _chunkedProgress;
            private int _webSocketProgress;
            private int _contentLengthState;
            private int _contentLengthDigits;
            private bool _contentLengthNegative;
            private bool _contentLengthOverflow;
            private long _contentLengthValue;

            /// <summary>消费行内一个字节，不含行尾的 CRLF。</summary>
            /// <param name="value">行内字节</param>
            public void Consume(byte value)
            {
                _lineStarted = true;

                if (!_colonSeen)
                {
                    if (value == 58)
                    {
                        _colonSeen = true;
                        _colonCount = 1;
                        return;
                    }

                    // 头名必须与目标名完全等长且逐字节相等，解码阶段取键时也不去两侧空白
                    var foldedName = ToLowerAscii(value);
                    _transferNameProgress = AdvanceName(_transferEncodingName, _transferNameProgress, foldedName);
                    _contentLengthNameProgress = AdvanceName(_contentLengthName, _contentLengthNameProgress, foldedName);
                    _upgradeNameProgress = AdvanceName(_upgradeName, _upgradeNameProgress, foldedName);
                    return;
                }

                if (value == 58)
                {
                    // 与解码阶段一致：一行出现第二个冒号时整行取值按空串处理
                    _colonCount++;
                    return;
                }

                if (_colonCount > 1) return;

                ConsumeValue(value);
            }

            /// <summary>结束当前行：头名命中的字段写入结果，然后归位行内状态。</summary>
            public void EndLine()
            {
                if (_lineStarted)
                {
                    // 没有冒号的行在解码阶段是「整行当键、值为空」，这里同样按命中处理
                    if (_transferNameProgress == _transferEncodingName.Length)
                        Fields.Chunked = _chunkedSeen;

                    if (_upgradeNameProgress == _upgradeName.Length)
                        Fields.UpgradeWebSocket = _webSocketSeen;

                    if (_contentLengthNameProgress == _contentLengthName.Length)
                    {
                        // 同名头重复出现时取最后一个，与报头字典「后写覆盖」一致；
                        // "-0" 解析成功且不小于 0，仍视为 0，所以先合成带符号的值再判负。
                        var value = _contentLengthNegative ? -_contentLengthValue : _contentLengthValue;

                        var valid = _colonCount == 1
                            && _contentLengthDigits > 0
                            && _contentLengthState != 5
                            && !_contentLengthOverflow
                            && value >= 0;

                        Fields.ContentLengthInvalid = !valid;
                        Fields.ContentLength = valid ? (int)value : 0;
                    }
                }

                _lineStarted = false;
                _colonSeen = false;
                _colonCount = 0;
                _transferNameProgress = 0;
                _contentLengthNameProgress = 0;
                _upgradeNameProgress = 0;
                _chunkedSeen = false;
                _webSocketSeen = false;
                _chunkedProgress = 0;
                _webSocketProgress = 0;
                _contentLengthState = 0;
                _contentLengthDigits = 0;
                _contentLengthNegative = false;
                _contentLengthOverflow = false;
                _contentLengthValue = 0;
            }

            /// <summary>消费值里的一个字节：推进 Content-Length 数值解析与两个子串匹配。</summary>
            /// <param name="value">值内字节</param>
            private void ConsumeValue(byte value)
            {
                ConsumeContentLength(value);

                var folded = ToLowerAscii(value);

                if (!_chunkedSeen)
                {
                    _chunkedProgress = AdvanceToken(_chunkedToken, _chunkedFallback, _chunkedProgress, folded);
                    if (_chunkedProgress == _chunkedToken.Length) _chunkedSeen = true;
                }

                if (!_webSocketSeen)
                {
                    _webSocketProgress = AdvanceToken(_webSocketToken, _webSocketFallback, _webSocketProgress, folded);
                    if (_webSocketProgress == _webSocketToken.Length) _webSocketSeen = true;
                }
            }

            /// <summary>按 int.TryParse(NumberStyles.Integer) 的规则推进 Content-Length：空白、符号、数字、空白。</summary>
            /// <param name="value">值内字节</param>
            private void ConsumeContentLength(byte value)
            {
                switch (_contentLengthState)
                {
                    case 0:
                        if (IsAsciiWhiteSpace(value)) return;

                        if (value == 43 || value == 45)
                        {
                            _contentLengthNegative = value == 45;
                            _contentLengthState = 2;
                            return;
                        }

                        if (value >= 48 && value <= 57)
                        {
                            _contentLengthState = 3;
                            AppendDigits(value);
                            return;
                        }

                        _contentLengthState = 5;
                        return;

                    case 2:
                        if (value >= 48 && value <= 57)
                        {
                            _contentLengthState = 3;
                            AppendDigits(value);
                            return;
                        }

                        _contentLengthState = 5;
                        return;

                    case 3:
                        if (value >= 48 && value <= 57)
                        {
                            AppendDigits(value);
                            return;
                        }

                        if (IsAsciiWhiteSpace(value))
                        {
                            _contentLengthState = 4;
                            return;
                        }

                        _contentLengthState = 5;
                        return;

                    case 4:
                        if (IsAsciiWhiteSpace(value)) return;

                        _contentLengthState = 5;
                        return;

                    default:
                        // 已判定非法，后续字节不再影响结论
                        return;
                }
            }

            /// <summary>累加一位十进制数字；超出 int 上限后按解析失败处理。</summary>
            /// <param name="value">数字字节</param>
            private void AppendDigits(byte value)
            {
                _contentLengthDigits++;

                if (_contentLengthOverflow) return;

                _contentLengthValue = _contentLengthValue * 10 + (value - 48);

                if (_contentLengthValue > int.MaxValue)
                {
                    _contentLengthOverflow = true;
                    _contentLengthValue = 0;
                }
            }
        }

        /// <summary>
        /// 只扫出定帧需要的报头字段：Transfer-Encoding、Content-Length、Upgrade。
        /// 报头只扫 <paramref name="headerLength"/> 以内，不建 HttpMessage、字典与字符串。
        /// </summary>
        /// <param name="buffer">自上次消费位置起的未消费缓冲</param>
        /// <param name="headerEnd">报头结束符起始偏移</param>
        /// <param name="headerLength">报头长度（含结束符）</param>
        /// <returns>扫描结果</returns>
        private static HeaderFields ScanHeaderFields(ReadOnlySequence<byte> buffer, long headerEnd, int headerLength)
        {
            if (headerEnd < 4)
            {
                // 与解码阶段一致：\r\n\r\n 之前不足 4 字节时组不出请求行
                return new HeaderFields { Malformed = true };
            }

            // 请求行前允许出现空行，跳过规则与解码阶段保持一致
            var requestLineStart = 0L;

            while (requestLineStart + 1 < headerLength
                && ByteAt(buffer, requestLineStart) == 13
                && ByteAt(buffer, requestLineStart + 1) == 10)
            {
                requestLineStart += 2;
            }

            var requestLineEnd = IndexOfCrLf(buffer, requestLineStart, headerLength);

            if (requestLineEnd < 0) requestLineEnd = headerLength;

            // 解码阶段按空格切「方法 SP 目标 SP 协议」三段，段数不足会直接抛异常，这里提前按 400 拒绝
            var firstSpace = IndexOfByte(buffer, (byte)' ', requestLineStart, requestLineEnd);

            if (firstSpace < 0 || IndexOfByte(buffer, (byte)' ', firstSpace + 1, requestLineEnd) < 0)
                return new HeaderFields { Malformed = true };

            var scanner = new HeaderLineScanner();
            var lineStart = requestLineEnd + 2;
            var pendingCr = false;

            if (lineStart < headerEnd)
            {
                foreach (var segment in buffer.Slice(lineStart, headerEnd - lineStart))
                {
                    var span = segment.Span;
                    for (var i = 0; i < span.Length; i++)
                    {
                        var value = span[i];

                        if (pendingCr)
                        {
                            pendingCr = false;

                            if (value == 10)
                            {
                                scanner.EndLine();
                                continue;
                            }

                            // 孤立的 CR 不是行尾，仍属于行内容
                            scanner.Consume(13);
                        }

                        if (value == 13)
                        {
                            pendingCr = true;
                            continue;
                        }

                        scanner.Consume(value);
                    }
                }
            }

            if (pendingCr) scanner.Consume(13);

            // 结束符之前的最后一段内容也要收尾；空行由 EndLine 内部忽略
            scanner.EndLine();

            return scanner.Fields;
        }

        /// <summary>取指定绝对偏移处的单字节，只换视图不复制。</summary>
        /// <param name="buffer">自上次消费位置起的未消费缓冲</param>
        /// <param name="position">绝对偏移</param>
        /// <returns>该偏移处的字节</returns>
        private static byte ByteAt(ReadOnlySequence<byte> buffer, long position)
        {
            return buffer.Slice(position).First.Span[0];
        }

        /// <summary>在 [start, end) 内查找单字节，未找到返回 -1。</summary>
        /// <param name="buffer">自上次消费位置起的未消费缓冲</param>
        /// <param name="value">目标字节</param>
        /// <param name="start">起始偏移</param>
        /// <param name="end">结束偏移（不含）</param>
        /// <returns>命中的绝对偏移，未命中返回 -1</returns>
        private static long IndexOfByte(ReadOnlySequence<byte> buffer, byte value, long start, long end)
        {
            var offset = start;

            foreach (var segment in buffer.Slice(start, end - start))
            {
                var span = segment.Span;

                for (var i = 0; i < span.Length; i++)
                {
                    if (span[i] == value) return offset + i;
                }

                offset += span.Length;
            }

            return -1;
        }

        /// <summary>在 [start, end) 内查找 <c>\r\n</c>，跨段也能命中，未找到返回 -1。</summary>
        /// <param name="buffer">自上次消费位置起的未消费缓冲</param>
        /// <param name="start">起始偏移</param>
        /// <param name="end">结束偏移（不含）</param>
        /// <returns>CR 的绝对偏移，未命中返回 -1</returns>
        private static long IndexOfCrLf(ReadOnlySequence<byte> buffer, long start, long end)
        {
            var offset = start;
            var previousWasCr = false;

            foreach (var segment in buffer.Slice(start, end - start))
            {
                var span = segment.Span;

                for (var i = 0; i < span.Length; i++)
                {
                    var value = span[i];

                    if (previousWasCr && value == 10) return offset + i - 1;

                    previousWasCr = value == 13;
                }

                offset += span.Length;
            }

            return -1;
        }

        /// <summary>把 ASCII 大写字母折成小写，其余字节原样返回。</summary>
        /// <param name="value">待折叠的字节</param>
        /// <returns>折叠后的字节</returns>
        private static byte ToLowerAscii(byte value)
        {
            return value >= 65 && value <= 90 ? (byte)(value + 32) : value;
        }

        /// <summary>判断字节是否为 int.TryParse 认作空白的 ASCII 空白。</summary>
        /// <param name="value">待判断的字节</param>
        /// <returns>是空白返回 true</returns>
        private static bool IsAsciiWhiteSpace(byte value)
        {
            return value == 32 || (value >= 9 && value <= 13);
        }

        /// <summary>头名逐字节比较：只有长度完全相等且逐字节命中才算命中目标名。</summary>
        /// <param name="lowerName">小写的目标头名</param>
        /// <param name="progress">当前已匹配的字节数，-1 表示已失配</param>
        /// <param name="folded">折叠成小写后的输入字节</param>
        /// <returns>新的已匹配字节数，失配返回 -1</returns>
        private static int AdvanceName(byte[] lowerName, int progress, byte folded)
        {
            if (progress < 0 || progress >= lowerName.Length) return -1;

            return folded == lowerName[progress] ? progress + 1 : -1;
        }

        /// <summary>KMP 推进一步，用于判断值里是否包含指定子串。</summary>
        /// <param name="lowerToken">小写的目标子串</param>
        /// <param name="fallback">KMP 回退表</param>
        /// <param name="progress">已匹配长度</param>
        /// <param name="folded">折叠成小写后的输入字节</param>
        /// <returns>推进后的已匹配长度</returns>
        private static int AdvanceToken(byte[] lowerToken, int[] fallback, int progress, byte folded)
        {
            if (progress >= lowerToken.Length) return progress;

            while (progress > 0 && lowerToken[progress] != folded)
                progress = fallback[progress - 1];

            if (lowerToken[progress] == folded) progress++;

            return progress;
        }

        /// <summary>构造 KMP 回退表：每段的最长真前缀后缀长度。</summary>
        /// <param name="lowerToken">小写的目标子串</param>
        /// <returns>与子串等长的回退表</returns>
        private static int[] BuildTokenFallback(byte[] lowerToken)
        {
            var fallback = new int[lowerToken.Length];
            var matched = 0;

            for (var i = 1; i < lowerToken.Length; i++)
            {
                while (matched > 0 && lowerToken[i] != lowerToken[matched])
                    matched = fallback[matched - 1];

                if (lowerToken[i] == lowerToken[matched]) matched++;

                fallback[i] = matched;
            }

            return fallback;
        }

        /// <summary>解析报头。报头落在首段时直接借用该段内存，跨段时只复制报头，不复制整帧。</summary>
        /// <param name="buffer">自上次消费位置起的未消费缓冲</param>
        /// <param name="headerLength">报头长度（含结束符）</param>
        /// <returns>解析成功返回消息对象；报文畸形返回 null</returns>
        private static HttpMessage ParseHeader(ReadOnlySequence<byte> buffer, int headerLength)
        {
            if (buffer.First.Length >= headerLength)
            {
                // Analysis 的签名是 Span<byte>，但只做只读解析；AsMemory 只换视图，不复制字节。
                return TryAnalyze(MemoryMarshal.AsMemory(buffer.First).Span.Slice(0, headerLength));
            }

            var rented = MemoryPoolManager.Rent(headerLength);
            try
            {
                buffer.Slice(0, headerLength).CopyTo(new Span<byte>(rented, 0, headerLength));
                return TryAnalyze(new Span<byte>(rented, 0, headerLength));
            }
            finally
            {
                MemoryPoolManager.Return(rented, headerLength);
            }
        }

        /// <summary>调用现有报头解析器；解析异常在此吞掉并返回 null，由调用方按 400 报告。</summary>
        /// <param name="headerSpan">完整报头字节</param>
        /// <returns>解析成功返回消息对象；失败返回 null</returns>
        private static HttpMessage TryAnalyze(Span<byte> headerSpan)
        {
            HttpMessage message;
            try
            {
                if (RequestDataReader.Analysis(headerSpan, out message) <= 0 || message == null)
                    return null;
            }
            catch (Exception)
            {
                return null;
            }

            // 这里只用来判断报文能否解析，报头原文不再需要，立刻归还池化缓冲
            message.ReleaseHeaderSource();

            return message;
        }
    }
}