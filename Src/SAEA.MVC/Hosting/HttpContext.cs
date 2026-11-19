/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MVC.Hosting
*文件名： HttpContext
*版本号： v26.4.23.1
*唯一标识：846146d0-64fe-4ac1-81ae-814507b0b78f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/04/21 23:58:08
*描述：HttpContext上下文类
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 20:10:00
*修改人： yswenli
*版本号： v26.11.16.1
*描述：声明为可复用上下文并实现 ResetForNextRequest，复位路由路径、取消令牌、响应开始标记与 WebSocket 能力入口
*
*****************************************************************************/
using SAEA.Http;
using SAEA.Http.Base;
using SAEA.Http.Model;
using SAEA.MVC.Invocation;
using SAEA.MVC.Routing;
using SAEA.MVC.WebSockets;
using SAEA.Sockets.Pipeline;

using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MVC.Hosting
{
    /// <summary>
    /// MVC 请求上下文，负责路由到控制器并生成响应
    /// </summary>
    public class HttpContext : HttpContextBase, IHttpContext, IReusableHttpContext
    {
        RouteTable _routeTable = null;

        AttributeRouteTable _attributeRoutes = null;

        /// <summary>
        /// 以宿主服务创建上下文并绑定其路由表
        /// </summary>
        /// <param name="webHost">Web 宿主</param>
        public HttpContext(IWebHost webHost) : base(webHost)
        {
            _routeTable = _webHost.RouteParam as RouteTable;

            _attributeRoutes = AttributeRouteTable.From(_routeTable);
        }

        /// <summary>
        /// 本次属性路由命中的归一化实际路径，未命中属性路由时为 null
        /// </summary>
        public string RoutingPath { get; internal set; }

        /// <summary>
        /// 本次 action 的取消令牌，超时后转为已取消；只有观察令牌的代码才会停止。
        /// 令牌只在本次 action 调用期间有效，不要在 action 返回后继续注册回调或交给后台任务长期持有，
        /// action 返回后取消源即释放；未进入 action 的请求（404、405、静态文件、RequestDelegate）
        /// 读到的是不可取消的默认令牌
        /// </summary>
        public CancellationToken RequestAborted { get; internal set; }

        /// <summary>
        /// 报头是否已经写出；流式结果（大数据、SSE）写出报头后置为 true，
        /// 此后发生异常不能再改状态码重写响应
        /// </summary>
        internal bool IsResponseStarted { get; private set; }

        /// <summary>
        /// 标记报头已写出
        /// </summary>
        internal void MarkResponseStarted()
        {
            IsResponseStarted = true;
        }

        WebSocketsFeature _webSockets;

        /// <summary>
        /// MVC 的 WebSocket 能力入口
        /// </summary>
        public override IWebSocketsFeature WebSockets
        {
            get
            {
                if (_webSockets == null)
                    _webSockets = new WebSocketsFeature(this);

                return _webSockets;
            }
        }

        /// <summary>
        /// 处理业务逻辑
        /// </summary>
        /// <param name="channel">连接通道</param>
        /// <param name="httpMessage">Http消息</param>
        public override void HttpHandle(IChannel channel, HttpMessage httpMessage)
        {
            HttpHandleAsync(channel, httpMessage).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 异步处理业务逻辑：等待 action 完成，并等待响应真正写出
        /// </summary>
        /// <param name="channel">连接通道</param>
        /// <param name="httpMessage">Http消息</param>
        /// <returns>响应写出完成后的任务</returns>
        public override async Task HttpHandleAsync(IChannel channel, HttpMessage httpMessage)
        {
            InitContext(channel, httpMessage);

            await new RequestPipeline(this, _routeTable, _attributeRoutes).HandleAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// 复位上下文供本连接的下一条请求复用：清掉上一次请求的路由命中、取消令牌与响应开始标记，
        /// 并丢弃缓存了控制器与待处理字节的 WebSocket 能力入口。
        /// </summary>
        public override void ResetForNextRequest()
        {
            base.ResetForNextRequest();

            RoutingPath = null;

            RequestAborted = default(CancellationToken);

            IsResponseStarted = false;

            // 能力入口会记住本次请求的 WSController 与早到字节，复用上下文时不能带进下一次请求，按需重建
            _webSockets = null;
        }

        /// <summary>
        /// 根据地址路由到目标并返回执行结果
        /// </summary>
        /// <returns></returns>
        public override IHttpResult GetActionResult()
        {
            return GetActionResultAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// 异步根据地址路由到目标并返回执行结果
        /// </summary>
        /// <returns>业务处理结果</returns>
        public override async Task<IHttpResult> GetActionResultAsync()
        {
            return await new RequestPipeline(this, _routeTable, _attributeRoutes).GetActionResultAsync().ConfigureAwait(false);
        }
    }
}