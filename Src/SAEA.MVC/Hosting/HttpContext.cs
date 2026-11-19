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
*命名空间：SAEA.MVC
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
*修改时间：2026/11/19 11:16:00
*修改人： yswenli
*版本号： v26.11.16.1
*描述：文件移入 Hosting/，补 Routing 与 WebSockets 子命名空间引用，无逻辑变更
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.NameValue;
using SAEA.Http;
using SAEA.Http.Base;
using SAEA.Http.Common;
using SAEA.Http.Model;
using SAEA.MVC.Routing;
using SAEA.MVC.WebSockets;
using SAEA.Sockets.Pipeline;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MVC
{
    /// <summary>
    /// MVC 请求上下文，负责路由到控制器并生成响应
    /// </summary>
    public class HttpContext : HttpContextBase, IHttpContext
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

            IHttpResult result = null;

            try
            {
                switch (this.Request.Method)
                {
                    case ConstHelper.GET:
                    case ConstHelper.POST:
                    case ConstHelper.PUT:
                    case ConstHelper.DELETE:

                        if (this.Request.Query.Count > 0)
                        {
                            foreach (var item in this.Request.Query)
                            {
                                this.Request.Parmas[item.Key] = item.Value;
                            }
                        }
                        if (this.Request.Forms.Count > 0)
                        {
                            foreach (var item in this.Request.Forms)
                            {
                                this.Request.Parmas[item.Key] = item.Value;
                            }
                        }
                        if (WebConfig != null && WebConfig.RequestDelegate != null)
                        {
                            WebConfig.RequestDelegate(this);
                        }
                        else
                        {
                            result = await GetActionResultAsync().ConfigureAwait(false);
                        }
                        break;
                    case ConstHelper.OPTIONS:
                        result = new EmptyResult();
                        break;
                    default:
                        result = new ContentResult("不支持的请求方式", System.Net.HttpStatusCode.HttpVersionNotSupported);
                        break;
                }
            }
            catch (Exception ex)
            {
                result = WebConfig == null || WebConfig.ExceptionHandler == null
                    ? null
                    : WebConfig.ExceptionHandler(this, ex);

                if (result == null)
                {
                    result = new ContentResult("请求发生异常：" + ex.Message, System.Net.HttpStatusCode.InternalServerError);
                }
            }

            // 已升级为 WebSocket 或已直接写过原始响应：不再走普通响应收尾
            if (!(result is IBigDataResult || result is IEventStream) && !Response.IsUpgraded && !Response.IsHandled)
            {
                // 非大数据结果和非事件流，设置缓存并结束连接
                Response.SetCached(result, this.Session.CacheCalcString);

                // 连接复用由请求协议与请求的 Connection 头决定：HTTP/1.1 默认保持，
                // HTTP/1.0 默认关闭；响应头与 Send/End 的连接副作用必须一致。
                var connection = Request.Headers.ContainsKey("Connection") ? Request.Headers["Connection"] : null;

                if (Response.ApplyConnectionPolicy(Request.Protocal, connection))
                {
                    // 发送响应但不关闭连接，同一连接可继续处理下一个请求
                    await Response.SendAsync().ConfigureAwait(false);
                }
                else
                {
                    await Response.EndAsync().ConfigureAwait(false);
                }
            }
            // IBigDataResult或IEventStream类型会在其他地方处理连接释放
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
            string url = Request.Url;

            bool isPost = Request.Method == ConstHelper.POST;

            //禁止访问
            var flist = WebConfig.ForbiddenAccessList;

            if (flist.Any())
            {
                foreach (var item in flist)
                {
                    if (url.IndexOf(item, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return new ContentResult("o_o，当前内容禁止访问！url:" + url, System.Net.HttpStatusCode.Forbidden);
                    }
                    if (System.Text.RegularExpressions.Regex.IsMatch(url, item, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        return new ContentResult("o_o，当前内容禁止访问！url:" + url, System.Net.HttpStatusCode.Forbidden);
                    }
                }
            }

            // 属性路由先于两段式约定解析，命中即执行，未声明属性路由时走原有逻辑
            if (_attributeRoutes != null)
            {
                var match = _attributeRoutes.Match(Request.RelativeUrl, Request.Method);

                if (match != null)
                {
                    if (match.IsFound)
                    {
                        if (match.Values != null)
                        {
                            foreach (var item in match.Values)
                            {
                                // 路径参数优先于同名 query 与表单值
                                Request.Parmas[item.Key] = item.Value;
                            }
                        }

                        RoutingPath = match.Path;

                        return await MvcInvoker.InvokeResultAsync(_routeTable, match.ControllerType, match.ActionName, Request.Parmas.ToNameValueCollection(), isPost).ConfigureAwait(false);
                    }

                    // 路径已被属性路由声明但请求动词不符：返回 405 并告知可用动词
                    Response.Headers["Allow"] = match.AllowHeader ?? string.Empty;

                    // 文案用原始 request-target：Request.Url 会被剥掉数字末段，路径会缺一截
                    var requestUrl = string.IsNullOrEmpty(Request.RelativeUrl) ? Request.Url : Request.RelativeUrl;

                    // 正文按 text/html 输出，requestUrl 含完整 query，编码后再拼进文案
                    return new ContentResult("o_o，当前请求方式不被允许 url:" + System.Net.WebUtility.HtmlEncode(requestUrl) + "，允许的方式：" + match.AllowHeader, System.Net.HttpStatusCode.MethodNotAllowed);
                }
            }

            var arr = url.Split("/", StringSplitOptions.RemoveEmptyEntries);

            var filePath = string.Empty;

            NameValueCollection nameValues;

            switch (arr.Length)
            {
                case 0:

                    filePath = Server.MapPath(WebConfig.HomePage);

                    if (filePath == null)
                    {
                        return new ContentResult("o_o，找不到任何内容 url:" + url, System.Net.HttpStatusCode.NotFound);
                    }

                    if (StaticResourcesCache.Exists(filePath))
                    {
                        if (StaticResourcesCache.IsBigFile(filePath))

                            return new BigDataResult(filePath);
                        else

                            return new FileResult(filePath, IsStaticsCached);
                    }
                    else
                    {
                        if (RouteTable.Types == null) return new ContentResult("o_o，找不到任何路由信息！url:" + url, System.Net.HttpStatusCode.NotFound);

                        var d = RouteTable.Types.Where(b => (string.Compare(b.Name, WebConfig.DefaultRoute.Name, true) == 0 || string.Compare(b.Name, WebConfig.DefaultRoute.Name + ConstHelper.CONTROLLERNAME, true) == 0)).FirstOrDefault();

                        nameValues = Request.Parmas.ToNameValueCollection();

                        return await MvcInvoker.InvokeResultAsync(_routeTable, d, WebConfig.DefaultRoute.Value.ToString(), nameValues, isPost).ConfigureAwait(false);
                    }

                case 1:
                    filePath = Server.MapPath(url);

                    if (filePath == null)
                    {
                        return new ContentResult("o_o，找不到任何内容 url:" + url, System.Net.HttpStatusCode.NotFound);
                    }

                    if (StaticResourcesCache.Exists(filePath))
                    {
                        if (StaticResourcesCache.IsBigFile(filePath))

                            return new BigDataResult(filePath);
                        else

                            return new FileResult(filePath, IsStaticsCached);
                    }
                    break;

                default:
                    var controllerName = arr[arr.Length - 2];

                    if (RouteTable.Types != null && RouteTable.Types.Any())
                    {
                        var first = RouteTable.Types.Where(b => string.Compare(b.Name, controllerName + ConstHelper.CONTROLLERNAME, true) == 0).FirstOrDefault();

                        if (first != null)
                        {
                            nameValues = Request.Parmas.ToNameValueCollection();

                            return await MvcInvoker.InvokeResultAsync(_routeTable, first, arr[arr.Length - 1], nameValues, isPost).ConfigureAwait(false);
                        }
                    }

                    filePath = Server.MapPath(url);

                    if (filePath == null)
                    {
                        return new ContentResult("o_o，找不到任何内容 url:" + url, System.Net.HttpStatusCode.NotFound);
                    }

                    if (StaticResourcesCache.Exists(filePath))
                    {
                        if (StaticResourcesCache.IsBigFile(filePath))

                            return new BigDataResult(filePath);
                        else

                            return new FileResult(filePath, IsStaticsCached);
                    }
                    break;
            }

            return new ContentResult("o_o，找不到任何内容 url:" + url, System.Net.HttpStatusCode.NotFound);
        }
    }
}