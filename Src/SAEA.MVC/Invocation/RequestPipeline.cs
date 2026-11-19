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
*命名空间：SAEA.MVC.Invocation
*文件名： RequestPipeline
*版本号： v26.11.16.1
*唯一标识：874a1c79-cd88-4f92-8dd2-c962b7b4cb22
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 14:09:19
*描述：请求处理与路由解析管道
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 16:20:31
*修改人： yswenli
*版本号： v26.11.16.1
*描述：放行 PUT/DELETE/PATCH/HEAD，HEAD 打标抑制正文，OPTIONS 由独立分支按声明作答；路由调用改为传真实请求方法
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.NameValue;
using SAEA.Http.Common;
using SAEA.Http.Model;
using SAEA.MVC.Attributes;
using SAEA.MVC.Hosting;
using SAEA.MVC.Results;
using SAEA.MVC.Routing;

using System;
using System.Linq;
using System.Threading.Tasks;

namespace SAEA.MVC.Invocation
{
    /// <summary>
    /// 请求处理与路由解析管道：每次请求由 HttpContext 创建，承接参数合并、结果收尾与路由匹配
    /// </summary>
    internal sealed class RequestPipeline
    {
        readonly SAEA.MVC.Hosting.HttpContext _context;

        readonly RouteTable _routeTable;

        readonly AttributeRouteTable _attributeRoutes;

        /// <summary>
        /// 绑定本次请求的上下文与路由表
        /// </summary>
        /// <param name="context">本次请求的 MVC 上下文</param>
        /// <param name="routeTable">两段式路由表</param>
        /// <param name="attributeRoutes">属性路由表，未启用时为 null</param>
        internal RequestPipeline(SAEA.MVC.Hosting.HttpContext context, RouteTable routeTable, AttributeRouteTable attributeRoutes)
        {
            _context = context;
            _routeTable = routeTable;
            _attributeRoutes = attributeRoutes;
        }

        /// <summary>
        /// 处理业务逻辑
        /// </summary>
        /// <returns>响应写出完成后的任务</returns>
        internal async Task HandleAsync()
        {
            IHttpResult result = null;

            try
            {
                var method = _context.Request.Method;

                // HEAD 复用 GET 的执行结果，但只回报头：先打标，正文由响应层统一丢弃，
                // 避免各类结果（普通结果、大数据、事件流）各自处理一遍
                if (HttpMethodCatalog.IsHead(method))
                {
                    _context.Response.SuppressBody = true;
                }

                switch (method)
                {
                    case ConstHelper.GET:
                    case ConstHelper.HEAD:
                    case ConstHelper.POST:
                    case ConstHelper.PUT:
                    case ConstHelper.DELETE:
                    case ConstHelper.PATCH:

                        MergeParameters();

                        if (_context.WebConfig != null && _context.WebConfig.RequestDelegate != null)
                        {
                            _context.WebConfig.RequestDelegate(_context);
                        }
                        else
                        {
                            result = await GetActionResultAsync().ConfigureAwait(false);
                        }
                        break;
                    case ConstHelper.OPTIONS:
                        result = await HandleOptionsAsync().ConfigureAwait(false);
                        break;
                    default:
                        result = new ContentResult("不支持的请求方式", System.Net.HttpStatusCode.HttpVersionNotSupported);
                        break;
                }
            }
            catch (Exception ex)
            {
                if (_context.IsResponseStarted)
                {
                    // 报头与部分正文已经发出，无法再改状态码；直接断开连接，
                    // 避免在流式响应后面再写一条完整的 500
                    await _context.Response.SendEndAsync().ConfigureAwait(false);

                    return;
                }

                result = _context.WebConfig == null || _context.WebConfig.ExceptionHandler == null
                    ? null
                    : _context.WebConfig.ExceptionHandler(_context, ex);

                if (result == null)
                {
                    result = new ContentResult("请求发生异常：" + ex.Message, System.Net.HttpStatusCode.InternalServerError);
                }
            }

            // delegate 自行写出响应或没有可写内容时不产生 result：交给 End 收尾，重复调用在 Channel 置空后自然失效
            if (result == null)
            {
                await _context.Response.EndAsync().ConfigureAwait(false);
            }
            // 已升级为 WebSocket 或已直接写过原始响应：不再走普通响应收尾
            else if (!(result is IBigDataResult || result is IEventStream) && !_context.Response.IsUpgraded && !_context.Response.IsHandled)
            {
                // 非大数据结果和非事件流，设置缓存并结束连接
                _context.Response.SetCached(result, _context.Session.CacheCalcString);

                // 连接复用由请求协议与请求的 Connection 头决定：HTTP/1.1 默认保持，
                // HTTP/1.0 默认关闭；响应头与 Send/End 的连接副作用必须一致。
                var connection = _context.Request.Headers.ContainsKey("Connection") ? _context.Request.Headers["Connection"] : null;

                if (_context.Response.ApplyConnectionPolicy(_context.Request.Protocal, connection))
                {
                    // 发送响应但不关闭连接，同一连接可继续处理下一个请求
                    await _context.Response.SendAsync().ConfigureAwait(false);
                }
                else
                {
                    await _context.Response.EndAsync().ConfigureAwait(false);
                }
            }
            // IBigDataResult或IEventStream类型会在其他地方处理连接释放
        }

