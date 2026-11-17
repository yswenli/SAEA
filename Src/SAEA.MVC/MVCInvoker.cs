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
*修改时间：2026/11/17 21:00:00
*修改人： yswenli
*版本号： v26.11.16.1
*描述：两段式路由按 action 上声明的 [HttpGet]/[HttpPost] 严格拦截动词，未声明则全部放行
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
            if (routeTable == null) return new ContentResult($"o_o，当前未注册任何Controller!", System.Net.HttpStatusCode.NotFound);

            var routing = routeTable.GetOrAdd(controller, actionName, isPost);

            if (routing == null)
            {
                return new ContentResult($"o_o，找不到：{controller.Name}/{actionName} 当前请求为:{(isPost ? ConstHelper.HTTPPOST : ConstHelper.HTTPGET)}", System.Net.HttpStatusCode.NotFound);
            }

            // 两段式路由表只按 GET/POST 分键，PUT、DELETE 也走 GET 分支，isPost 不足以判定真实动词；
            // 这里以请求行里的方法为准，按 action 声明的动词拦截，未声明则保持原有全部放行
            var allowedMethods = GetAllowedMethods(routing.Action);

            if (allowedMethods != null && !IsMethodAllowed(allowedMethods, HttpContext.Current.Request.Method))
            {
                var allow = string.Join(", ", allowedMethods);

                HttpContext.Current.Response.Headers["Allow"] = allow;

                return new ContentResult("o_o，当前请求方式不被允许 url:" + HttpContext.Current.Request.Url + "，允许的方式：" + allow, System.Net.HttpStatusCode.MethodNotAllowed);
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
                        var invokeResult = MethodInvoke(routing.Action, routing.Instance, nameValues);

                        // 正常返回统一当 EmptyResult：响应已由握手或连接负责
                        if (invokeResult is ContentResult contentResult && contentResult.Status == System.Net.HttpStatusCode.RequestTimeout)
                        {
                            // 已升级的连接不能再写 HTTP 响应：按服务端异常收尾，避免连接悬空
                            if (websockets?.Current != null)
                            {
                                websockets.HandleActionException(new TimeoutException($"{routing.Action.Name}: WebSocket 入口执行超过 {HttpContext.Current.WebConfig.Timeout} 秒，连接已关闭"));
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
                    result = MethodInvoke(routing.Action, routing.Instance, nameValues);
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
        /// 代理执行方法
        /// </summary>
        /// <param name="action"></param>
        /// <param name="obj"></param>
        /// <param name="nameValues"></param>
        /// <returns></returns>
        static ActionResult MethodInvoke(MethodInfo action, object obj, NameValueCollection nameValues)
        {
            ActionResult result = null;
            object data = null;

            var @params = action.GetParameters();
            int timeoutMs = (int)(HttpContext.Current.WebConfig.Timeout * 1000);

            try
            {
                // 创建参数列表
                object[] parameters = null;
                if (@params != null && @params.Any())
                {
                    var list = ParamsHelper.FillPamars(@params, nameValues);
                    parameters = list.ToArray();
                }

                // 检查是否为异步方法（Task / Task<T>）
                bool isAsync = action.ReturnType != null && typeof(Task).IsAssignableFrom(action.ReturnType);

                if (isAsync)
                {
                    // 处理异步方法
                    var task = (Task)action.Invoke(obj, parameters);

                    // 等待任务完成或超时
                    if (!task.Wait(timeoutMs))
                    {
                        // 超时
                        return new ContentResult($"{action.Name}: The execution of this asynchronous method has timed out", System.Net.HttpStatusCode.RequestTimeout);
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
                    // 处理同步方法
                    var task = Task.Run(() => action.Invoke(obj, parameters));
                    
                    // 等待任务完成或超时
                    if (!task.Wait(timeoutMs))
                    {
                        // 超时
                        return new ContentResult($"{action.Name}: The execution of this method has timed out", System.Net.HttpStatusCode.RequestTimeout);
                    }
                    
                    // 任务完成，获取结果
                    data = task.Result;
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
            }
            catch
            {
                // 处理其他异常
                throw;
            }

            return result;
        }
    }
}