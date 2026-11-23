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
*修改时间：2026/11/20 15:18:00
*修改人： yswenli
*版本号： v26.11.16.1
*描述：请求管道改为首次用到时创建一次并在整条连接内复用，不再每请求新建一个管道对象
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

        // 管道只持有本上下文的三个只读引用、没有请求内状态，同一连接内创建一次即可复用
        RequestPipeline _pipeline = null;

        /// <summary>
        /// 本次上下文的请求管道，首次使用时创建，之后整条连接复用
        /// </summary>
        private RequestPipeline Pipeline
        {
            get
            {
                if (_pipeline == null)
                    _pipeline = new RequestPipeline(this, _routeTable, _attributeRoutes);

                return _pipeline;
            }
        }

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

            await Pipeline.HandleAsync().ConfigureAwait(false);
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
            return await Pipeline.GetActionResultAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// 路由前的请求钩子：返回非 null 表示由钩子接管本次请求，不再解析路由。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 鉴权、入口重定向这类判定必须早于路由，才能同时覆盖接口、静态文件与事件流；
        /// 默认返回 null，交给请求管道继续正常路由。钩子被调用时 Request、Session 与 Response
        /// 已完成初始化，但表单与 JSON 参数尚未合并，依赖参数的判定请改用过滤器。
        /// </para>
        /// <para>
        /// 请求管道实际调用的是 <see cref="OnRequestExecutingAsync"/>，它默认转调本方法；
        /// 判定里需要 I/O 时覆写异步版本即可，本方法不必再动。
        /// </para>
        /// <para>
        /// 上下文实例会在同一连接的后续请求中复用：这里不得把请求级状态留在字段上，
        /// 确有需要请在 <see cref="ResetForNextRequest"/> 中清理。
        /// 钩子返回的结果按普通结果收尾，会经过缓存头与连接复用策略；钩子抛出的异常由管道统一转成 500。
        /// </para>
        /// </remarks>
        /// <returns>接管结果；返回 null 时继续正常路由。</returns>
        public virtual IHttpResult OnRequestExecuting()
        {
            return null;
        }

        /// <summary>
        /// 路由前请求钩子的异步版本：返回非 null 表示由钩子接管本次请求，不再解析路由。
        /// </summary>
        /// <remarks>
        /// 请求管道调用本方法；默认实现转调 <see cref="OnRequestExecuting"/>，未覆写异步版本的上下文行为不变。
        /// 同时覆写两者时只有本方法会被调用，需要保留同步版本的逻辑请自行调用
        /// <see cref="OnRequestExecuting"/> 或 <c>base.OnRequestExecutingAsync()</c>。
        /// </remarks>
        /// <returns>接管结果；返回 null 时继续正常路由。</returns>
        public virtual Task<IHttpResult> OnRequestExecutingAsync()
        {
            return Task.FromResult(OnRequestExecuting());
        }
    }
}