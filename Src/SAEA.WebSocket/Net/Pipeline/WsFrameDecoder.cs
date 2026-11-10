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
*文件名： WsFrameDecoder
*版本号： v26.11.5.1
*唯一标识：59d5bf6b-d257-4b88-b27b-cb0b1d799a38
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 13:45:00
*描述：WebSocket 入站帧解码与分片重组处理器
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 13:45:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：WebSocket 入站帧解码与分片重组处理器
*
*****************************************************************************/
using System;
using System.Threading.Tasks;
using SAEA.Common.Caching;
using SAEA.Sockets.Pipeline;
using SAEA.WebSocket.Model;
using SAEA.WebSocket.Type;

namespace SAEA.WebSocket
{
    /// <summary>
    /// WebSocket 入站解码器：把拆帧器产出的完整 wire frame 解成 <see cref="WSProtocal"/>。
    /// <para>
    /// 消息边界不等于帧边界，因此本类必须持每连接的分片状态：continuation 帧按序拼接，
    /// 控制帧可插在分片之间并立即产出，新的数据帧在上一条分片消息结束前一律按协议错误拒绝。
    /// 实例只服务一条连接，不跨连接复用。
    /// </para>
    /// <para>
    /// 所有权：输入帧由执行器持有并释放，本类绝不释放；产出的消息是新建对象，
    /// 交给下游后由链持有并在异常路径上补偿。整条数据帧解出的帧体直接移交内存池缓冲，
    /// 不做整帧复制；分片消息必须重组成独立数组，因此按精确长度分配、不标记池化。
    /// </para>
    /// </summary>
    public sealed class WsFrameDecoder : InboundHandlerAdapter
    {
        /// <summary>默认单条消息（含分片重组成后）允许的最大字节数。</summary>
        public const int DefaultMaxMessageSize = 64 * 1024 * 1024;

        /// <summary>控制帧帧体上限，由 RFC 6455 固定为 125 字节。</summary>
        private const int MaxControlPayloadLength = 125;

        /// <summary>分片重组首次分配的容量下界，避免逐片翻倍时产生过多中间数组。</summary>
        private const int InitialFragmentCapacity = 1024;

        private readonly bool _requireMask;
        private readonly int _maxMessageSize;

        private byte[] _fragments;
        private int _fragmentLength;
        private byte _fragmentedOpcode;

        /// <summary>
        /// 创建解码器。
        /// </summary>
        /// <param name="requireMask">是否要求收到的帧必须带掩码：服务端为 true，客户端为 false</param>
        /// <param name="maxMessageSize">单条消息上限（字节），必须大于 0</param>
        public WsFrameDecoder(bool requireMask, int maxMessageSize = DefaultMaxMessageSize)
        {
            if (maxMessageSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxMessageSize), "maxMessageSize 必须大于 0。");

            _requireMask = requireMask;
            _maxMessageSize = maxMessageSize;
        }

        /// <summary>是否要求收到的帧必须带掩码。</summary>
        public bool RequireMask
        {
            get { return _requireMask; }
        }

        /// <summary>单条消息（含分片重组成后）允许的最大字节数。</summary>
        public int MaxMessageSize
        {
            get { return _maxMessageSize; }
        }

        /// <summary>
        /// 解一个完整帧；分片未结束时暂存状态并返回，不向下游派发。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文</param>
        /// <param name="msg">拆帧器产出的完整帧，必须是 <see cref="PooledMessage"/></param>
        /// <returns>下游处理完成后结束的任务</returns>
        public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
        {
            var frame = msg as PooledMessage;
            if (frame == null)
                throw new InvalidOperationException("WsFrameDecoder 的入参必须是 PooledMessage。");

            var protocal = Decode(frame.Frame);
            if (protocal == null)
                return;

            await ctx.FireChannelReadAsync(protocal).ConfigureAwait(false);
        }

