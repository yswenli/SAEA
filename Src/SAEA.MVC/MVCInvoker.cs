/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MVC
*文件名： MvcInvoker
*版本号： V4.0.0.1
*唯一标识：eb956356-8ea4-4657-aec1-458a3654c078
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/10 18:10:16
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/10 18:10:16
*修改人： yswenli
*版本号： V4.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Http;
using SAEA.Http.Common;
using SAEA.Http.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SAEA.MVC
{
    /// <summary>
    /// saea.mvc实例方法映射处理类
    /// </summary>
    public class MvcInvoker : IInvoker
    {
        protected HttpContext _httpContext;

        public object Parma { get; set; }

        public MvcInvoker(object parma)
        {
            this.Parma = parma;
        }


        public IHttpResult GetActionResult(HttpContext httpContext)
        {
            RouteTable routeTable = (RouteTable)this.Parma;

            string url = httpContext.Request.Url;

            bool isPost = httpContext.Request.Method == ConstHelper.POST;

            //禁止访问
            var flist = httpContext.WebConfig.ForbiddenAccessList;

            if (flist.Count > 0)
            {
                foreach (var item in flist)
                {
                    if (url.IndexOf(item, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return new ContentResult("o_o，当前内容禁止访问！", System.Net.HttpStatusCode.Forbidden);
                    }
                    if (System.Text.RegularExpressions.Regex.IsMatch(url, item, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        return new ContentResult("o_o，当前内容禁止访问！", System.Net.HttpStatusCode.Forbidden);
                    }
                }
            }

            var arr = url.Split("/", StringSplitOptions.RemoveEmptyEntries);

            var filePath = string.Empty;

            NameValueCollection nameValues;

            switch (arr.Length)
            {
                case 0:

                    filePath = httpContext.Server.MapPath(httpContext.WebConfig.DefaultPage);

                    if (StaticResourcesCache.Exists(filePath))
                    {
                        return new FileResult(filePath, httpContext.IsStaticsCached);
                    }
                    else
                    {
                        var d = RouteTable.Types.Where(b => (string.Compare(b.Name, httpContext.WebConfig.DefaultRout.Name, true) == 0 || string.Compare(b.Name, httpContext.WebConfig.DefaultRout.Name + ConstHelper.CONTROLLERNAME, true) == 0)).FirstOrDefault();

                        nameValues = httpContext.Request.Parmas.ToNameValueCollection();

                        return MvcInvoke(httpContext, routeTable, d, httpContext.WebConfig.DefaultRout.Value, nameValues, isPost);
                    }

                case 1:
                    filePath = httpContext.Server.MapPath(url);
                    if (StaticResourcesCache.Exists(filePath))
                    {
                        return new FileResult(filePath, httpContext.IsStaticsCached);
                    }
                    break;

                default:
                    var controllerName = arr[arr.Length - 2];

                    var first = RouteTable.Types.Where(b => string.Compare(b.Name, controllerName + ConstHelper.CONTROLLERNAME, true) == 0).FirstOrDefault();

                    if (first != null)
                    {
                        nameValues = httpContext.Request.Parmas.ToNameValueCollection();

                        return MvcInvoke(httpContext, routeTable, first, arr[arr.Length - 1], nameValues, isPost);
                    }
                    else
                    {
                        filePath = httpContext.Server.MapPath(url);
                        if (StaticResourcesCache.Exists(filePath))
                        {
                            return new FileResult(filePath, httpContext.IsStaticsCached);
                        }
                    }
                    break;
            }
            return new ContentResult("o_o，找不到任何内容", System.Net.HttpStatusCode.NotFound);
        }

        /// <summary>
        /// MVC处理
        /// </summary>
        /// <param name="httpContext"></param>
        /// <param name="routeTable"></param>
        /// <param name="controller"></param>
        /// <param name="actionName"></param>
        /// <param name="nameValues"></param>
        /// <param name="isPost"></param>
        /// <returns></returns>
        private static ActionResult MvcInvoke(HttpContext httpContext, RouteTable routeTable, Type controller, string actionName, NameValueCollection nameValues, bool isPost)
        {
            try
            {
                var routing = routeTable.GetOrAdd(controller, actionName, isPost);

                if (routing == null)
                {
                    throw new Exception($"o_o，找不到：{controller.Name}/{actionName} 当前请求为:{(isPost ? ConstHelper.HTTPPOST : ConstHelper.HTTPGET)}");
                }

                routing.Instance.HttpContext = httpContext;

                var nargs = new object[] { httpContext };

                if (routing.FilterAtrrs != null && routing.FilterAtrrs.Count > 0)
                {
                    foreach (var arr in routing.FilterAtrrs)
                    {
                        var method = arr.GetType().GetMethod(ConstHelper.ONACTIONEXECUTING);

                        if (method != null)
                        {
                            var goOn = (bool)method.Invoke(arr, nargs.ToArray());

                            if (!goOn)
                            {
                                return new ContentResult("o_o，当前逻辑已被拦截！", System.Net.HttpStatusCode.NotAcceptable);
                            }
                        }
                    }
                }

                if (routing.ActionFilterAtrrs != null && routing.ActionFilterAtrrs.Count > 0)
                {
                    foreach (var arr in routing.ActionFilterAtrrs)
                    {
                        if (arr.ToString() == ConstHelper.OUTPUTCACHEATTRIBUTE)
                        {
                            var method = arr.GetType().GetMethod(ConstHelper.ONACTIONEXECUTING);

                            if (method != null)
                            {
                                httpContext.Session.CacheCalcResult = (string)arr.GetType().GetMethod(ConstHelper.ONACTIONEXECUTING).Invoke(arr, nargs.ToArray());
                            }
                        }
                        else
                        {
                            var method = arr.GetType().GetMethod(ConstHelper.ONACTIONEXECUTING);

                            if (method != null)
                            {
                                var goOn = (bool)arr.GetType().GetMethod(ConstHelper.ONACTIONEXECUTING).Invoke(arr, nargs.ToArray());

                                if (!goOn)
                                {
                                    return new ContentResult("o_o，当前逻辑已被拦截！", System.Net.HttpStatusCode.NotAcceptable);
                                }
                            }
                        }
                    }
                }

                #region actionResult

                ActionResult result;

                if (httpContext.Request.ContentType == ConstHelper.FORMENCTYPE3 && !string.IsNullOrEmpty(httpContext.Request.Json))
                {
                    var nnv = SerializeHelper.Deserialize<Dictionary<string, string>>(httpContext.Request.Json).ToNameValueCollection();

                    result = MethodInvoke(routing.Action, routing.Instance, nnv);
                }
                else
                {
                    result = MethodInvoke(routing.Action, routing.Instance, nameValues);
                }

                #endregion



                nargs = new object[] { httpContext, result };

                if (routing.FilterAtrrs != null && routing.FilterAtrrs.Count > 0)
                {
                    foreach (var arr in routing.FilterAtrrs)
                    {
                        var method = arr.GetType().GetMethod(ConstHelper.ONACTIONEXECUTED);
                        if (method != null)
                            method.Invoke(arr, nargs);
                    }
                }

                if (routing.ActionFilterAtrrs != null && routing.ActionFilterAtrrs.Count > 0)
                {
                    foreach (var arr in routing.ActionFilterAtrrs)
                    {
                        if (arr.ToString() == ConstHelper.OUTPUTCACHEATTRIBUTE)
                        {
                            continue;
                        }
                        else
                        {
                            var method = arr.GetType().GetMethod(ConstHelper.ONACTIONEXECUTED);
                            if (method != null)
                                method.Invoke(arr, nargs);
                        }
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("o_o，找不到"))
                {
                    return new ContentResult(ex.Message, System.Net.HttpStatusCode.NotFound);
                }
                else
                {
                    return new ContentResult("→_→，出错了：" + ex.Message, System.Net.HttpStatusCode.InternalServerError);
                }
            }
        }

        /// <summary>
        /// 代理执行方法
        /// </summary>
        /// <param name="action"></param>
        /// <param name="obj"></param>
        /// <param name="nameValues"></param>
        /// <returns></returns>
        private static ActionResult MethodInvoke(MethodInfo action, object obj, NameValueCollection nameValues)
        {
            ActionResult result = null;
            try
            {
                var @params = action.GetParameters();

                if (@params != null && @params.Length > 0)
                {
                    var list = ParamsHelper.FillPamars(@params, nameValues);

                    result = (ActionResult)action.Invoke(obj, list.ToArray());
                }
                else
                {
                    result = (ActionResult)action.Invoke(obj, null);
                }
            }
            catch (Exception ex)
            {
                result = new ContentResult($"→_→，出错了：{obj}/{action.Name},出现异常：{ex.Message}", System.Net.HttpStatusCode.InternalServerError);
            }
            return result;
        }


    }
}
