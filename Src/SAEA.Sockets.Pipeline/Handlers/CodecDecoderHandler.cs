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
*命名空间：SAEA.Sockets.Pipeline
*文件名： CodecDecoderHandler
*版本号： v26.11.5.1
*唯一标识：447f2707-8e16-402c-99cb-c2f9df9db697
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：入站第一站：把队列条目持有的帧字节交给 ICodec 解成信封
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：入站第一站：把队列条目持有的帧字节交给 ICodec 解成信封
*
*****************************************************************************/
using System;
using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 入站第一站：把队列条目持有的帧字节交给 <see cref="ICodec"/> 解成信封。
    /// 帧字节在解码完成后立即归还，<see cref="Envelope.Payload"/> 已是独立拷贝。
    /// </summary>
    public sealed class CodecDecoderHandler : InboundHandlerAdapter
    {
        private readonly ICodec _codec;

        /// <summary>以第一层编解码器创建入站解码处理器。</summary>
        /// <param name="codec">帧字节到信封的编解码器，不能为 null。</param>
        public CodecDecoderHandler(ICodec codec)
        {
            if (codec == null)
                throw new ArgumentNullException(nameof(codec));

            _codec = codec;
        }

        /// <summary>只接受 <see cref="PooledMessage"/>；解码完成后归还帧字节，再下传解出的信封。</summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="msg">池化帧消息，必须是 <see cref="PooledMessage"/>。</param>
        /// <returns>下游处理完成后结束的任务。</returns>
        public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
        {
            var pooled = msg as PooledMessage;
            if (pooled == null)
                throw new InvalidOperationException("CodecDecoderHandler 的入参必须是 PooledMessage。");

            // 解码失败时不在本 handler 归还，交给执行包的 finally 统一释放，避免异常路径提前归还。
            var decoded = _codec.Decode(pooled.Frame);

            pooled.Release();
            await ctx.FireChannelReadAsync(decoded).ConfigureAwait(false);
        }
    }
}