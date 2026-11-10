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
 *文件名： HttpDispatchHandler
 *版本号： v26.11.5.1
 *唯一标识：3f5e6d2a-1c4b-4a7e-9b8d-7a2c5e9f0d41
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/10 9:20:00
 *描述：HTTP 请求派发处理器：为每条连接建立会话并把解析后的请求交给业务上下文
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/10 9:20:00
 *修改人： yswenli
 *版本号： v26.11.5.1
 *描述：新增，替代原 HttpSocket 的请求事件分发
 *
 *****************************************************************************/
using System;
using System.Threading.Tasks;

using SAEA.Common;
using SAEA.Http.Model;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;

namespace SAEA.Http.Base.Net
{
    /// <summary>
    /// HTTP 请求派发处理器：连接激活时建立会话并登记，激活期间把每条请求交给业务上下文，失活时摘除登记。
    /// <para>
    /// 每个连接独占一个实例；会话在该处理器的 <see cref="ChannelActiveAsync"/> 中创建，
    /// 因为此时才能安全读取 <see cref="IChannelHandlerContext.Channel"/>。
    /// </para>
    /// </summary>
    internal sealed class HttpDispatchHandler : InboundHandlerAdapter
    {
        private readonly WebHost _webHost;

        private readonly ChannelInfo _info;

        private readonly SwitchableSplitter _barrier;

        private readonly IFrameSplitter _upgradedSplitter;

        private readonly bool _isDebug;

        private HttpConnectionSession _session;

        /// <summary>
        /// 创建派发处理器。
        /// </summary>
        /// <param name="webHost">所属 Web 主机</param>
        /// <param name="info">底层通道信息，可为 null</param>
        /// <param name="barrier">协议切换屏障，可为 null</param>
        /// <param name="upgradedSplitter">升级后生效的分帧器，可为 null</param>
        /// <param name="isDebug">是否输出调试日志</param>
        internal HttpDispatchHandler(
            WebHost webHost,
            ChannelInfo info,
            SwitchableSplitter barrier,
            IFrameSplitter upgradedSplitter,
            bool isDebug)
        {
            _webHost = webHost;
            _info = info;
            _barrier = barrier;
            _upgradedSplitter = upgradedSplitter;
            _isDebug = isDebug;
        }

        /// <summary>
        /// 连接激活：创建会话并登记，供链上其它处理器与主机按通道取回。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <returns>已完成的任务。</returns>
        public override Task ChannelActiveAsync(IChannelHandlerContext ctx)
        {
            var channel = ctx.Channel;

            var session = new HttpConnectionSession(channel, _info, ctx.Pipeline, _barrier, _upgradedSplitter);

            _session = session;

            channel.SetAttribute(HttpConnectionSession.AttributeKey, session);

            return Task.CompletedTask;
        }

        /// <summary>
        /// 连接失活：摘除会话登记并通知连接关闭。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <returns>已完成的任务。</returns>
        public override Task ChannelInactiveAsync(IChannelHandlerContext ctx)
        {
            var session = _session;
            _session = null;

            var channelId = session == null ? ctx.ChannelId : session.Channel.Id;

            var closed = _webHost.WebConfig == null ? null : _webHost.WebConfig.ConnectionClosed;
            if (closed != null)
            {
                try
                {
                    closed(channelId, null);
                }
                catch (Exception ex)
                {
                    LogHelper.Debug("连接关闭通知失败:" + channelId + "," + ex.Message);
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 分派一条请求；非请求消息原样交给下游。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="msg">待处理的入站消息。</param>
        /// <returns>业务处理完成后结束的任务。</returns>
        public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
        {
            var request = msg as HttpMessage;
            if (request == null)
            {
                await ctx.FireChannelReadAsync(msg).ConfigureAwait(false);
                return;
            }

            if (_isDebug)
                LogHelper.Debug("HttpSocket.Recieve", request.Body);

            IHttpContext httpContext;

            try
            {
                httpContext = _webHost.CreateContext();
            }
            catch (Exception ex)
            {
                LogHelper.Error("创建 HttpContext 失败:" + ctx.ChannelId, ex);
                throw;
            }

            try
            {
                httpContext.HttpHandle(ctx.Channel, request);
            }
            catch (Exception ex)
            {
                LogHelper.Error("HTTP 请求处理失败:" + ctx.ChannelId, ex);
                throw;
            }
            finally
            {
                // 已升级的上下文由 WebSocket 连接负责释放（Dispose 只清 Request/Response）。
                if (!httpContext.Response.IsUpgraded)
                    httpContext.Dispose();
            }
        }
    }
}