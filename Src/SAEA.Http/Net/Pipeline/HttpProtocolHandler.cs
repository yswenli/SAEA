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
 *命名空间：SAEA.Http
 *文件名： HttpProtocolHandler
 *版本号： v26.11.5.1
 *唯一标识：825cf896-ecf5-485a-96d8-85209516ec86
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/9 13:19:30
 *描述：HTTP 协议边界处理器：升级协商、屏障切换与协议错误响应
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/9 15:32:00
 *修改人： yswenli
 *版本号： v26.11.5.1
 *描述：431 状态码改用数值转换，适配 netstandard2.0 缺少对应枚举成员
 *
 *****************************************************************************/
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using SAEA.Http.Base;
using SAEA.Http.Base.Net;
using SAEA.Sockets.Pipeline;

namespace SAEA.Http
{
    /// <summary>
    /// HTTP 协议边界处理器：处理同一条连接上的协议升级与分帧协议错误。
    /// <para>
    /// 升级请求由分帧器停切并置起屏障后送到这里。装配了外部升级目标时，业务接受后先写握手响应，
    /// 只有该写成功返回后才切换分帧器；未装配外部目标时，请求直接交给下游，由业务在自己的上下文里
    /// 调用响应升级，等下游返回后按会话是否已升级决定切换或复位屏障。
    /// 升级完成后的原始帧与业务自定义消息原样透传，不做二次解帧。
    /// </para>
    /// <para>
    /// 分帧器只抛固定 token 的 <see cref="FrameProtocolException"/>，413 / 431 / 400 响应在这里生成：
    /// 先写响应再经有界收尾关闭连接，且不再向链尾二次派发同一个协议异常。
    /// </para>
    /// </summary>
    public sealed class HttpProtocolHandler : InboundHandlerAdapter
    {
        private const string HttpVersion = "HTTP/1.1";
        private const string UpgradeHeader = "Upgrade";
        private const string WebSocketToken = "websocket";
        private const string PlainTextContentType = "text/plain; charset=utf-8";

        private readonly SwitchableSplitter _barrier;
        private readonly IFrameSplitter _upgradedSplitter;
        private readonly IHttpUpgradeTarget _upgradeTarget;

        /// <summary>
        /// 创建 HTTP 协议边界处理器；三个能力都可为 null，为 null 时对应能力关闭而不是抛异常。
        /// </summary>
        /// <param name="barrier">协议切换屏障，为 null 时不参与屏障复位与切换</param>
        /// <param name="upgradedSplitter">升级后生效的分帧器，为 null 时只写握手响应、不切换分帧器</param>
        /// <param name="upgradeTarget">升级判定入口，为 null 时一律按拒绝升级处理</param>
        public HttpProtocolHandler(
            SwitchableSplitter barrier,
            IFrameSplitter upgradedSplitter,
            IHttpUpgradeTarget upgradeTarget)
        {
            _barrier = barrier;
            _upgradedSplitter = upgradedSplitter;
            _upgradeTarget = upgradeTarget;
        }

