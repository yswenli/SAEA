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
*修改时间：2026/11/24 18:12:35
*修改人： yswenli
*版本号： 
*描述：v26.11.24.2
*
*****************************************************************************/
using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Http.Base;
using SAEA.Sockets.Pipeline;

namespace SAEA.Http
{
    /// <summary>
    /// HTTP 请求分帧器：先从缓冲里找报头结束符 <c>\r\n\r\n</c> 并只扫出定帧需要的报头字段，再按 <c>Content-Length</c>
    /// 或 chunked 语法等齐请求体，只有整帧到齐才产出。报头与请求体各有独立上限，<c>Content-Length</c> 超限在报头扫描后立即判定，不等请求体。
    /// <para>
    /// chunked 只做定界扫描、不解码：整帧（报头 + 原始 chunk 字节，含 chunk 头与 trailer）原样下发，由解码器还原负载。
    /// 扫描状态保存在实例上并按偏移续扫，跨多次 <see cref="TrySplit"/> 不重扫已消费字节。
    /// </para>
    /// <para>
    /// 识别 <c>Expect: 100-continue</c> 时通过回调让调用方先回 100 中间响应，每个请求最多一次。
    /// </para>
    /// <para>
    /// 只切 HTTP 请求，不生成任何响应：协议错误统一用
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

        // 定帧只关心的四个头名与三个值子串，一律按小写 ASCII 逐字节比较，避免为目标名分配字符串。
        private static readonly byte[] _transferEncodingName = Encoding.ASCII.GetBytes("transfer-encoding");
        private static readonly byte[] _contentLengthName = Encoding.ASCII.GetBytes("content-length");
        private static readonly byte[] _upgradeName = Encoding.ASCII.GetBytes("upgrade");
        private static readonly byte[] _expectName = Encoding.ASCII.GetBytes("expect");
        private static readonly byte[] _chunkedToken = Encoding.ASCII.GetBytes("chunked");
        private static readonly byte[] _webSocketToken = Encoding.ASCII.GetBytes("websocket");
        private static readonly byte[] _expectContinueToken = Encoding.ASCII.GetBytes("100-continue");

        // KMP 回退表与目标子串一起静态构造，热路径上只做整数比较。
        private static readonly int[] _chunkedFallback = BuildTokenFallback(_chunkedToken);
        private static readonly int[] _webSocketFallback = BuildTokenFallback(_webSocketToken);
        private static readonly int[] _expectContinueFallback = BuildTokenFallback(_expectContinueToken);

        // chunk-size 的十六进制位数上限：8 位足以覆盖 int 范围，多出的位数只可能是畸形或恶意输入。
        private const int MaxChunkSizeDigits = 8;

        // trailer 累计上限：trailer 会被整段丢弃，不能让畸形请求用它把分帧缓冲撑大。
        private const int MaxChunkTrailerBytes = 8 * 1024;

        private const int ChunkPhaseSize = 0;
        private const int ChunkPhaseSizeExtension = 1;
        private const int ChunkPhaseSizeLineFeed = 2;
        private const int ChunkPhaseData = 3;
        private const int ChunkPhaseDataCarriageReturn = 4;
        private const int ChunkPhaseDataLineFeed = 5;
        private const int ChunkPhaseTrailerLineStart = 6;
        private const int ChunkPhaseTrailerLine = 7;
        private const int ChunkPhaseTrailerLineFeed = 8;
        private const int ChunkPhaseTrailerEmptyLineFeed = 9;

        private readonly int _maxRequestSize;
        private readonly int _maxHeaderSize;
        private readonly int _maxFrameLength;

        // chunk 扫描进度：偏移相对当前请求起点，跨调用增量推进；帧完成后全部复位
        private long _chunkScanOffset;
        private long _chunkDecodedLength;
        private long _chunkRemaining;
        private long _chunkTrailerBytes;
        private int _chunkPhase;
        private int _chunkSizeDigits;
        private bool _chunkScanning;

        // 100 Continue 每个请求只发一次：回调触发前先置位，回调抛异常也不会重复发送
        private bool _continueSent;

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

        /// <summary>
        /// 请求带 <c>Expect: 100-continue</c> 且请求体尚未到齐时的回调，由调用方写出 100 中间响应。
        /// 每个请求最多触发一次；只用于让客户端尽快发请求体，不等待客户端确认后再继续读取。
        /// </summary>
        internal Action ExpectContinueHandler { get; set; }

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

