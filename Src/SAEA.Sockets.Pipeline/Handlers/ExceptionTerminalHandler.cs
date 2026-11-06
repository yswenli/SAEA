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
*文件名： ExceptionTerminalHandler
*版本号： v26.11.5.1
*唯一标识：a83f843b-9fa7-4e5d-96b4-1de6af4232ec
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：入站方向链尾：异常按类型决定记录与是否断开，未被任何处理器消费的消息在这里回收
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：入站方向链尾：异常按类型决定记录与是否断开，未被任何处理器消费的消息在这里回收
*
*****************************************************************************/
using System;
using System.Threading.Tasks;
using SAEA.Common;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 入站方向链尾：异常按类型决定记录与是否断开，未被任何处理器消费的消息在这里回收。
    /// <para>
    /// 异常分支都不再向下传（本处理器就是链尾）：单消息超时与"收口中仍尝试回包"
    /// 只记录不断连；其它异常记录后关闭连接。消息分支只做 Debug + 释放，
    /// 不断连接——未命中路由的告警已由 <see cref="MessageDispatcher"/> 发出。
    /// </para>
    /// </summary>
    public sealed class ExceptionTerminalHandler : InboundHandlerAdapter
    {
        /// <summary>普通处理器异常导致断连时使用的关闭原因。</summary>
        public const string HandlerExceptionReason = "handler-exception";

        /// <summary>消息一路透传到链尾仍无人消费：记录 Debug，实现 <see cref="IDisposable"/> 时释放，不关连接。</summary>
        public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
        {
            LogHelper.Debug(
                "消息未被消费，链尾回收:" + ctx.ChannelId + "," +
                (msg == null ? "null" : msg.GetType().Name));

            var disposable = msg as IDisposable;
            if (disposable != null)
                disposable.Dispose();

            return Task.CompletedTask;
        }

        /// <summary>
        /// 单消息超时与连接已关闭只记录；其它异常记录为 Error 并请求关闭连接，
        /// 关闭自身抛出的 <see cref="ChannelClosedException"/> 同样吞掉。
        /// </summary>
        public override async Task ChannelExceptionCaughtAsync(IChannelHandlerContext ctx, Exception ex)
        {
            if (ex is MessageTimeoutException)
            {
                LogHelper.Warn("消息处理超时:" + ctx.ChannelId, ex);
                return;
            }

            if (ex is ChannelClosedException)
            {
                LogHelper.Debug("连接已关闭，放弃异常上报:" + ctx.ChannelId + "," + ex.Message);
                return;
            }

            LogHelper.Error("处理器异常:" + ctx.ChannelId, ex);

            try
            {
                await ctx.Channel.CloseAsync(HandlerExceptionReason).ConfigureAwait(false);
            }
            catch (ChannelClosedException)
            {
            }
        }
    }
}