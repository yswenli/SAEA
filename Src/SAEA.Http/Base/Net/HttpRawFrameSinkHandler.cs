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
 *命名空间：SAEA.Http.Base.Net
 *文件名： HttpRawFrameSinkHandler
 *版本号： v26.11.5.1
 *唯一标识：c8a1d0f7-5e2b-4c93-8a61-1d3f9b7c2e58
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/10 9:30:00
 *描述：升级后原始字节接收处理器：把无边界帧复制成独立数组交给 WebSocket 连接
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/10 9:30:00
 *修改人： yswenli
 *版本号： v26.11.5.1
 *描述：新增，替代 HttpCoder 的原始字节回调
 *
 *****************************************************************************/
using System;
using System.Threading.Tasks;

using SAEA.Common;
using SAEA.Sockets.Pipeline;

namespace SAEA.Http.Base.Net
{
    /// <summary>
    /// 升级后原始字节接收处理器：把无边界分帧产出的帧复制成独立数组，再交给会话登记的接收入口。
    /// <para>
    /// 帧内存在池化缓冲上，跨回调交给业务前必须复制；复制后立即释放本帧，避免长期占用池。
    /// 未登记接收入口时原样交给下游，保持未升级连接的行为不变。
    /// </para>
    /// </summary>
    internal sealed class HttpRawFrameSinkHandler : InboundHandlerAdapter
    {
        /// <summary>
        /// 处理一条入站消息。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="msg">待处理的入站消息。</param>
        /// <returns>下游处理完成后结束的任务。</returns>
        public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
        {
            var frame = msg as PooledMessage;

            if (frame != null)
            {
                var session = HttpConnectionSession.TryGet(ctx.Channel);
                var sink = session == null ? null : session.RawSink;

                if (sink != null)
                {
                    var errorSink = session.RawErrorSink;

                    byte[] data;

                    using (frame)
                    {
                        data = frame.Frame.ToArray();
                    }

                    try
                    {
                        sink(data);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error("WebSocket 原始字节处理失败:" + ctx.ChannelId, ex);

                        if (errorSink != null)
                        {
                            try
                            {
                                errorSink(ex);
                            }
                            catch (Exception notifyError)
                            {
                                LogHelper.Error("WebSocket 原始字节异常通知失败:" + ctx.ChannelId, notifyError);
                            }
                        }
                    }

                    return;
                }
            }

            await ctx.FireChannelReadAsync(msg).ConfigureAwait(false);
        }
    }
}