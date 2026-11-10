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
*命名空间：SAEA.FileSocket
*文件名： FileMessageEncoder
*版本号： v26.11.5.1
*唯一标识：0d7a6000-086d-4cd2-b345-0fdfc6d41dff
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 11:20:00
*描述：文件传输出站编码器
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 11:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增，把文件传输协议对象编码成原始帧
*
*****************************************************************************/
using System;
using System.Threading.Tasks;
using SAEA.Common.Caching;
using SAEA.Sockets.Base;
using SAEA.Sockets.Pipeline;

namespace SAEA.FileSocket
{
    /// <summary>
    /// 文件传输出站编码器：把 <see cref="BaseSocketProtocal"/> 编码成 <see cref="RawFrame"/>。
    /// <para>
    /// 出站前缀节点，必须装在 <see cref="OutboundSerializingHandler"/> 左侧（更靠近链首），
    /// 编码得到的帧自带池化缓冲所有权，随帧移交给传输层后由传输层归还。
    /// </para>
    /// </summary>
    public sealed class FileMessageEncoder : OutboundHandlerAdapter
    {
        /// <summary>
        /// 编码一次出站写：协议对象写成 <c>[8B bodyLength][1B type][body]</c>，
        /// 已经是原始帧的消息（如有）原样透传。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="msg">待发送消息，只接受 <see cref="BaseSocketProtocal"/> 与 <see cref="RawFrame"/>。</param>
        /// <returns>下游处理完成后结束的任务。</returns>
        public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
        {
            var protocal = msg as BaseSocketProtocal;
            if (protocal != null)
            {
                // 按实际帧体申请容量：长度字段写在帧头，不随帧体重复占位。
                var capacity = (int)Math.Min((long)protocal.Content.Length + FrameConstants.P_Head, int.MaxValue);

                var writer = new PooledBufferWriter(capacity);
                try
                {
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
                "FileMessageEncoder 只接受 BaseSocketProtocal 或 RawFrame，实际为 "
                + (msg == null ? "(null)" : msg.GetType().FullName) + "。");
        }
    }
}