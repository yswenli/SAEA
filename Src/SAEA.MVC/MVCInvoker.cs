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
*唯一标识：42c60c32-ddc8-417c-9a4e-0e714e9f281f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/28 14:19:50
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/10/28 14:19:50
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
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

                result = MethodInvoke(routing.Action, routing.Instance, nameValues);
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

                // 检查是否为异步方法
                bool isAsync = action.ReturnType.IsGenericType && action.ReturnType.GetGenericTypeDefinition() == typeof(Task<>);

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
                    
                    // 任务完成，获取结果
                    var resultProperty = task.GetType().GetProperty("Result");
                    data = resultProperty.GetValue(task);
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