            // 定帧只认 Transfer-Encoding、Content-Length、Upgrade、Expect 四个字段，直接扫字节即可；
            // 完整的 HttpMessage 与报头字典会在解码阶段再建一次，这里重复解析等于每请求白分配约 1.1 KB。
            var header = ScanHeaderFields(buffer, headerEnd, headerLength);

            // 报头结束符已到齐，请求行畸形不能当「数据不足」继续等，否则这类连接既收不到响应也不会断开。
            if (header.Malformed)
                throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);

            // 两种定界方式同时出现时取值有歧义，按协议拒绝，避免后续把 body 切成两条请求。
            if (header.Chunked && header.ContentLengthSeen)
                throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);

            if (header.Chunked)
            {
                long chunkedLength;
                if (!TryScanChunkedBody(buffer, headerLength, out chunkedLength))
                {
                    // 已通过畸形与超限判定，才允许在等请求体时回 100 Continue
                    FireExpectContinue(header);
                    return false;
                }

                frame = buffer.Slice(0, chunkedLength);
                consumed = chunkedLength;

                ApplyUpgradeBarrier(header);

                // 帧已切出，下一条请求重新获得一次 100 Continue 机会
                ResetContinueState();

                return true;
            }

            // 非 chunked 请求不需要 chunk 状态；显式复位，避免上一个请求的半截状态残留
            ResetChunkState();

            if (header.ContentLengthInvalid)
                throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);

            // 必须在这里判超限：等请求体到齐再判，超限字节早已进入内存。
            if (header.ContentLengthOverflow || header.ContentLength > _maxRequestSize)
            {
                // 413 比 400 更具体，而旧实现是先完整解析报头、解析失败即回 400，超限判定在其后。
                // 超限请求不是热路径，这里仍走一次完整解析，保证「畸形报文优先按 400 拒绝」的顺序不变。
                if (ParseHeader(buffer, headerLength) == null)
                    throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);

                // 数值超出 long 上限时无法算出剩余量，与 chunked 一样交回处理器按请求体上限兜底。
                if (header.ContentLengthOverflow)
                {
                    throw new FrameProtocolException(HttpProtocolErrorCodes.RequestEntityTooLarge)
                    {
                        UnreadBodyBytes = -1
                    };
                }

                // 已到达的请求体字节留在这个缓冲里，收口只需等待 Content-Length 之外的剩余部分；
                // 剩余量只在定长请求可知，chunked 保持 -1 由处理器按请求体上限兜底。
                var received = buffer.Length - headerLength;
                var unread = header.ContentLength - (received > 0 ? received : 0);
                if (unread < 0) unread = 0;

                throw new FrameProtocolException(HttpProtocolErrorCodes.RequestEntityTooLarge)
                {
                    UnreadBodyBytes = unread
                };
            }

            var contentLength = header.ContentLength;
            var frameLength = (long)headerLength + contentLength;
            if (buffer.Length < frameLength)
            {
                // 超限与非法 Content-Length 已在上面判定，这里只可能是请求体还没到齐
                FireExpectContinue(header);
                return false;
            }

            frame = buffer.Slice(0, frameLength);
            consumed = frameLength;

            ApplyUpgradeBarrier(header);

            // 帧已切出，下一条请求重新获得一次 100 Continue 机会
            ResetContinueState();

            return true;
        }

        /// <inheritdoc />
        public void Reset()
        {
            // 半帧数据留在接收泵的 Pipe 里，这里只需清掉跨调用保存的 chunk 扫描与 100 Continue 状态
            ResetChunkState();
            ResetContinueState();
        }

        /// <summary>升级请求产出后停切，等待业务决定是否切换协议。</summary>
        /// <param name="header">报头扫描结果</param>
        private void ApplyUpgradeBarrier(HeaderFields header)
        {
            if (!header.UpgradeWebSocket) return;

            // 分帧器无法预知业务是否接受升级，一律停切，避免紧跟其后的 WS 首帧被当成 HTTP 请求。
            var barrier = Barrier;
            if (barrier != null)
                barrier.StopAfterCurrent = true;
        }

        /// <summary>清空 chunk 扫描状态，使下一个请求从 chunk-size 行重新开始。</summary>
        private void ResetChunkState()
        {
            _chunkScanning = false;
            _chunkScanOffset = 0;
            _chunkDecodedLength = 0;
            _chunkRemaining = 0;
            _chunkTrailerBytes = 0;
            _chunkPhase = ChunkPhaseSize;
            _chunkSizeDigits = 0;
        }

        /// <summary>允许下一条请求再触发一次 100 Continue。</summary>
        private void ResetContinueState()
        {
            _continueSent = false;
        }

        /// <summary>
        /// 在请求体尚未到齐时触发一次 100 Continue 回调。
        /// <para>
        /// 每个请求最多一次；只用于让客户端尽快发请求体，不等待客户端确认。回调异常只记日志，不能影响切帧。
        /// </para>
        /// </summary>
        /// <param name="header">报头扫描结果</param>
        private void FireExpectContinue(HeaderFields header)
        {
            if (!header.ExpectContinue || _continueSent) return;

            // 没有请求体时（无 chunked 且 Content-Length 为 0）不需要 100 Continue
            if (!header.Chunked && header.ContentLength <= 0) return;

            var handler = ExpectContinueHandler;
            if (handler == null) return;

            // 先置位再调用：回调抛异常后下次也不重复发送
            _continueSent = true;

            try
            {
                handler();
            }
            catch (Exception ex)
            {
                LogHelper.Error("HttpRequestSplitter.ExpectContinue", ex);
            }
        }

        /// <summary>
        /// 增量扫描 chunked 请求体：只按语法定界，不解码负载，跨调用从 <see cref="_chunkScanOffset"/> 续扫。
        /// <para>
        /// 整帧包含报头与原始 chunk 字节（chunk 头、chunk-ext、数据、trailer），由解码器负责还原负载。
        /// 数据不足时保留全部状态返回 false，下次调用接着推进，不重扫已消费字节。
        /// </para>
        /// </summary>
        /// <param name="buffer">自上次消费位置起的未消费缓冲，含完整报头</param>
        /// <param name="bodyStart">请求体首字节偏移，即报头长度</param>
        /// <param name="frameLength">整帧长度，含报头与原始 chunk 字节</param>
        /// <returns>终止块与 trailer 都已到齐返回 true；数据不足返回 false</returns>
        private bool TryScanChunkedBody(ReadOnlySequence<byte> buffer, int bodyStart, out long frameLength)
        {
            frameLength = 0;

            if (!_chunkScanning)
            {
                // 新请求：从报头之后的第一行 chunk-size 开始
                _chunkScanning = true;
                _chunkScanOffset = bodyStart;
                _chunkDecodedLength = 0;
                _chunkRemaining = 0;
                _chunkTrailerBytes = 0;
                _chunkPhase = ChunkPhaseSize;
                _chunkSizeDigits = 0;
            }

            while (_chunkScanOffset < buffer.Length)
            {
                // 原始字节同样计入帧预算：超限不等到数据到齐，直接按 413 拒绝
                if (_chunkScanOffset > (long)_maxFrameLength)
                    throw new FrameProtocolException(HttpProtocolErrorCodes.RequestEntityTooLarge);

                var value = ByteAt(buffer, _chunkScanOffset);

                switch (_chunkPhase)
                {
                    case ChunkPhaseSize:
                        {
                            var digit = ToHexValue(value);

                            if (digit >= 0)
                            {
                                if (++_chunkSizeDigits > MaxChunkSizeDigits)
                                    throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);

                                _chunkRemaining = _chunkRemaining * 16 + digit;

                                // 已解码长度只会让总长更大，单个 size 超限即可提前拒绝；
                                // 与 int 上限显式比较，避免 8 位十六进制累加后的取值被误读成 int 语义
                                if (_chunkRemaining > (long)_maxRequestSize)
                                    throw new FrameProtocolException(HttpProtocolErrorCodes.RequestEntityTooLarge);

                                _chunkScanOffset++;
                                continue;
                            }

                            // size 行至少要有一位十六进制数字
                            if (_chunkSizeDigits == 0)
                                throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);

                            if (value == 59)
                            {
                                // chunk-ext 内容忽略，但同样只接受 CRLF 收尾
                                _chunkPhase = ChunkPhaseSizeExtension;
                                _chunkScanOffset++;
                                continue;
                            }

                            if (value == 13)
                            {
                                _chunkPhase = ChunkPhaseSizeLineFeed;
                                _chunkScanOffset++;
                                continue;
                            }

                            throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);
                        }

                    case ChunkPhaseSizeExtension:
                        if (value == 13)
                        {
                            _chunkPhase = ChunkPhaseSizeLineFeed;
                            _chunkScanOffset++;
                            continue;
                        }

                        // 扩展里出现裸 LF 说明行尾不是 CRLF
                        if (value == 10)
                            throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);

                        _chunkScanOffset++;
                        continue;

                    case ChunkPhaseSizeLineFeed:
                        if (value != 10)
                            throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);

                        _chunkScanOffset++;

                        // size 行完整后再累加解码长度，超限即刻 413，不等 chunk 数据到齐
                        _chunkDecodedLength += _chunkRemaining;

                        if (_chunkDecodedLength > (long)_maxRequestSize)
                            throw new FrameProtocolException(HttpProtocolErrorCodes.RequestEntityTooLarge);

                        _chunkPhase = _chunkRemaining == 0 ? ChunkPhaseTrailerLineStart : ChunkPhaseData;
                        continue;

                    case ChunkPhaseData:
                        {
                            // 数据段长度已知，整段跳过，不逐字节比较
                            var available = buffer.Length - _chunkScanOffset;
                            var take = _chunkRemaining < available ? _chunkRemaining : available;

                            _chunkScanOffset += take;
                            _chunkRemaining -= take;

                            // data 段可能一次跨过帧预算并正好停在缓冲末尾，只靠循环入口判定会漏到下一次读取
                            if (_chunkScanOffset > (long)_maxFrameLength)
                                throw new FrameProtocolException(HttpProtocolErrorCodes.RequestEntityTooLarge);

                            if (_chunkRemaining == 0)
                                _chunkPhase = ChunkPhaseDataCarriageReturn;

                            continue;
                        }

                    case ChunkPhaseDataCarriageReturn:
                        if (value != 13)
                            throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);

                        _chunkPhase = ChunkPhaseDataLineFeed;
                        _chunkScanOffset++;
                        continue;

                    case ChunkPhaseDataLineFeed:
                        if (value != 10)
                            throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);

                        _chunkScanOffset++;

                        // 回到 chunk-size 行，逐块复位 size 相关状态
                        _chunkPhase = ChunkPhaseSize;
                        _chunkSizeDigits = 0;
                        _chunkRemaining = 0;
                        continue;

                    case ChunkPhaseTrailerLineStart:
                        if (value == 13)
                        {
                            _chunkPhase = ChunkPhaseTrailerEmptyLineFeed;
                            _chunkScanOffset++;
                            continue;
                        }

                        // trailer 内容整段丢弃，只需要判断行尾
                        _chunkPhase = ChunkPhaseTrailerLine;
                        continue;

                    case ChunkPhaseTrailerLine:
                        if (value == 13)
                        {
                            _chunkPhase = ChunkPhaseTrailerLineFeed;
                            _chunkScanOffset++;
                            continue;
                        }

                        // 累计 trailer 字节数，避免畸形请求用 trailer 把分帧缓冲撑大
                        if (++_chunkTrailerBytes > MaxChunkTrailerBytes)
                            throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);

                        _chunkScanOffset++;
                        continue;

                    case ChunkPhaseTrailerLineFeed:
                        if (value != 10)
                            throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);

                        _chunkScanOffset++;
                        _chunkPhase = ChunkPhaseTrailerLineStart;
                        continue;

                    case ChunkPhaseTrailerEmptyLineFeed:
                        if (value != 10)
                            throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);

                        _chunkScanOffset++;
                        frameLength = _chunkScanOffset;

                        // 整帧到齐，先清状态再返回，否则下一条请求会从半截 chunk 接着扫
                        ResetChunkState();

                        return true;
                }
            }

            return false;
        }

        /// <summary>把十六进制 ASCII 转成数值，非十六进制字符返回 -1。</summary>
        /// <param name="value">待转换的字节</param>
        /// <returns>0-15 的数值，或 -1</returns>
        private static int ToHexValue(byte value)
        {
            if (value >= 48 && value <= 57) return value - 48;
            if (value >= 65 && value <= 70) return value - 55;
            if (value >= 97 && value <= 102) return value - 87;

            return -1;
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

            /// <summary>是否出现过 Content-Length 头；用来和 chunked 互斥判定，不看取值是否合法</summary>
            public bool ContentLengthSeen;

            /// <summary>Content-Length 不是非负整数</summary>
            public bool ContentLengthInvalid;

            /// <summary>Content-Length 是合法数字但超出 long 上限，无法得知剩余量</summary>
            public bool ContentLengthOverflow;

            /// <summary>Content-Length 的数值，字段缺失时为 0</summary>
            public long ContentLength;

            /// <summary>Upgrade 的值里含 websocket</summary>
            public bool UpgradeWebSocket;

            /// <summary>Expect 的值里含 100-continue</summary>
            public bool ExpectContinue;
        }

        /// <summary>
        /// 逐字节扫描报头行，结果写进 <see cref="HeaderFields"/>。
        /// <para>
        /// 头名按 ASCII 折大小写与目标名比较，值就地判定 chunked / websocket / 100-continue 子串与 Content-Length 数值；
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
            private int _expectNameProgress;
            private bool _chunkedSeen;
            private bool _webSocketSeen;
            private bool _expectContinueSeen;
            private int _chunkedProgress;
            private int _webSocketProgress;
            private int _expectContinueProgress;
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
                    _expectNameProgress = AdvanceName(_expectName, _expectNameProgress, foldedName);
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

                    if (_expectNameProgress == _expectName.Length)
                        Fields.ExpectContinue = _expectContinueSeen;

                    if (_contentLengthNameProgress == _contentLengthName.Length)
                    {
                        // 只要头名命中就记出现，取值合法性由 ContentLengthInvalid 单独判定
                        Fields.ContentLengthSeen = true;

                        // 同名头重复出现时取最后一个，与报头字典「后写覆盖」一致；
                        // "-0" 解析成功且不小于 0，仍视为 0，所以先合成带符号的值再判负。
                        var value = _contentLengthNegative ? -_contentLengthValue : _contentLengthValue;

                        var valid = _colonCount == 1
                            && _contentLengthDigits > 0
                            && _contentLengthState != 5
                            && value >= 0;

                        // 语法非法按 400 拒绝；语法合法但数值超范围属于「太大」，由超限分支按 413 处理。
                        Fields.ContentLengthInvalid = !valid;
                        Fields.ContentLengthOverflow = valid && _contentLengthOverflow;
                        Fields.ContentLength = valid && !_contentLengthOverflow ? value : 0;
                    }
                }

                _lineStarted = false;
                _colonSeen = false;
                _colonCount = 0;
                _transferNameProgress = 0;
                _contentLengthNameProgress = 0;
                _upgradeNameProgress = 0;
                _expectNameProgress = 0;
                _chunkedSeen = false;
                _webSocketSeen = false;
                _expectContinueSeen = false;
                _chunkedProgress = 0;
                _webSocketProgress = 0;
                _expectContinueProgress = 0;
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

                if (!_expectContinueSeen)
                {
                    _expectContinueProgress = AdvanceToken(_expectContinueToken, _expectContinueFallback, _expectContinueProgress, folded);
                    if (_expectContinueProgress == _expectContinueToken.Length) _expectContinueSeen = true;
                }
            }

            /// <summary>按 long.TryParse(NumberStyles.Integer) 的规则推进 Content-Length：空白、符号、数字、空白。</summary>
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

            /// <summary>累加一位十进制数字；超出 long 上限按数值超范围处理。</summary>
            /// <param name="value">数字字节</param>
            private void AppendDigits(byte value)
            {
                _contentLengthDigits++;

                if (_contentLengthOverflow) return;

                var digit = value - 48;

                // 先判再加：越界值一旦乘 10 就会回绕成小数字，超范围会被误当成合法长度。
                if (_contentLengthValue > (long.MaxValue - digit) / 10)
                {
                    _contentLengthOverflow = true;
                    _contentLengthValue = 0;
                    return;
                }

                _contentLengthValue = _contentLengthValue * 10 + digit;
            }
        }

        /// <summary>
        /// 只扫出定帧需要的报头字段：Transfer-Encoding、Content-Length、Expect、Upgrade。
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