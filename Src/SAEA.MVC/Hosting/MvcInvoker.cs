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
*文件名： MVCInvoker
*版本号： v26.4.23.1
*唯一标识：df2222b1-5599-42e6-b920-b28f86849d31
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/28 14:19:50
*描述：MVCInvoker接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/18 16:14:00
*修改人： yswenli
*版本号： v26.11.16.1
*描述：InvokeResult 抽出 async 核心 InvokeResultAsync，原公开方法改为阻塞等待同一实现；MethodInvoke 改为异步执行，未配置超时时直接等待，配置超时时用 WhenAny 加取消源复现 408 与 AggregateException 形态
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.NameValue;
using SAEA.Common.Serialization;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MVC
{
    /// <summary>
    /// saea.mvc实例方法映射处理类
    /// </summary>
    public static class MvcInvoker
    {
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
            if (routeTable == null) return new ContentResult($"o_o，当前未注册任何Controller!", System.Net.HttpStatusCode.NotFound);

            var routing = routeTable.GetOrAdd(controller, actionName, isPost);

            if (routing == null)
            {
                // 两段式路由表只按 GET/POST 分键，PUT、DELETE 也走 GET 分支，isPost 不足以代表真实动词，以请求行为准
                var requestMethod = HttpContext.Current.Request.Method;

                if (string.IsNullOrEmpty(requestMethod))
                    requestMethod = isPost ? ConstHelper.POST : ConstHelper.GET;

                // 正文按 text/html 输出，actionName 来自请求路径，必须编码后再拼进文案
                return new ContentResult($"o_o，找不到：{controller.Name}/{System.Net.WebUtility.HtmlEncode(actionName)} 当前请求为:{requestMethod}", System.Net.HttpStatusCode.NotFound);
            }

            // 两段式路由表只按 GET/POST 分键，PUT、DELETE 也走 GET 分支，isPost 不足以判定真实动词；
            // 这里以请求行里的方法为准，按 action 声明的动词拦截，未声明则保持原有全部放行
            var allowedMethods = GetAllowedMethods(routing.Action);

            if (allowedMethods != null && !IsMethodAllowed(allowedMethods, HttpContext.Current.Request.Method))
            {
                var allow = string.Join(", ", allowedMethods);

                HttpContext.Current.Response.Headers["Allow"] = allow;

                // 文案用原始 request-target：Request.Url 会被剥掉数字末段，路径会缺一截
                var requestUrl = string.IsNullOrEmpty(HttpContext.Current.Request.RelativeUrl) ? HttpContext.Current.Request.Url : HttpContext.Current.Request.RelativeUrl;

                // 正文按 text/html 输出，requestUrl 含完整 query，编码后再拼进文案
                return new ContentResult("o_o，当前请求方式不被允许 url:" + System.Net.WebUtility.HtmlEncode(requestUrl) + "，允许的方式：" + allow, System.Net.HttpStatusCode.MethodNotAllowed);
            }

            var websockets = HttpContext.Current.WebSockets as WebSocketsFeature;

            var isWebSocketAction = routing.Action != null && routing.Action.GetCustomAttributes(typeof(WebSocketAttribute), true).Any();

            // WSController 上的非 [WebSocket] action（MessageAsync / CloseAsync / ErrorAsync）不允许普通 HTTP 直连
            if (routing.Instance is WSController wsController)
            {
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
            if (routing.FilterAtrrs != null && routing.FilterAtrrs.Any())
            {
                foreach (var arr in routing.FilterAtrrs)
                {
                    var method = arr.GetType().GetMethod(ConstHelper.ONACTIONEXECUTING);

                    if (method != null)
                    {
                        beforeResult = (ActionResult)method.Invoke(arr, null);
                    }
                }
            }

            //方法过滤器
            if (routing.ActionFilterAtrrs != null && routing.ActionFilterAtrrs.Any())
            {
                foreach (var arr in routing.ActionFilterAtrrs)
                {
                    var method = arr.GetType().GetMethod(ConstHelper.ONACTIONEXECUTING);

                    if (method != null)
                    {
                        beforeResult = (ActionResult)arr.GetType().GetMethod(ConstHelper.ONACTIONEXECUTING).Invoke(arr, null);
                    }
                }
            }

            #region actionResult

            if (beforeResult == null || beforeResult is EmptyResult)
            {
                if (!string.IsNullOrEmpty(HttpContext.Current.Request.ContentType)
               && HttpContext.Current.Request.ContentType.IndexOf(ConstHelper.Json, StringComparison.InvariantCultureIgnoreCase) > -1
               && !string.IsNullOrEmpty(HttpContext.Current.Request.Json))
                {
                    try
                    {
                        var dic = SerializeHelper.Deserialize<Dictionary<string, string>>(HttpContext.Current.Request.Json);

                        if (HttpContext.Current.Request.Parmas == null || !HttpContext.Current.Request.Parmas.Any())
                        {
                            HttpContext.Current.Request.Parmas = dic;
                        }
                        else
                        {
                            if (dic != null && dic.Any())
                            {
                                foreach (var item in dic)
                                {
                                    HttpContext.Current.Request.Parmas[item.Key] = item.Value;
                                }
                            }
                        }

                        nameValues = HttpContext.Current.Request.Parmas.ToNameValueCollection();
                    }
                    catch
                    {
                        try
                        {
                            var list = SerializeHelper.Deserialize<List<object>>(HttpContext.Current.Request.Json);

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
                            return new ContentResult("o_o，错误请求,Json:" + HttpContext.Current.Request.Json, System.Net.HttpStatusCode.BadRequest);
                        }
                    }
                }

                if (isWebSocketAction)
                {
                    try
                    {
                        // WebSocket 入口固定走超时路径：超时后按 ErrorAsync 收尾关闭连接，不能退化成无限等待
                        var invokeResult = await MethodInvokeAsync(routing.Action, routing.Instance, nameValues, true).ConfigureAwait(false);

                        // 正常返回统一当 EmptyResult：响应已由握手或连接负责
                        if (invokeResult is ContentResult contentResult && contentResult.Status == System.Net.HttpStatusCode.RequestTimeout)
                        {
                            // 已升级的连接不能再写 HTTP 响应：按服务端异常收尾，避免连接悬空
                            if (websockets?.Current != null)
                            {
                                websockets.HandleActionException(new TimeoutException($"{routing.Action.Name}: WebSocket 入口执行超过 {GetActionTimeoutSeconds()} 秒，连接已关闭"));
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
                        websockets?.HandleActionException(Unwrap(ex));
                        result = new EmptyResult();
                    }
                }
                else
                {
                    result = await MethodInvokeAsync(routing.Action, routing.Instance, nameValues).ConfigureAwait(false);
                }
            }
            else
            {
                result = beforeResult;
            }
            #endregion

            var nargs = new object[] { result };

            if (routing.FilterAtrrs != null && routing.FilterAtrrs.Any())
            {
                foreach (var arr in routing.FilterAtrrs)
                {
                    var method = arr.GetType().GetMethod(ConstHelper.ONACTIONEXECUTED);
                    if (method != null)
                    {
                        method.Invoke(arr, nargs);

                        result = (ActionResult)nargs[0];
                    }

                }
            }

            if (routing.ActionFilterAtrrs != null && routing.ActionFilterAtrrs.Any())
            {
                foreach (var arr in routing.ActionFilterAtrrs)
                {
                    var method = arr.GetType().GetMethod(ConstHelper.ONACTIONEXECUTED);
                    if (method != null)
                    {
                        method.Invoke(arr, nargs);
                        result = (ActionResult)nargs[0];
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 读取 action 上声明的 [HttpGet] / [HttpPost]，返回允许的请求方法
        /// </summary>
        /// <param name="action">目标 action 方法</param>
        /// <returns>按 GET、POST 顺序排列的允许方法；两者都未声明时返回 null，表示不限制</returns>
        static string[] GetAllowedMethods(MethodInfo action)
        {
            if (action == null) return null;

            var attributes = action.GetCustomAttributes(true);

            if (attributes == null || attributes.Length == 0) return null;

            var allowGet = false;

            var allowPost = false;

            for (var i = 0; i < attributes.Length; i++)
            {
                var name = attributes[i].GetType().Name;

                if (name == ConstHelper.HTTPGET)
                    allowGet = true;
                else if (name == ConstHelper.HTTPPOST)
                    allowPost = true;
            }

            if (!allowGet && !allowPost) return null;

            if (allowGet && allowPost) return new[] { ConstHelper.GET, ConstHelper.POST };

            return allowGet ? new[] { ConstHelper.GET } : new[] { ConstHelper.POST };
        }

        /// <summary>
        /// 判断请求方法是否在允许集合内，比较忽略大小写
        /// </summary>
        /// <param name="allowedMethods">允许的请求方法集合，不能为 null</param>
        /// <param name="method">请求行中的方法</param>
        /// <returns>命中任一允许方法时返回 true</returns>
        static bool IsMethodAllowed(string[] allowedMethods, string method)
        {
            for (var i = 0; i < allowedMethods.Length; i++)
            {
                if (string.Compare(allowedMethods[i], method, StringComparison.OrdinalIgnoreCase) == 0) return true;
            }

            return false;
        }

        /// <summary>
        /// 解包 Task.Wait 抛出的 AggregateException
        /// </summary>
        static Exception Unwrap(Exception exception)
        {
            while (exception is AggregateException aggregate && aggregate.InnerException != null)
            {
                exception = aggregate.InnerException;
            }

            return exception;
        }

        /// <summary>
        /// 取本次请求的 action 执行超时秒数，仅在启用超时路径时使用
        /// </summary>
        /// <returns>ActionTimeout 大于 0 时用它，否则沿用连接空闲回收的 Timeout</returns>
        static double GetActionTimeoutSeconds()
        {
            var webConfig = HttpContext.Current.WebConfig;

            // 0 与负数都视为未单独配置：需要超时收尾的入口（显式配置 ActionTimeout 的动作与 WebSocket 入口）
            // 继续沿用 Timeout 兜底
            return webConfig.ActionTimeout > 0 ? webConfig.ActionTimeout : webConfig.Timeout;
        }

        /// <summary>
        /// 代理执行方法
        /// </summary>
        /// <param name="action"></param>
        /// <param name="obj"></param>
        /// <param name="nameValues"></param>
        /// <param name="alwaysTimeout">是否无视配置强制启用超时包装，WebSocket 入口依赖 408 收尾必须传 true</param>
        /// <returns></returns>
        static ActionResult MethodInvoke(MethodInfo action, object obj, NameValueCollection nameValues, bool alwaysTimeout = false)
        {
            return MethodInvokeAsync(action, obj, nameValues, alwaysTimeout).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 异步代理执行方法：未启用超时时直接等待 action 完成；启用超时时等待完成或到点返回 408
        /// </summary>
        /// <param name="action">目标 action</param>
        /// <param name="obj">控制器实例</param>
        /// <param name="nameValues">请求参数</param>
        /// <param name="alwaysTimeout">是否无视配置强制启用超时包装，WebSocket 入口依赖 408 收尾必须传 true</param>
        /// <returns>action 结果；超时返回 408 结果</returns>
        static async Task<ActionResult> MethodInvokeAsync(MethodInfo action, object obj, NameValueCollection nameValues, bool alwaysTimeout = false)
        {
            ActionResult result = null;
            object data = null;

            var @params = action.GetParameters();
            int timeoutMs = (int)(GetActionTimeoutSeconds() * 1000);

            // 只有显式配置 ActionTimeout 才启用超时包装：默认的 Timeout 兜底长达 180 秒，实际不会命中，
            // 却要让每个请求额外付出一次线程跳转与带超时的等待
            var withTimeout = alwaysTimeout || HttpContext.Current.WebConfig.ActionTimeout > 0;

            // 每次调用一个取消源：超时前由 finally 释放，超时后交给任务观察回调释放
            var cts = new CancellationTokenSource();

            // 标记已交给超时路径处理，避免 finally 提前释放掉还在被 action 观察的取消源
            var handedToTimeout = false;

            try
            {
                // 先落到上下文再调用 action，保证 action 内能读到本次令牌
                if (HttpContext.Current is SAEA.MVC.HttpContext mvcContext)
                    mvcContext.RequestAborted = cts.Token;

                // 创建参数列表
                object[] parameters = null;
                if (@params != null && @params.Any())
                {
                    parameters = FillParameters(@params, nameValues, cts.Token);
                }

                // 检查是否为异步方法（Task / Task<T>）
                bool isAsync = action.ReturnType != null && typeof(Task).IsAssignableFrom(action.ReturnType);

                if (!withTimeout)
                {
                    // 快路径：不设超时。同步 action 直接调用，异步 action 真正 await，等待期间不占用线程
                    if (isAsync)
                    {
                        var task = (Task)action.Invoke(obj, parameters);

                        try
                        {
                            await task.ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            // 旧路径由 Task.Wait 抛出 AggregateException，这里按同样的形态抛回，
                            // 保证 OnException 收到的异常类型与 500 文案逐字不变
                            throw new AggregateException(ex);
                        }

                        // 任务完成，Task<T> 取 Result，非泛型 Task 没有结果
                        if (action.ReturnType.IsGenericType && action.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                        {
                            var resultProperty = task.GetType().GetProperty("Result");
                            data = resultProperty?.GetValue(task);
                        }
                    }
                    else
                    {
                        try
                        {
                            data = action.Invoke(obj, parameters);
                        }
                        catch (Exception ex)
                        {
                            // 同上：旧路径经 Task.Run + Wait 抛 AggregateException，异常形态必须保持一致
                            throw new AggregateException(ex);
                        }
                    }
                }
                else if (isAsync)
                {
                    // 处理异步方法
                    var task = (Task)action.Invoke(obj, parameters);

                    // 等待任务完成或超时
                    if (!await WaitWithTimeoutAsync(task, timeoutMs).ConfigureAwait(false))
                    {
                        // 超时
                        handedToTimeout = true;

                        // Cancel() 在调用线程同步执行用户注册的回调，回调阻塞会把 408 拖后甚至自锁，因此放到线程池执行
                        _ = Task.Run(() => CancelQuietly(cts));

                        ObserveAndDispose(task, cts);

                        return new ContentResult($"{action.Name}: The execution of this asynchronous method has timed out", System.Net.HttpStatusCode.RequestTimeout);
                    }

                    // 与原 task.Wait(timeoutMs) 返回 true 后一致：失败任务按 AggregateException 抛出
                    RethrowLikeWait(task);

                    // 任务完成，Task<T> 取 Result，非泛型 Task 没有结果
                    if (action.ReturnType.IsGenericType && action.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                    {
                        var resultProperty = task.GetType().GetProperty("Result");
                        data = resultProperty?.GetValue(task);
                    }
                }
                else
                {
                    // 处理同步方法
                    var task = Task.Run(() => action.Invoke(obj, parameters));

                    // 等待任务完成或超时
                    if (!await WaitWithTimeoutAsync(task, timeoutMs).ConfigureAwait(false))
                    {
                        // 超时
                        handedToTimeout = true;

                        // Cancel() 在调用线程同步执行用户注册的回调，回调阻塞会把 408 拖后甚至自锁，因此放到线程池执行
                        _ = Task.Run(() => CancelQuietly(cts));

                        ObserveAndDispose(task, cts);

                        return new ContentResult($"{action.Name}: The execution of this method has timed out", System.Net.HttpStatusCode.RequestTimeout);
                    }

                    // 任务完成，获取结果（已失败时与原 task.Wait 一样抛 AggregateException）
                    data = task.Result;
                }
            }
            finally
            {
                // 非超时路径（正常完成、action 同步抛异常、等待时抛 AggregateException）自己释放
                if (!handedToTimeout) cts.Dispose();
            }

            // 处理各种返回类型
            if (data is ActionResult)
            {
                result = data as ActionResult;
            }
            else if (data is string)
            {
                result = new ContentResult(data as string);
            }
            else
            {
                result = new JsonResult(data);
            }

            return result;
        }

        /// <summary>
        /// 等待任务完成或到达超时；结束时立即释放计时器，避免长超时配置下计时器常驻
        /// </summary>
        /// <param name="task">待等待的任务</param>
        /// <param name="timeoutMs">最长等待毫秒数</param>
        /// <returns>任务在超时前完成返回 true，超时返回 false</returns>
        static async Task<bool> WaitWithTimeoutAsync(Task task, int timeoutMs)
        {
            // 已完成的任务不必建计时器：原 Task.Wait 在这种情况下也是立即返回
            if (task.IsCompleted)
                return true;

            using (var delayCts = new CancellationTokenSource())
            {
                var delayTask = Task.Delay(timeoutMs, delayCts.Token);

                var completed = await Task.WhenAny(task, delayTask).ConfigureAwait(false);

                if (ReferenceEquals(completed, task))
                {
                    // 任务先完成，主动取消计时器；被取消的延迟任务不会产生未观察异常
                    delayCts.Cancel();
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// 按 Task.Wait 的异常形态重新抛出：失败任务抛 AggregateException，取消任务包一层 TaskCanceledException
        /// </summary>
        /// <param name="task">已结束的任务</param>
        static void RethrowLikeWait(Task task)
        {
            if (task.IsFaulted)
                throw task.Exception;

            if (task.IsCanceled)
                throw new AggregateException(new TaskCanceledException(task));
        }

        /// <summary>
        /// 按声明顺序填充 action 参数，框架注入 CancellationToken 类型的参数
        /// </summary>
        /// <param name="params">action 的参数声明</param>
        /// <param name="nameValues">请求参数</param>
        /// <param name="token">本次调用注入的取消令牌</param>
        /// <returns>与参数声明一一对应的实参数组</returns>
        static object[] FillParameters(ParameterInfo[] @params, NameValueCollection nameValues, CancellationToken token)
        {
            var parameters = new object[@params.Length];

            var hasToken = false;

            for (var i = 0; i < @params.Length; i++)
            {
                if (@params[i].ParameterType == typeof(CancellationToken))
                {
                    hasToken = true;
                    break;
                }
            }

            if (!hasToken)
            {
                var list = ParamsHelper.FillPamars(@params, nameValues);

                if (list == null) return parameters;

                return list.ToArray();
            }

            // 令牌不能交给参数填充：先剔除非令牌参数填充，再按原声明顺序把令牌插回原位
            var others = new List<ParameterInfo>(@params.Length);

            for (var i = 0; i < @params.Length; i++)
            {
                if (@params[i].ParameterType != typeof(CancellationToken))
                    others.Add(@params[i]);
            }

            List<object> filled = null;

            if (others.Count > 0)
                filled = ParamsHelper.FillPamars(others.ToArray(), nameValues);

            var filledIndex = 0;

            for (var i = 0; i < @params.Length; i++)
            {
                if (@params[i].ParameterType == typeof(CancellationToken))
                    parameters[i] = token;
                else
                    parameters[i] = filled[filledIndex++];
            }

            return parameters;
        }

        /// <summary>
        /// 取消并吞掉竞态异常：取消与任务完成后的释放可能并发，取消回调本身也可能抛异常
        /// </summary>
        /// <param name="cts">待取消的取消源</param>
        static void CancelQuietly(CancellationTokenSource cts)
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // 任务已结束并释放了取消源，此时没有回调需要执行，无需再取消
            }
            catch (AggregateException)
            {
                // 令牌回调抛出的异常不能顶掉 408 响应
            }
        }

        /// <summary>
        /// 观察超时任务的异常并在其结束后释放取消源
        /// </summary>
        /// <param name="task">已超时的任务</param>
        /// <param name="cts">本次调用的取消源</param>
        static void ObserveAndDispose(Task task, CancellationTokenSource cts)
        {
            task.ContinueWith(t =>
            {
                // 读一次 Exception 把异常标记为已观察，避免未观察异常在终结时抛出
                var ignored = t.Exception;

                cts.Dispose();
            }, TaskScheduler.Default);
        }
    }
}