        /// <summary>
        /// 把 query 与表单值并入本次请求的参数集合，同名时表单值覆盖 query
        /// </summary>
        private void MergeParameters()
        {
            if (_context.Request.Query.Count > 0)
            {
                foreach (var item in _context.Request.Query)
                {
                    _context.Request.Parmas[item.Key] = item.Value;
                }
            }

            if (_context.Request.Forms.Count > 0)
            {
                foreach (var item in _context.Request.Forms)
                {
                    _context.Request.Parmas[item.Key] = item.Value;
                }
            }
        }

        /// <summary>
        /// 处理 OPTIONS：声明了 [HttpOptions] 就执行该 action，未声明则回 204 并给出已声明的动词，
        /// 不执行普通 action
        /// </summary>
        /// <returns>显式 action 的执行结果或 204 空结果</returns>
        private async Task<IHttpResult> HandleOptionsAsync()
        {
            MergeParameters();

            // 属性路由优先：命中即按条目声明的动词作答
            if (_attributeRoutes != null)
            {
                var match = _attributeRoutes.Match(_context.Request.RelativeUrl, ConstHelper.OPTIONS);

                if (match != null)
                {
                    if (match.IsFound) return await InvokeAttributeAsync(match, ConstHelper.OPTIONS).ConfigureAwait(false);

                    return CreateNoContent(match.AllowHeader);
                }
            }

            var arr = _context.Request.Url.Split("/", StringSplitOptions.RemoveEmptyEntries);

            Type controllerType = null;

            var actionName = string.Empty;

            if (arr.Length == 0)
            {
                // 根路径按默认路由的控制器与 action 判定可声明的动词
                if (_context.WebConfig != null && _context.WebConfig.DefaultRoute != null)
                {
                    var defaultName = _context.WebConfig.DefaultRoute.Name;

                    if (RouteTable.Types != null)
                    {
                        controllerType = RouteTable.Types.Where(b => string.Compare(b.Name, defaultName, true) == 0 || string.Compare(b.Name, defaultName + ConstHelper.CONTROLLERNAME, true) == 0).FirstOrDefault();
                    }

                    actionName = _context.WebConfig.DefaultRoute.Value.ToString();
                }
            }
            else if (arr.Length > 1)
            {
                var controllerName = arr[arr.Length - 2];

                if (RouteTable.Types != null)
                {
                    controllerType = RouteTable.Types.Where(b => string.Compare(b.Name, controllerName + ConstHelper.CONTROLLERNAME, true) == 0).FirstOrDefault();
                }

                actionName = arr[arr.Length - 1];
            }

            if (controllerType != null)
            {
                // 交给 MvcInvoker 按 action 声明的动词作答：声明了 [HttpOptions] 就执行，否则 204 加 Allow
                return await MvcInvoker.InvokeResultAsync(_routeTable, controllerType, actionName, _context.Request.Parmas.ToNameValueCollection(), ConstHelper.OPTIONS).ConfigureAwait(false);
            }

            // 静态资源与未知路径不做内容协商，直接自动应答；没有可列出的动词时不写 Allow
            return CreateNoContent(null);
        }

        /// <summary>
        /// 执行属性路由命中的 action，并把路径参数并入本次请求
        /// </summary>
        /// <param name="match">命中的属性路由结果</param>
        /// <param name="method">本次请求方法，决定 action 条目的动词键</param>
        /// <returns>action 执行结果</returns>
        private async Task<IHttpResult> InvokeAttributeAsync(AttributeRouteTable.MatchResult match, string method)
        {
            if (match.Values != null)
            {
                foreach (var item in match.Values)
                {
                    // 路径参数优先于同名 query 与表单值
                    _context.Request.Parmas[item.Key] = item.Value;
                }
            }

            _context.RoutingPath = match.Path;

            return await MvcInvoker.InvokeResultAsync(_routeTable, match.ControllerType, match.ActionName, _context.Request.Parmas.ToNameValueCollection(), method).ConfigureAwait(false);
        }

