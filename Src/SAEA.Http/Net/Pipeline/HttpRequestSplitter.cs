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
*修改时间：2026/11/10 10:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：报头完整但解析失败改判 400，避免畸形请求挂住连接
*
*****************************************************************************/
using System;
using System.Buffers;
using System.Runtime.InteropServices;
using SAEA.Common.Caching;
using SAEA.Http.Base;
using SAEA.Sockets.Pipeline;

namespace SAEA.Http
{
    /// <summary>
    /// HTTP 请求分帧器：先从缓冲里找报头结束符 <c>\r\n\r\n</c> 并解析报头，再按 <c>Content-Length</c> 等齐请求体，
    /// 只有整帧到齐才产出。报头与请求体各有独立上限，<c>Content-Length</c> 超限在报头解析后立即判定，不等请求体。
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

            // 报头结束符已到齐，解析失败只能是报文畸形：不能当「数据不足」继续等，否则请求行非法的连接既收不到响应也不会断开。
            var message = ParseHeader(buffer, headerLength);
            if (message == null)
                throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);

            if (message.Headers.TryGetValue("Transfer-Encoding", out var transferEncoding)
                && !string.IsNullOrEmpty(transferEncoding)
                && transferEncoding.IndexOf("chunked", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // 不做 chunked 解码：继续按 Content-Length 切会把 chunk 字节误当后续请求。
                throw new FrameProtocolException(HttpProtocolErrorCodes.ChunkedNotSupported);
            }

            var contentLength = 0;
            if (message.Headers.TryGetValue("Content-Length", out var contentLengthText))
            {
                if (!int.TryParse(contentLengthText, out contentLength) || contentLength < 0)
                    throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);
            }

            // 必须在这里判超限：等请求体到齐再判，超限字节早已进入内存。
            if (contentLength > _maxRequestSize)
                throw new FrameProtocolException(HttpProtocolErrorCodes.RequestEntityTooLarge);

            var frameLength = (long)headerLength + contentLength;
            if (buffer.Length < frameLength) return false;

            frame = buffer.Slice(0, frameLength);
            consumed = frameLength;

            if (message.Headers.TryGetValue("Upgrade", out var upgrade)
                && !string.IsNullOrEmpty(upgrade)
                && upgrade.IndexOf("websocket", StringComparison.OrdinalIgnoreCase) >= 0)
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

            return message;
        }
    }
}