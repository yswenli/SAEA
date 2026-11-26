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
*修改时间：2026/11/26 10:00:00
*修改人： yswenli
*版本号： v26.11.25.1
*描述：静态文件结果写出前按请求 Range 头应用单区间范围
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
    /// 请求处理与路由解析管道：由 HttpContext 首次用到时创建并在同一连接内复用，承接参数合并、结果收尾与路由匹配
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
                var method = NormalizeMethod(_context.Request.Method);

                // 回写规范值：输出缓存的键与下游调用都拿请求方法当入参，统一成大写后不必在每处再比较一次大小写
                if (!string.IsNullOrEmpty(method) && !string.Equals(method, _context.Request.Method, StringComparison.Ordinal))
                {
                    _context.Request.Method = method;
                }

                // HEAD 复用 GET 的执行结果，但只回报头：先打标，正文由响应层统一丢弃，
                // 避免各类结果（普通结果、大数据、事件流）各自处理一遍
                if (HttpMethodCatalog.IsHead(method))
                {
                    _context.Response.SuppressBody = true;
                }

                // 路由前钩子：鉴权与入口重定向必须早于路由，才能同时覆盖接口、静态文件与事件流；
                // 返回非 null 表示钩子已接管本次请求，按普通结果走后续收尾
                result = await _context.OnRequestExecutingAsync().ConfigureAwait(false);

                if (result == null)
                {
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
                            // 未知方法用 501：505 是 HTTP 版本不支持，与「方法不认识」不是一回事
                            result = new ContentResult("不支持的请求方式", System.Net.HttpStatusCode.NotImplemented);
                            break;
                    }
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

                // 文件正文已在 SetCached 中装好，此时才能按完整长度计算单区间并切片
                if (result is IFileResult)
                {
                    _context.Request.Headers.TryGetValue("Range", out var rangeHeader);

                    _context.Response.ApplyRangeHeader(rangeHeader);
                }

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
        /// 取本请求的参数集合，没有参数时返回 null
        /// </summary>
        /// <returns>按请求参数新建的集合；请求无参数时为 null</returns>
        private NameValueCollection GetNameValues()
        {
            var parmas = _context.Request.Parmas;

            // 无参数请求不必建空集合：下游一律先判空再取值，null 与空集合的绑定结果一致；
            // Parmas 还可能被 JSON 正文整体替换成 null，这里一并按无参数处理
            if (parmas == null || parmas.Count == 0) return null;

            return parmas.ToNameValueCollection();
        }

        /// <summary>
        /// 把请求方法名规范成大写：HTTP 方法名区分大小写，但本框架的谓词判定一律忽略大小写，
        /// 统一在入口规范化可避免下游各处重复比较
        /// </summary>
        /// <param name="method">请求行里的方法名，可为 null</param>
        /// <returns>大写方法名；入参为 null 或空时原样返回</returns>
        private static string NormalizeMethod(string method)
        {
            if (string.IsNullOrEmpty(method)) return method;

            return method.ToUpperInvariant();
        }

        /// <summary>
        /// 按 "/" 切分请求路径，只取最后两段
        /// </summary>
        /// <param name="url">不含 query 的请求路径</param>
        /// <param name="count">非空分段数，与 string.Split('/') 去掉空段的计数一致</param>
        /// <param name="controllerName">倒数第二段；分段数不足两段时为 null</param>
        /// <param name="actionName">最后一段；没有分段时为 null</param>
        /// <remarks>
        /// 路由判定只用到总段数与最后两段，其余分段不必各建一个子串
        /// </remarks>
        private static void SplitLastTwoSegments(string url, out int count, out string controllerName, out string actionName)
        {
            count = 0;

            controllerName = null;

            actionName = null;

            if (string.IsNullOrEmpty(url)) return;

            var lastStart = -1;

            var lastLength = 0;

            var prevStart = -1;

            var prevLength = 0;

            var index = 0;

            while (index < url.Length)
            {
                var slash = url.IndexOf('/', index);

                var end = slash < 0 ? url.Length : slash;

                // 连续斜杠与首尾斜杠产生的空段按原语义丢弃
                if (end > index)
                {
                    count++;

                    prevStart = lastStart;

                    prevLength = lastLength;

                    lastStart = index;

                    lastLength = end - index;
                }

                if (slash < 0) break;

                index = slash + 1;
            }

            if (count == 0) return;

            actionName = url.Substring(lastStart, lastLength);

            if (count > 1) controllerName = url.Substring(prevStart, prevLength);
        }

        /// <summary>
        /// 处理 OPTIONS：声明了 [HttpOptions] 就执行该 action，未声明则回 204 并给出已声明的动词，
        /// 不执行普通 action；只有路径完全未命中时才回 404
        /// </summary>
        /// <returns>显式 action 的执行结果、204 空结果，或路径完全未命中时的 404</returns>
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

            SplitLastTwoSegments(_context.Request.Url, out var segmentCount, out var controllerSegment, out var actionSegment);

            Type controllerType = null;

            var actionName = string.Empty;

            if (segmentCount == 0)
            {
                // 根路径按默认路由的控制器与 action 判定可声明的动词
                if (_context.WebConfig != null && _context.WebConfig.DefaultRoute != null)
                {
                    var defaultName = _context.WebConfig.DefaultRoute.Name;

                    controllerType = _routeTable.FindController(defaultName);

                    actionName = _context.WebConfig.DefaultRoute.Value.ToString();
                }
            }
            else if (segmentCount > 1)
            {
                var controllerName = controllerSegment;

                controllerType = _routeTable.FindControllerBySuffix(controllerName);

                actionName = actionSegment;
            }

            if (controllerType != null)
            {
                // 交给 MvcInvoker 按 action 声明的动词作答：声明了 [HttpOptions] 就执行，否则 204 加 Allow
                return await MvcInvoker.InvokeResultAsync(_routeTable, controllerType, actionName, GetNameValues(), ConstHelper.OPTIONS).ConfigureAwait(false);
            }

            // 静态资源仍是真实存在的路径，OPTIONS 照旧自动应答 204；只有完全未命中的路径才回 404
            string filePath;

            if (segmentCount == 0)
            {
                // 根路径与 GetActionResultAsync 的路径形状保持一致：按默认首页映射，配置缺失时按未命中处理
                filePath = _context.WebConfig == null || _context.WebConfig.HomePage == null
                    ? null
                    : _context.Server.MapPath(_context.WebConfig.HomePage);
            }
            else
            {
                filePath = _context.Server.MapPath(_context.Request.Url);
            }

            if (filePath != null && StaticResourcesCache.Exists(filePath))
            {
                return CreateNoContent(null);
            }

            return new ContentResult("o_o，找不到任何内容 url:" + _context.Request.Url, System.Net.HttpStatusCode.NotFound);
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

            return await MvcInvoker.InvokeResultAsync(_routeTable, match.ControllerType, match.ActionName, GetNameValues(), method).ConfigureAwait(false);
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

            var method = NormalizeMethod(_context.Request.Method);

            // 该方法可被 HttpContext.GetActionResultAsync 直接调用，不能只依赖 HandleAsync 里的规范化
            if (!string.IsNullOrEmpty(method) && !string.Equals(method, _context.Request.Method, StringComparison.Ordinal))
            {
                _context.Request.Method = method;
            }

            // 空方法名仍按 GET 处理
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

            SplitLastTwoSegments(url, out var segmentCount, out var controllerSegment, out var actionSegment);

            var filePath = string.Empty;

            NameValueCollection nameValues;

            switch (segmentCount)
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

                        var d = _routeTable.FindController(_context.WebConfig.DefaultRoute.Name);

                        nameValues = GetNameValues();

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
                    var controllerName = controllerSegment;

                    var first = _routeTable.FindControllerBySuffix(controllerName);

                    if (first != null)
                    {
                        nameValues = GetNameValues();

                        return await MvcInvoker.InvokeResultAsync(_routeTable, first, actionSegment, nameValues, method).ConfigureAwait(false);
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