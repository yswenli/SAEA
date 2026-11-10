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
*文件名： WsMessageEncoder
*版本号： v26.11.5.1
*唯一标识：b74c16a2-c1a5-48fc-952a-09bea320dd41
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 13:50:00
*描述：WebSocket 出站编码处理器
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 13:50:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：WebSocket 出站编码处理器
*
*****************************************************************************/
using System;
using System.Threading.Tasks;
using SAEA.Common.Caching;
using SAEA.Sockets.Pipeline;
using SAEA.WebSocket.Model;

namespace SAEA.WebSocket
{
    /// <summary>
    /// WebSocket 出站编码器：把 <see cref="WSProtocal"/> 编码成一条 wire frame 的 <see cref="RawFrame"/>。
    /// <para>
    /// 出站前缀节点，必须装在 <see cref="OutboundSerializingHandler"/> 左侧（更靠近链首）。
    /// 服务端发送不掩码、客户端发送必须掩码，方向由构造函数固定，实例不跨角色复用。
    /// </para>
    /// <para>
    /// 编码得到的帧自带池化缓冲所有权，随帧移交给传输层后由传输层归还；
    /// 已经是 <see cref="RawFrame"/> 的消息（如握手应答）原样透传，便于绕过帧编码直接写原始字节。
    /// </para>
    /// </summary>
    public sealed class WsMessageEncoder : OutboundHandlerAdapter
    {
        /// <summary>帧头最大字节数：2 字节基础头 + 8 字节扩展长度 + 4 字节掩码。</summary>
        private const int MaxHeaderLength = 14;

        private readonly bool _masked;
        private readonly WsHandshakeState _handshake;

        /// <summary>
        /// 创建编码器。
        /// </summary>
        /// <param name="masked">发送方向是否必须掩码：客户端为 true，服务端为 false</param>
        /// <param name="handshake">握手完成标记，为 null 时不校验握手状态</param>
        public WsMessageEncoder(bool masked, WsHandshakeState handshake = null)
        {
            _masked = masked;
            _handshake = handshake;
        }

        /// <summary>发送方向是否必须掩码。</summary>
        public bool Masked
        {
            get { return _masked; }
        }

        /// <summary>
        /// 编码一次出站写：协议帧写成 wire frame，原始帧原样透传。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="msg">待发送消息，只接受 <see cref="WSProtocal"/> 与 <see cref="RawFrame"/>。</param>
        /// <returns>下游处理完成后结束的任务。</returns>
        public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
        {
            var protocal = msg as WSProtocal;
            if (protocal != null)
            {
                // 数据帧不得抢在握手前面上线：对端会把它们当成握手报文解析并按协议错误断开。
                var handshake = _handshake;
                if (handshake != null && !handshake.IsCompleted)
                    throw new ChannelClosedException("WebSocket 握手尚未完成，不能发送数据帧。");

                if (protocal.BodyLength > int.MaxValue - MaxHeaderLength)
                    throw new InvalidOperationException(
                        "WebSocket 帧体长度超出可编码上限:" + protocal.BodyLength + "。");

                var capacity = (int)protocal.BodyLength + MaxHeaderLength;

                var writer = new PooledBufferWriter(capacity);
                try
                {
                    if (_masked)
                        protocal.WriteMaskedTo(writer);
                    else
                        protocal.WriteTo(writer);

                    return ctx.WriteAsync(new RawFrame(writer.WrittenMemory, writer));
                }
                catch
                {
                    // 只在移交前失败时归还；成功交出后所有权随帧走，不能再由本节点释放。
                    writer.Dispose();
                    throw;
                }
            }

            if (msg is RawFrame)
                return ctx.WriteAsync(msg);

            throw new InvalidOperationException(
                "WsMessageEncoder 只接受 WSProtocal 或 RawFrame，实际为 "
                + (msg == null ? "(null)" : msg.GetType().FullName) + "。");
        }
    }
}