        /// <summary>
        /// 解析帧头、校验协议约束并还原帧体；不产生业务消息的中间分片返回 null。
        /// </summary>
        /// <param name="frame">完整 wire frame（帧头 + 帧体）</param>
        /// <returns>可直接派发的协议帧；中间分片为 null</returns>
        private WSProtocal Decode(System.ReadOnlyMemory<byte> frame)
        {
            var span = frame.Span;

            if (span.Length < 2)
                throw new FrameProtocolException(WebSocketProtocolErrors.FrameTooShort);

            var b0 = span[0];
            if ((b0 & 0x70) != 0)
                throw new FrameProtocolException(WebSocketProtocolErrors.UnsupportedRsv);

            var fin = (b0 & 0x80) != 0;
            var opcode = (byte)(b0 & 0x0f);

            if (opcode != (byte)WSProtocalType.Cont
                && opcode != (byte)WSProtocalType.Text
                && opcode != (byte)WSProtocalType.Binary
                && opcode != (byte)WSProtocalType.Close
                && opcode != (byte)WSProtocalType.Ping
                && opcode != (byte)WSProtocalType.Pong)
                throw new FrameProtocolException(WebSocketProtocolErrors.UnsupportedOpcode);

            var b1 = span[1];
            var masked = (b1 & 0x80) != 0;
            var len7 = b1 & 0x7f;

            if (_requireMask && !masked)
                throw new FrameProtocolException(WebSocketProtocolErrors.MissingMask);

            var extLen = len7 == 126 ? 2 : (len7 == 127 ? 8 : 0);
            var maskOffset = 2 + extLen;
            var headerLen = maskOffset + (masked ? 4 : 0);

            // 拆帧器只在帧头到齐后才产出，这里不足即说明两者实现已经不一致。
            if (span.Length < headerLen)
                throw new FrameProtocolException(WebSocketProtocolErrors.FrameLengthMismatch);

            long payloadLength;
            if (len7 < 126)
            {
                payloadLength = len7;
            }
            else if (len7 == 126)
            {
                payloadLength = (span[2] << 8) | span[3];
            }
            else
            {
                // 最高位为 1 表示长度超出 long 可表示范围，RFC 6455 禁止使用。
                if ((span[2] & 0x80) != 0)
                    throw new FrameProtocolException(WebSocketProtocolErrors.InvalidLength);

                ulong value = 0;
                for (var i = 0; i < 8; i++)
                    value = (value << 8) | span[2 + i];

                if (value > int.MaxValue)
                    throw new FrameProtocolException(WebSocketProtocolErrors.InvalidLength);

                payloadLength = (long)value;
            }

            if (opcode >= (byte)WSProtocalType.Close)
            {
                if (!fin)
                    throw new FrameProtocolException(WebSocketProtocolErrors.ControlFrameFragmented);
                if (payloadLength > MaxControlPayloadLength)
                    throw new FrameProtocolException(WebSocketProtocolErrors.ControlFrameTooLarge);
            }

            if (payloadLength > _maxMessageSize)
                throw new FrameProtocolException(WebSocketProtocolErrors.MessageTooLarge);

            // 分片状态校验必须先于缓冲分配：否则校验失败时已租出的缓冲没人归还。
            if (opcode == (byte)WSProtocalType.Cont && _fragmentedOpcode == 0)
                throw new FrameProtocolException(WebSocketProtocolErrors.UnexpectedContinuation);

            if ((opcode == (byte)WSProtocalType.Text || opcode == (byte)WSProtocalType.Binary)
                && _fragmentedOpcode != 0)
                throw new FrameProtocolException(WebSocketProtocolErrors.InterleavedDataFrame);

            var appendsFragment = opcode == (byte)WSProtocalType.Cont || !fin;
            if (opcode < (byte)WSProtocalType.Close
                && appendsFragment
                && (long)_fragmentLength + payloadLength > _maxMessageSize)
                throw new FrameProtocolException(WebSocketProtocolErrors.MessageTooLarge);

            if ((long)headerLen + payloadLength != span.Length)
                throw new FrameProtocolException(WebSocketProtocolErrors.FrameLengthMismatch);

            var length = (int)payloadLength;
            var payload = RentPayload(length);

            if (length > 0)
                CopyPayload(span, headerLen, length, masked, maskOffset, payload);

            return Assemble(opcode, fin, payload, length);
        }

        /// <summary>
        /// 按帧体长度取缓冲：超过小对象阈值走内存池，其余精确分配，避免为短帧占住 4KB 池块。
        /// </summary>
        /// <param name="length">帧体字节长度</param>
        /// <returns>可写入的帧体缓冲</returns>
        private static byte[] RentPayload(int length)
        {
            if (length == 0)
                return Array.Empty<byte>();

            if (length > MemoryPoolManager.SmallThreshold)
                return MemoryPoolManager.Rent(length);

            return new byte[length];
        }

