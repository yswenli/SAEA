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
*文件名： MvcInvoker
*版本号： v26.4.23.1
*唯一标识：df2222b1-5599-42e6-b920-b28f86849d31
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/28 14:19:50
*描述：MVC 实例方法映射处理
*
*=====================================================================
*修改标记
*修改时间：2026/11/20 14:59:51
*修改人： yswenli
*版本号： v26.11.16.1
*描述：WebSocket 能力入口改为仅在命中 WSController 时按需物化，普通请求不再每请求新建入口对象
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.NameValue;
using SAEA.Common.Serialization;
using SAEA.MVC.Attributes;
using SAEA.MVC.Controllers;
using SAEA.MVC.Invocation;
using SAEA.MVC.Results;
using SAEA.MVC.Routing;
using SAEA.MVC.WebSockets;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace SAEA.MVC.Hosting
{
    /// <summary>
    /// saea.mvc实例方法映射处理类
    /// </summary>
    public static class MvcInvoker
    {
        // WebSocket 特性判定与过滤器方法查找都属于 action/过滤器类型的固定元数据，
        // 反射结果只读，按类型缓存后每请求不再重复查特性与查找方法
        static readonly ConcurrentDictionary<MethodInfo, bool> _webSocketActionCache = new ConcurrentDictionary<MethodInfo, bool>();

        static readonly ConcurrentDictionary<Type, MethodInfo> _onActionExecutingCache = new ConcurrentDictionary<Type, MethodInfo>();

        static readonly ConcurrentDictionary<Type, MethodInfo> _onActionExecutedCache = new ConcurrentDictionary<Type, MethodInfo>();

        /// <summary>
        /// 判断 action 是否声明了 [WebSocket]，同一 action 只查一次特性
        /// </summary>
        /// <param name="action">目标 action</param>
        /// <returns>声明了 WebSocketAttribute 时返回 true</returns>
        static bool IsWebSocketAction(MethodInfo action)
        {
            if (action == null) return false;

            if (_webSocketActionCache.TryGetValue(action, out var cached)) return cached;

            var isWebSocket = action.GetCustomAttributes(typeof(WebSocketAttribute), true).Length > 0;

            _webSocketActionCache.TryAdd(action, isWebSocket);

            return isWebSocket;
        }

        /// <summary>
        /// 取过滤器类型的 OnActionExecuting 方法，同一类型只查找一次
        /// </summary>
        /// <param name="filterType">过滤器实例的类型</param>
        /// <returns>方法元数据；未定义该方法时为 null</returns>
        static MethodInfo GetOnActionExecutingMethod(Type filterType)
        {
            if (_onActionExecutingCache.TryGetValue(filterType, out var cached)) return cached;

            var method = filterType.GetMethod(ConstHelper.ONACTIONEXECUTING);

            _onActionExecutingCache.TryAdd(filterType, method);

            return method;
        }

        /// <summary>
        /// 取过滤器类型的 OnActionExecuted 方法，同一类型只查找一次
        /// </summary>
        /// <param name="filterType">过滤器实例的类型</param>
        /// <returns>方法元数据；未定义该方法时为 null</returns>
        static MethodInfo GetOnActionExecutedMethod(Type filterType)
        {
            if (_onActionExecutedCache.TryGetValue(filterType, out var cached)) return cached;

            var method = filterType.GetMethod(ConstHelper.ONACTIONEXECUTED);

            _onActionExecutedCache.TryAdd(filterType, method);

            return method;
        }

        /// <summary>
        /// MVC处理
        /// </summary>
        /// <param name="routeTable"></param>
        /// <param name="controller"></param>
        /// <param name="actionName"></param>
        /// <param name="nameValues"></param>
        /// <param name="isPost"></param>
        /// <returns></returns>
        public static ActionResult InvokeResult(RouteTable routeTable, Type controller, string actionName, NameValueCollection nameValues, bool isPost)
        {
            return InvokeResultAsync(routeTable, controller, actionName, nameValues, isPost).GetAwaiter().GetResult();
        }

        /// <summary>
        /// MVC处理：按真实请求方法选择 action 条目
        /// </summary>
        /// <param name="routeTable">路由表</param>
        /// <param name="controller">目标控制器类型</param>
        /// <param name="actionName">action 名称</param>
        /// <param name="nameValues">请求参数</param>
        /// <param name="method">请求方法，如 GET；为空时按 GET 处理</param>
        /// <returns>action 执行结果</returns>
        public static ActionResult InvokeResult(RouteTable routeTable, Type controller, string actionName, NameValueCollection nameValues, string method)
        {
            return InvokeResultAsync(routeTable, controller, actionName, nameValues, method).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 异步 MVC 处理：等待 action 真正完成，异常形态与同步入口一致
        /// </summary>
        /// <param name="routeTable">路由表</param>
        /// <param name="controller">目标控制器类型</param>
        /// <param name="actionName">action 名称</param>
        /// <param name="nameValues">请求参数</param>
        /// <param name="isPost">是否 POST 请求</param>
        /// <returns>action 执行结果</returns>
        public static async Task<ActionResult> InvokeResultAsync(RouteTable routeTable, Type controller, string actionName, NameValueCollection nameValues, bool isPost)
        {
            return await InvokeResultAsync(routeTable, controller, actionName, nameValues, isPost ? ConstHelper.POST : ConstHelper.GET).ConfigureAwait(false);
        }

        /// <summary>
        /// 异步 MVC 处理：等待 action 真正完成，异常形态与同步入口一致
        /// </summary>
        /// <param name="routeTable">路由表</param>
        /// <param name="controller">目标控制器类型</param>
        /// <param name="actionName">action 名称</param>
        /// <param name="nameValues">请求参数</param>
        /// <param name="method">请求方法，如 GET；为空时按 GET 处理</param>
        /// <returns>action 执行结果</returns>
        public static async Task<ActionResult> InvokeResultAsync(RouteTable routeTable, Type controller, string actionName, NameValueCollection nameValues, string method)
        {
            if (string.IsNullOrEmpty(method)) method = ConstHelper.GET;

            if (routeTable == null) return new ContentResult($"o_o，当前未注册任何Controller!", System.Net.HttpStatusCode.NotFound);

            // 请求上下文只在本入口取一次，后续与拆出的类型都复用这几份局部量，不再各层读静态环境
            var context = HttpContext.Current;
            var request = context.Request;
            var webConfig = context.WebConfig;
            var mvcContext = context as SAEA.MVC.Hosting.HttpContext;

            var routing = routeTable.GetOrAdd(controller, actionName, method);

            if (routing == null)
            {
                // 正文按 text/html 输出，actionName 来自请求路径，必须编码后再拼进文案
                return new ContentResult($"o_o，找不到：{controller.Name}/{System.Net.WebUtility.HtmlEncode(actionName)} 当前请求为:{method}", System.Net.HttpStatusCode.NotFound);
            }

            // 两段式路由表按请求方法分键，但 action 上声明的动词仍以请求行为准；
            // 这里按声明拦截，未声明则保持原有全部放行
            var allowedMethods = VerbPolicy.GetAllowedMethods(routing.Action);

            if (allowedMethods == null)
            {
                // 未声明动词的 action 不限读写，OPTIONS 仍由框架接管：给全量可声明动词后回 204，不执行 action；
                // 这里用 AllowOrder 而不是 All，因为不限谓词不等于显式声明 OPTIONS
                if (string.Compare(method, ConstHelper.OPTIONS, true) == 0)
                {
                    return CreateNoContent(context, VerbPolicy.CreateAllowHeader(HttpMethodCatalog.AllowOrder));
                }
            }
            else if (!VerbPolicy.IsMethodAllowed(allowedMethods, method))
            {
                var allow = VerbPolicy.CreateAllowHeader(allowedMethods);

                if (string.Compare(method, ConstHelper.OPTIONS, true) == 0)
                {
                    return CreateNoContent(context, allow);
                }

                if (!string.IsNullOrEmpty(allow))
                {
                    context.Response.Headers["Allow"] = allow;
                }

                // 文案用原始 request-target：Request.Url 会被剥掉数字末段，路径会缺一截
                var requestUrl = string.IsNullOrEmpty(request.RelativeUrl) ? request.Url : request.RelativeUrl;

                // 正文按 text/html 输出，requestUrl 含完整 query，编码后再拼进文案
                return new ContentResult("o_o，当前请求方式不被允许 url:" + System.Net.WebUtility.HtmlEncode(requestUrl) + "，允许的方式：" + allow, System.Net.HttpStatusCode.MethodNotAllowed);
            }

            // 能力入口按需物化：HttpContext.ResetForNextRequest 每请求都会把入口清空，
            // 在这里无条件取属性会让每个普通 HTTP 请求都新建一个只承载 WS 状态的入口对象
            WebSocketsFeature websockets = null;

            var isWebSocketAction = IsWebSocketAction(routing.Action);

            // WSController 上的非 [WebSocket] action（MessageAsync / CloseAsync / ErrorAsync）不允许普通 HTTP 直连
            if (routing.Instance is WSController wsController)
            {
                // 只有 WSController 才需要入口：user action 访问 context.WebSockets 时仍按需创建，语义不变
                websockets = context.WebSockets as WebSocketsFeature;

                websockets?.Attach(wsController);

                if (!isWebSocketAction)
                    return new ContentResult($"o_o，{routing.ControllerName}/{routing.ActionName} 只能作为 WebSocket 入口使用", System.Net.HttpStatusCode.BadRequest);
            }
            else if (isWebSocketAction)
            {
                return new ContentResult($"o_o，{routing.ControllerName}/{routing.ActionName} 必须声明在 WSController 派生类上", System.Net.HttpStatusCode.BadRequest);
            }

            ActionResult result;

            ActionResult beforeResult = null;

            //类过滤器
            if (routing.FilterAtrrs != null && routing.FilterAtrrs.Count > 0)
            {
                foreach (var arr in routing.FilterAtrrs)
                {
                    var filterMethod = GetOnActionExecutingMethod(arr.GetType());

                    if (filterMethod != null)
                    {
                        beforeResult = (ActionResult)filterMethod.Invoke(arr, null);
                    }
                }
            }

            //方法过滤器
            if (routing.ActionFilterAtrrs != null && routing.ActionFilterAtrrs.Count > 0)
            {
                foreach (var arr in routing.ActionFilterAtrrs)
                {
                    var filterMethod = GetOnActionExecutingMethod(arr.GetType());

                    if (filterMethod != null)
                    {
                        beforeResult = (ActionResult)filterMethod.Invoke(arr, null);
                    }
                }
            }

            #region actionResult

            if (beforeResult == null || beforeResult is EmptyResult)
            {
                if (!string.IsNullOrEmpty(request.ContentType)
               && request.ContentType.IndexOf(ConstHelper.Json, StringComparison.InvariantCultureIgnoreCase) > -1
               && !string.IsNullOrEmpty(request.Json))
                {
                    try
                    {
                        var dic = SerializeHelper.Deserialize<Dictionary<string, string>>(request.Json);

                        if (request.Parmas == null || !request.Parmas.Any())
                        {
                            request.Parmas = dic;
                        }
                        else
                        {
                            if (dic != null && dic.Any())
                            {
                                foreach (var item in dic)
                                {
                                    request.Parmas[item.Key] = item.Value;
                                }
                            }
                        }

                        nameValues = request.Parmas.ToNameValueCollection();
                    }
                    catch
                    {
                        try
                        {
                            var list = SerializeHelper.Deserialize<List<object>>(request.Json);

                            if (list != null && list.Count > 0)
                            {
                                var ps = routing.Action.GetParameters();
                                if (ps != null && ps.Length > 0)
                                {
                                    foreach (var item in ps)
                                    {
                                        if (item.ParameterType.Name == "List`1")
                                        {
                                            nameValues.Add(item.Name, SerializeHelper.Deserialize(SerializeHelper.Serialize(list), item.ParameterType));
                                        }
                                    }

                                }
                            }
                        }
                        catch
                        {
                            return new ContentResult("o_o，错误请求,Json:" + request.Json, System.Net.HttpStatusCode.BadRequest);
                        }
                    }
                }

                if (isWebSocketAction)
                {
                    try
                    {
                        // WebSocket 入口固定走超时路径：超时后按 ErrorAsync 收尾关闭连接，不能退化成无限等待
                        var invokeResult = await ActionInvoker.InvokeAsync(routing.Action, routing.Instance, nameValues, mvcContext, webConfig, true).ConfigureAwait(false);

                        // 正常返回统一当 EmptyResult：响应已由握手或连接负责
                        if (invokeResult is ContentResult contentResult && contentResult.Status == System.Net.HttpStatusCode.RequestTimeout)
                        {
                            // 已升级的连接不能再写 HTTP 响应：按服务端异常收尾，避免连接悬空
                            if (websockets?.Current != null)
                            {
                                websockets.HandleActionException(new TimeoutException($"{routing.Action.Name}: WebSocket 入口执行超过 {ActionInvoker.GetActionTimeoutSeconds(webConfig)} 秒，连接已关闭"));
                                result = new EmptyResult();
                            }
                            else
                            {
                                result = invokeResult;
                            }
                        }
                        else
                        {
                            result = new EmptyResult();
                        }
                    }
                    catch (Exception ex)
                    {
                        // 不走 HttpHandle 的异常链（OnException 可能没挂），直接交给 WebSocket 错误收尾
                        websockets?.HandleActionException(ActionInvoker.Unwrap(ex));
                        result = new EmptyResult();
                    }
                }
                else
                {
                    result = await ActionInvoker.InvokeAsync(routing.Action, routing.Instance, nameValues, mvcContext, webConfig).ConfigureAwait(false);
                }
            }
            else
            {
                result = beforeResult;
            }
            #endregion

            // 过滤器通过数组回写结果：没有过滤器时不必每请求建一个中转数组
            object[] nargs = null;

            if (routing.FilterAtrrs != null && routing.FilterAtrrs.Count > 0)
            {
                foreach (var arr in routing.FilterAtrrs)
                {
                    var filterMethod = GetOnActionExecutedMethod(arr.GetType());
                    if (filterMethod != null)
                    {
                        if (nargs == null) nargs = new object[] { result };

                        filterMethod.Invoke(arr, nargs);

                        result = (ActionResult)nargs[0];
                    }

                }
            }

            if (routing.ActionFilterAtrrs != null && routing.ActionFilterAtrrs.Count > 0)
            {
                foreach (var arr in routing.ActionFilterAtrrs)
                {
                    var filterMethod = GetOnActionExecutedMethod(arr.GetType());
                    if (filterMethod != null)
                    {
                        if (nargs == null) nargs = new object[] { result };

                        filterMethod.Invoke(arr, nargs);
                        result = (ActionResult)nargs[0];
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 生成 OPTIONS 的自动应答：204 无正文，有可声明的动词时才写 Allow
        /// </summary>
        /// <param name="context">当前请求上下文</param>
        /// <param name="allow">Allow 报头内容，为空表示不写该报头</param>
        /// <returns>204 空结果</returns>
        static ActionResult CreateNoContent(SAEA.Http.Model.IHttpContext context, string allow)
        {
            if (!string.IsNullOrEmpty(allow))
            {
                context.Response.Headers["Allow"] = allow;
            }

            return new EmptyResult { Status = System.Net.HttpStatusCode.NoContent };
        }

    }
}