        /// <summary>
        /// 处理入站消息：非 HTTP 消息与普通请求直接透传，升级请求交给升级目标协商。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="msg">待处理的入站消息。</param>
        /// <returns>下游处理完成后结束的任务。</returns>
        public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
        {
            var request = msg as HttpMessage;
            if (request == null)
            {
                // 升级后的原始帧或业务自定义消息：本处理器不再解释，原样交给下游。
                await ctx.FireChannelReadAsync(msg).ConfigureAwait(false);
                return;
            }

            string upgrade;
            if (!request.Headers.TryGetValue(UpgradeHeader, out upgrade)
                || string.IsNullOrEmpty(upgrade)
                || upgrade.IndexOf(WebSocketToken, StringComparison.OrdinalIgnoreCase) < 0)
            {
                await ctx.FireChannelReadAsync(request).ConfigureAwait(false);
                return;
            }

            var target = _upgradeTarget;
            if (target == null)
            {
                // 没有外部升级目标：请求交给下游，由业务在自己的上下文里决定是否升级。
                // 下游可能调用 Response.Upgrade 切换屏障，因此在它返回前不能复位，否则切换会因
                // 「没有等待切换的帧」失败；返回后按会话状态判定，未升级才复位继续按 HTTP 处理。
                try
                {
                    await ctx.FireChannelReadAsync(request).ConfigureAwait(false);
                }
                finally
                {
                    var session = HttpConnectionSession.TryGet(ctx.Channel);
                    if (session == null || !session.IsUpgraded)
                        ResetBarrier();
                }
                return;
            }

            var response = await target.TryUpgradeAsync(ctx, request).ConfigureAwait(false);
            if (response == null)
            {
                ResetBarrier();
                await ctx.FireChannelReadAsync(request).ConfigureAwait(false);
                return;
            }

            try
            {
                // 必须先等握手响应真正交接，再解除屏障：先切分帧器会让后续帧越过尚未完成的响应。
                await ctx.Pipeline.WriteAsync(response).ConfigureAwait(false);
            }
            catch
            {
                // 写失败不能切换协议：连接按失败收口，原异常继续向上冒泡。
                await ctx.CloseAfterWriteAsync("http-upgrade-write-failed").ConfigureAwait(false);
                throw;
            }

            // 写成功后才切换协议。有会话时走会话升级：先登记接收入口、再换分帧器并标记已升级，
            // 紧随 101 的原始字节才不会在下一次解码时被当成 HTTP 报文；接收入口为 null 时
            // 原始帧继续交给下游处理器，由装配方自行消费。
            var upgradeSession = HttpConnectionSession.TryGet(ctx.Channel);
            if (upgradeSession != null)
            {
                upgradeSession.Upgrade(null, null);
                return;
            }

            // 独立装配的链没有会话（例如只装了协议处理器的单测），退化为只切分帧器。
            CompleteUpgrade();
        }

        /// <summary>
        /// 协议错误收口：只处理分帧器抛出的协议错误并回写 413 / 431 / 400，其余异常继续向下游传播。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="ex">待处理的异常。</param>
        /// <returns>回写与收口完成后结束的任务。</returns>
        public override async Task ChannelExceptionCaughtAsync(IChannelHandlerContext ctx, Exception ex)
        {
            var protocolError = ex as FrameProtocolException;
            if (protocolError == null)
            {
                await ctx.FireExceptionCaughtAsync(ex).ConfigureAwait(false);
                return;
            }

            var status = MapStatus(protocolError.Message);
            var response = new HttpResponse
            {
                Protocal = HttpVersion,
                Status = status,
                ContentType = PlainTextContentType,
                Body = Encoding.UTF8.GetBytes(((int)status) + " " + status)
            };

            try
            {
                await ctx.Pipeline.WriteAsync(response).ConfigureAwait(false);
            }
            catch
            {
                // 先写后关：写本身失败时仍要走有界收口，写异常不能被吞掉。
                await ctx.CloseAfterWriteAsync("http-protocol-error").ConfigureAwait(false);
                throw;
            }

            // 错误响应的写已交接，这里收口；同一个协议异常不再向链尾二次派发。
            await ctx.CloseAfterWriteAsync("http-protocol-error").ConfigureAwait(false);
        }

        /// <summary>按分帧器写入的固定 token 映射响应状态码，未识别的一律 400。</summary>
        private static HttpStatusCode MapStatus(string token)
        {
            if (string.Equals(token, HttpProtocolErrorCodes.RequestEntityTooLarge, StringComparison.Ordinal))
                return HttpStatusCode.RequestEntityTooLarge;

            if (string.Equals(token, HttpProtocolErrorCodes.RequestHeaderFieldsTooLarge, StringComparison.Ordinal))
                // netstandard2.0 的 HttpStatusCode 没有 431 成员，用数值转换保留状态码。
                return (HttpStatusCode)431;

            return HttpStatusCode.BadRequest;
        }

        /// <summary>决定不升级时复位屏障，让接收泵继续按当前分帧器切帧。</summary>
        private void ResetBarrier()
        {
            var barrier = _barrier;
            if (barrier == null)
                return;

            barrier.SwitchTo(barrier.Current);
        }

        /// <summary>握手响应写成功后切到升级分帧器；缺少升级分帧器时不得解除屏障。</summary>
        private void CompleteUpgrade()
        {
            var barrier = _barrier;
            var upgraded = _upgradedSplitter;
            if (barrier == null || upgraded == null)
                return;

            barrier.SwitchTo(upgraded);
        }
    }
}