        /// <summary>
        /// 把帧体还原到目标缓冲；带掩码时按 RFC 6455 的 4 字节循环异或还原。
        /// </summary>
        /// <param name="source">完整帧字节</param>
        /// <param name="headerLen">帧体起始偏移</param>
        /// <param name="length">帧体字节长度</param>
        /// <param name="masked">本帧是否带掩码</param>
        /// <param name="maskOffset">掩码键在帧内的偏移</param>
        /// <param name="payload">目标帧体缓冲</param>
        private static void CopyPayload(
            System.ReadOnlySpan<byte> source,
            int headerLen,
            int length,
            bool masked,
            int maskOffset,
            byte[] payload)
        {
            var body = source.Slice(headerLen, length);

            if (!masked)
            {
                body.CopyTo(payload);
                return;
            }

            var m0 = source[maskOffset];
            var m1 = source[maskOffset + 1];
            var m2 = source[maskOffset + 2];
            var m3 = source[maskOffset + 3];

            for (var i = 0; i < length; i++)
            {
                var key = (i & 3) == 0 ? m0 : ((i & 3) == 1 ? m1 : ((i & 3) == 2 ? m2 : m3));
                payload[i] = (byte)(body[i] ^ key);
            }
        }

        /// <summary>
        /// 按帧类型决定是派发、暂存还是结束分片重组；中间分片用到的池化缓冲在本方法内归还。
        /// </summary>
        /// <param name="opcode">帧操作码</param>
        /// <param name="fin">是否为消息末帧</param>
        /// <param name="payload">已还原的帧体</param>
        /// <param name="length">帧体字节长度</param>
        /// <returns>可派发的协议帧；中间分片为 null</returns>
        private WSProtocal Assemble(byte opcode, bool fin, byte[] payload, int length)
        {
            switch (opcode)
            {
                case (byte)WSProtocalType.Cont:
                    AppendFragment(payload, length);
                    ReturnPayloadIfPooled(payload, length);
                    if (!fin)
                        return null;
                    return TakeFragments();

                case (byte)WSProtocalType.Text:
                case (byte)WSProtocalType.Binary:
                    if (fin)
                        return CreateMessage(opcode, payload, length);

                    _fragmentedOpcode = opcode;
                    _fragmentLength = 0;
                    AppendFragment(payload, length);
                    ReturnPayloadIfPooled(payload, length);
                    return null;

                default:
                    return CreateMessage(opcode, payload, length);
            }
        }

        /// <summary>以帧体创建协议帧，帧体本身是否池化由长度决定，所有权随帧转移。</summary>
        /// <param name="opcode">帧操作码</param>
        /// <param name="payload">帧体缓冲</param>
        /// <param name="length">帧体字节长度</param>
        /// <returns>协议帧</returns>
        private static WSProtocal CreateMessage(byte opcode, byte[] payload, int length)
        {
            return new WSProtocal(opcode, payload, length)
            {
                IsPooled = length > MemoryPoolManager.SmallThreshold
            };
        }

        /// <summary>中间分片的帧体已复制进重组缓冲，池化来源必须立即归还，否则每片都会漏一块。</summary>
        /// <param name="payload">帧体缓冲</param>
        /// <param name="length">帧体字节长度</param>
        private static void ReturnPayloadIfPooled(byte[] payload, int length)
        {
            if (length > MemoryPoolManager.SmallThreshold)
                MemoryPoolManager.Return(payload, length);
        }

        /// <summary>把帧体追加到重组缓冲，容量不足时按需翻倍。</summary>
        /// <param name="payload">帧体缓冲</param>
        /// <param name="length">帧体字节长度</param>
        private void AppendFragment(byte[] payload, int length)
        {
            var required = _fragmentLength + length;

            if (_fragments == null)
            {
                _fragments = new byte[Math.Max(required, InitialFragmentCapacity)];
            }
            else if (_fragments.Length < required)
            {
                var capacity = _fragments.Length * 2;
                if (capacity < required)
                    capacity = required;
                Array.Resize(ref _fragments, capacity);
            }

            if (length > 0)
                Buffer.BlockCopy(payload, 0, _fragments, _fragmentLength, length);

            _fragmentLength = required;
        }

        /// <summary>
        /// 结束一次分片重组并交出消息。重组结果按精确长度分配：
        /// 池化缓冲的归还长度取帧体的 <c>BodyLength</c>，只有长度恰好等于租用大小才是安全的。
        /// </summary>
        /// <returns>重组后的协议帧</returns>
        private WSProtocal TakeFragments()
        {
            var opcode = _fragmentedOpcode;
            var length = _fragmentLength;
            var buffer = _fragments;

            _fragmentedOpcode = 0;
            _fragmentLength = 0;
            _fragments = null;

            if (length == 0)
                return new WSProtocal(opcode, Array.Empty<byte>());

            if (buffer.Length != length)
                Array.Resize(ref buffer, length);

            return new WSProtocal(opcode, buffer);
        }
    }
}