        /// <summary>
        /// 生成 OPTIONS 的自动应答：204 无正文，有可声明的动词时才写 Allow
        /// </summary>
        /// <param name="allow">Allow 报头内容，为空表示不写该报头</param>
        /// <returns>204 空结果</returns>
        private IHttpResult CreateNoContent(string allow)
        {
            if (!string.IsNullOrEmpty(allow))
            {
                _context.Response.Headers["Allow"] = allow;
            }

            return new EmptyResult { Status = System.Net.HttpStatusCode.NoContent };
        }

        /// <summary>
        /// 异步根据地址路由到目标并返回执行结果
        /// </summary>
        /// <returns>业务处理结果</returns>
        internal async Task<IHttpResult> GetActionResultAsync()
        {
            string url = _context.Request.Url;

            var method = _context.Request.Method;

            if (string.IsNullOrEmpty(method)) method = ConstHelper.GET;

            //禁止访问
            var flist = _context.WebConfig.ForbiddenAccessList;

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
                var match = _attributeRoutes.Match(_context.Request.RelativeUrl, method);

                if (match != null)
                {
                    if (match.IsFound)
                    {
                        return await InvokeAttributeAsync(match, method).ConfigureAwait(false);
                    }

                    // OPTIONS 由框架接管：不执行普通 action，只回 204 与路径上已声明的动词
                    if (string.Compare(method, ConstHelper.OPTIONS, true) == 0)
                    {
                        return CreateNoContent(match.AllowHeader);
                    }

                    // 路径已被属性路由声明但请求动词不符：返回 405 并告知可用动词；没有可列出的动词时不写空的 Allow
                    if (!string.IsNullOrEmpty(match.AllowHeader))
                    {
                        _context.Response.Headers["Allow"] = match.AllowHeader;
                    }

                    // 文案用原始 request-target：Request.Url 会被剥掉数字末段，路径会缺一截
                    var requestUrl = string.IsNullOrEmpty(_context.Request.RelativeUrl) ? _context.Request.Url : _context.Request.RelativeUrl;

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

                    filePath = _context.Server.MapPath(_context.WebConfig.HomePage);

                    if (filePath == null)
                    {
                        return new ContentResult("o_o，找不到任何内容 url:" + url, System.Net.HttpStatusCode.NotFound);
                    }

                    if (StaticResourcesCache.Exists(filePath))
                    {
                        if (StaticResourcesCache.IsBigFile(filePath))

                            return new BigDataResult(filePath);
                        else

                            return new FileResult(filePath, _context.IsStaticsCached);
                    }
                    else
                    {
                        if (RouteTable.Types == null) return new ContentResult("o_o，找不到任何路由信息！url:" + url, System.Net.HttpStatusCode.NotFound);

                        var d = RouteTable.Types.Where(b => (string.Compare(b.Name, _context.WebConfig.DefaultRoute.Name, true) == 0 || string.Compare(b.Name, _context.WebConfig.DefaultRoute.Name + ConstHelper.CONTROLLERNAME, true) == 0)).FirstOrDefault();

                        nameValues = _context.Request.Parmas.ToNameValueCollection();

                        return await MvcInvoker.InvokeResultAsync(_routeTable, d, _context.WebConfig.DefaultRoute.Value.ToString(), nameValues, method).ConfigureAwait(false);
                    }

                case 1:
                    filePath = _context.Server.MapPath(url);

                    if (filePath == null)
                    {
                        return new ContentResult("o_o，找不到任何内容 url:" + url, System.Net.HttpStatusCode.NotFound);
                    }

                    if (StaticResourcesCache.Exists(filePath))
                    {
                        if (StaticResourcesCache.IsBigFile(filePath))

                            return new BigDataResult(filePath);
                        else

                            return new FileResult(filePath, _context.IsStaticsCached);
                    }
                    break;

                default:
                    var controllerName = arr[arr.Length - 2];

                    if (RouteTable.Types != null && RouteTable.Types.Any())
                    {
                        var first = RouteTable.Types.Where(b => string.Compare(b.Name, controllerName + ConstHelper.CONTROLLERNAME, true) == 0).FirstOrDefault();

                        if (first != null)
                        {
                            nameValues = _context.Request.Parmas.ToNameValueCollection();

                            return await MvcInvoker.InvokeResultAsync(_routeTable, first, arr[arr.Length - 1], nameValues, method).ConfigureAwait(false);
                        }
                    }

                    filePath = _context.Server.MapPath(url);

                    if (filePath == null)
                    {
                        return new ContentResult("o_o，找不到任何内容 url:" + url, System.Net.HttpStatusCode.NotFound);
                    }

                    if (StaticResourcesCache.Exists(filePath))
                    {
                        if (StaticResourcesCache.IsBigFile(filePath))

                            return new BigDataResult(filePath);
                        else

                            return new FileResult(filePath, _context.IsStaticsCached);
                    }
                    break;
            }

            return new ContentResult("o_o，找不到任何内容 url:" + url, System.Net.HttpStatusCode.NotFound);
        }
    }
}