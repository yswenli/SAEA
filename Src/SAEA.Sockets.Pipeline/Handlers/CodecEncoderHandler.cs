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
*文件名： CodecEncoderHandler
*版本号： v26.11.5.1
*唯一标识：a9420f0b-e626-4169-a734-1b37de4b862e
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：出站编码：把信封的 payload 写进池化缓冲，产出只含 body 的中间体
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：出站编码：把信封的 payload 写进池化缓冲，产出只含 body 的中间体
*
*****************************************************************************/
using System;
using System.Threading.Tasks;
using SAEA.Common.Caching;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 出站编码：把信封的 payload 写进池化缓冲，产出只含 body 的中间体。
    /// 中间体在本次出站传递返回（或抛异常）后立即释放，下游只能同步读取 body。
    /// </summary>
    public sealed class CodecEncoderHandler : OutboundHandlerAdapter
    {
        private readonly ICodec _codec;

        /// <summary>以第一层编解码器创建出站编码处理器。</summary>
        /// <param name="codec">信封到 body 的编解码器，不能为 null。</param>
        public CodecEncoderHandler(ICodec codec)
        {
            if (codec == null)
                throw new ArgumentNullException(nameof(codec));

            _codec = codec;
        }

        /// <summary>只接受 <see cref="Envelope"/>；编码结果交给下游补帧头。</summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="msg">待编码的消息，必须是 <see cref="Envelope"/>。</param>
        /// <returns>下游写出完成后结束的任务。</returns>
        public override async Task WriteAsync(IChannelHandlerContext ctx, object msg)
        {
            var envelope = msg as Envelope;
            if (envelope == null)
                throw new InvalidOperationException("CodecEncoderHandler 只接受 Envelope。");

            var writer = new PooledBufferWriter(Math.Max(256, envelope.Payload == null ? 0 : envelope.Payload.Length));
            EncodedBody encoded = null;
            try
            {
                _codec.Encode(envelope, writer);
                encoded = new EncodedBody(envelope.Type, writer.WrittenMemory, writer);
                await ctx.WriteAsync(encoded).ConfigureAwait(false);
            }
            finally
            {
                // 编码器抛异常时 encoded 仍为 null，此时由这里归还刚借出的缓冲。
                if (encoded != null)
                    encoded.Dispose();
                else
                    writer.Dispose();
            }
        }
    }
}