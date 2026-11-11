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
*命名空间：SAEA.FTP
*文件名： FtpDataChunkEncoder
*版本号： v26.11.5.1
*唯一标识：820275a5-065e-4b1f-b4a2-2147d48d3f89
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 16:37:14
*描述：FTP 数据通道出站编码器，把数据块转成原始帧
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 16:37:14
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增数据通道出站编码器
*
*****************************************************************************/
using System;
using System.Threading.Tasks;
using SAEA.Sockets.Pipeline;

namespace SAEA.FTP
{
    /// <summary>
    /// 数据通道出站编码器：把 <see cref="FtpDataChunk"/> 转成 <see cref="RawFrame"/>。
    /// <para>
    /// 数据通道是「读多少算多少」的无边界透传，不需要额外帧头：数据块的字节就是完整帧字节。
    /// 数据块自身作为 <see cref="RawFrame"/> 的 owner 随帧移交，成功交出后由传输层释放；
    /// 移交前失败由本节点归还，绝不重复释放。
    /// </para>
    /// </summary>
    public sealed class FtpDataChunkEncoder : OutboundHandlerAdapter
    {
        /// <summary>
        /// 编码一次出站写：数据块转成原始帧，已经是原始帧的消息直接透传。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="msg">待发送消息，只接受 <see cref="FtpDataChunk"/> 与 <see cref="RawFrame"/>。</param>
        /// <returns>下游处理完成后结束的任务。</returns>
        public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
        {
            var chunk = msg as FtpDataChunk;
            if (chunk != null)
            {
                try
                {
                    // 所有权随帧移交：RawFrame 的 owner 就是数据块本身，传输层在发送完成或失败后归还。
                    return ctx.WriteAsync(new RawFrame(chunk.Memory, chunk));
                }
                catch
                {
                    // 只在移交前失败时归还；成功交出后所有权随帧走，不能再由本节点释放。
                    chunk.Dispose();
                    throw;
                }
            }

            if (msg is RawFrame)
                return ctx.WriteAsync(msg);

            throw new InvalidOperationException(
                "FtpDataChunkEncoder 只接受 FtpDataChunk 或 RawFrame，实际为 "
                + (msg == null ? "(null)" : msg.GetType().FullName) + "。");
        }
    }
}