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
*文件名： InboundLoggingHandler
*版本号： v26.11.5.1
*唯一标识：6087b6be-4298-4dae-8217-0a1e5fa0ee62
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：入站日志：记录本次消息在本节点下游的执行耗时
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：入站日志：记录本次消息在本节点下游的执行耗时
*
*****************************************************************************/
using System.Diagnostics;
using System.Threading.Tasks;
using SAEA.Common;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 入站日志：记录本次消息在本节点下游的执行耗时。
    /// <para>
    /// 计时口径是从调用下游到下游返回（含 <c>await</c> 期间）。本处理器不改变消息对象、
    /// 不吞异常；下游异常仍按链契约经 <c>ExceptionCaught</c> 流向链尾。
    /// </para>
    /// </summary>
    public sealed class InboundLoggingHandler : InboundHandlerAdapter
    {
        /// <summary>记录连接标识、消息 CLR 类型名与下游耗时后原样下传。</summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="msg">待记录的入站消息，可为 null。</param>
        /// <returns>下游处理完成后结束的任务。</returns>
        public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
        {
            var watch = Stopwatch.StartNew();
            try
            {
                await ctx.FireChannelReadAsync(msg).ConfigureAwait(false);
            }
            finally
            {
                watch.Stop();
                LogHelper.Debug(
                    "入站消息:" + ctx.ChannelId + "," +
                    (msg == null ? "null" : msg.GetType().Name) + "," +
                    watch.ElapsedMilliseconds + "ms");
            }
        }
    }
}