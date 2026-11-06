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
*文件名： FrameEncoderHandler
*版本号： v26.11.5.1
*唯一标识：d3d0c70c-e853-4cc8-92aa-60f190a926ee
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：出站最后一站：把 body 与类型码写成完整帧，并把最终缓冲的所有权包进 TransportFrame
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：出站最后一站：把 body 与类型码写成完整帧，并把最终缓冲的所有权包进 TransportFrame
*
*****************************************************************************/
using System;
using System.Threading.Tasks;
using SAEA.Common.Caching;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 出站最后一站：把 body 与类型码写成完整帧，并把最终缓冲的所有权包进 <see cref="TransportFrame"/>。
    /// 成功返回即表示所有权已交给链头/传输层；失败时由本处理器归还。
    /// </summary>
    public sealed class FrameEncoderHandler : OutboundHandlerAdapter
    {
        private readonly IFrameEncoder _encoder;

        /// <summary>以出站分帧器创建帧编码处理器。</summary>
        public FrameEncoderHandler(IFrameEncoder encoder)
        {
            if (encoder == null)
                throw new ArgumentNullException(nameof(encoder));

            _encoder = encoder;
        }

        /// <summary>只接受 <see cref="EncodedBody"/>；补完包头后只向链头交 <see cref="TransportFrame"/>。</summary>
        public override async Task WriteAsync(IChannelHandlerContext ctx, object msg)
        {
            var encoded = msg as EncodedBody;
            if (encoded == null)
                throw new InvalidOperationException("FrameEncoderHandler 之前必须经过 CodecEncoderHandler。");

            var writer = new PooledBufferWriter(encoded.Body.Length + FrameConstants.P_Head);
            TransportFrame frame = null;
            try
            {
                _encoder.Encode(encoded.Body, encoded.TypeCode, writer);
                frame = new TransportFrame(writer.WrittenMemory, writer);
                await ctx.WriteAsync(frame).ConfigureAwait(false);
            }
            catch
            {
                // 正常返回表示所有权已经交出，只在这里归还尚未交付的缓冲。
                if (frame != null)
                    frame.Dispose();
                else
                    writer.Dispose();
                throw;
            }
        }
    }
}