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
*命名空间：SAEA.WebSocket
*文件名： WsHandshakeSplitter
*版本号： v26.11.5.1
*唯一标识：b4416068-ee9c-4009-a3d4-29e934aee78b
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 13:03:00
*描述：WebSocket 握手阶段分帧器，按 HTTP 报头结束符切出握手块
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 13:03:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：WebSocket 握手阶段分帧器，按 HTTP 报头结束符切出握手块
*
*****************************************************************************/
using System;
using System.Buffers;
using SAEA.Sockets.Pipeline;

namespace SAEA.WebSocket
{
    /// <summary>
    /// WebSocket 握手阶段分帧器：在缓冲里找报头结束符 <c>\r\n\r\n</c>，整块报头到齐才产出。
    /// <para>
    /// 只切握手报文，不解析内容，也不校验请求行与字段；握手是否合法由链上的握手处理器判定。
    /// 产出握手块后把 <see cref="SwitchableSplitter.StopAfterCurrent"/> 置为 true，
    /// 让接收泵停切并等待切换到帧分帧器，避免紧随握手的首帧被当成另一个握手块。
    /// </para>
    /// <para>
    /// 报头一直不结束也必须挡住：超过上限直接按分帧协议错误抛出，由链尾收口。
    /// 实例只持有连接级配置，每连接新建。
    /// </para>
    /// </summary>
    public sealed class WsHandshakeSplitter : IFrameSplitter
    {
        private static readonly byte[] _headerEndMarker = new byte[] { 13, 10, 13, 10 };

        private readonly int _maxHeaderSize;

        /// <summary>
        /// 创建握手阶段分帧器。
        /// </summary>
        /// <param name="maxHeaderSize">握手报头上限（字节），必须大于 0</param>
        public WsHandshakeSplitter(int maxHeaderSize = 64 * 1024)
        {
            if (maxHeaderSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHeaderSize), "maxHeaderSize 必须大于 0。");

            _maxHeaderSize = maxHeaderSize;
        }

        /// <summary>可接受的最大握手块长度，与报头上限一致。</summary>
        public int MaxFrameLength
        {
            get { return _maxHeaderSize; }
        }

        /// <summary>
        /// 协议切换屏障。构造顺序上是先有本实例再有屏障，用属性回填以打破构造循环；
        /// 为 null 时只产出握手块、不设置屏障。
        /// </summary>
        public SwitchableSplitter Barrier { get; set; }

        /// <inheritdoc />
        public bool TrySplit(ReadOnlySequence<byte> buffer, out long consumed, out ReadOnlySequence<byte> frame)
        {
            consumed = 0;
            frame = default(ReadOnlySequence<byte>);

            if (buffer.IsEmpty)
                return false;

            var headerEnd = FindHeaderEnd(buffer);
            if (headerEnd < 0)
            {
                if (buffer.Length > _maxHeaderSize)
                    throw new FrameProtocolException(WebSocketProtocolErrors.HandshakeHeaderTooLarge);
                return false;
            }

            var headerLength = headerEnd + _headerEndMarker.Length;
            if (headerLength > _maxHeaderSize)
                throw new FrameProtocolException(WebSocketProtocolErrors.HandshakeHeaderTooLarge);

            frame = buffer.Slice(0, headerLength);
            consumed = headerLength;

            // 分帧器无法预知握手是否会被接受，一律先停切：被拒绝时由握手处理器复位屏障。
            var barrier = Barrier;
            if (barrier != null)
                barrier.StopAfterCurrent = true;

            return true;
        }

        /// <inheritdoc />
        public void Reset()
        {
            // 半帧留在接收泵的 Pipe 里，本实例不保存跨调用状态，空实现且可重复调用。
        }

        /// <summary>在可能跨段的缓冲里查找 <c>\r\n\r\n</c> 的起始偏移；未找到返回 -1。</summary>
        /// <param name="buffer">自上次消费位置起的未消费缓冲</param>
        /// <returns>报头结束符首字节的偏移</returns>
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
    }
}