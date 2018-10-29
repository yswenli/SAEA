/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.MVC
*文件名： MvcInvoker
*版本号： V3.1.1.0
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
*版本号： V3.1.1.0
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Http;
using SAEA.Http.Common;
using SAEA.Http.Model;
using System;
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


        protected IHttpResult GetActionResult()
        {
            RouteTable routeTable = (RouteTable)this.Parma;

            string url = _httpContext.Request.Url;

            NameValueCollection nameValues = _httpContext.Request.Parmas.ToNameValueCollection();

            bool isPost = _httpContext.Request.Method == ConstHelper.POST;

            //禁止访问
            var flist = _httpContext.WebConfig.ForbiddenAccessList;

            if (flist.Count > 0)
            {
                foreach (var item in flist)
                {
                    if (url.IndexOf(item.ToUpper(), StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return new ContentResult("o_o，当前内容禁止访问！", System.Net.HttpStatusCode.Forbidden);
                    }
                    if (System.Text.RegularExpressions.Regex.IsMatch(url, item))
                    {
                        return new ContentResult("o_o，当前内容禁止访问！", System.Net.HttpStatusCode.Forbidden);
                    }
                }
            }

            var arr = url.Split("/", StringSplitOptions.RemoveEmptyEntries);

            var filePath = string.Empty;

            switch (arr.Length)
            {
                case 0:

                    filePath = _httpContext.Server.MapPath(_httpContext.WebConfig.DefaultPage);

                    if (StaticResourcesCache.Exists(filePath))
                    {
                        return new FileResult(filePath, _httpContext.IsStaticsCached);
                    }
                    else
                    {
                        var d = RouteTable.Types.Where(b => string.Compare(b.Name, _httpContext.WebConfig.DefaultRout.Name, true) == 0 || string.Compare(b.Name, _httpContext.WebConfig.DefaultRout.Name + ConstHelper.CONTROLLERNAME, true) == 0).FirstOrDefault();

                        return MvcInvoke(_httpContext, routeTable, d, _httpContext.WebConfig.DefaultRout.Value, nameValues, isPost);
                    }

                case 1:
                    filePath = _httpContext.Server.MapPath(url);
                    if (StaticResourcesCache.Exists(filePath))
                    {
                        return new FileResult(filePath, _httpContext.IsStaticsCached);
                    }
                    break;

                default:
                    var controllerName = arr[arr.Length - 2];

                    var first = RouteTable.Types.Where(b => string.Compare(b.Name, controllerName + ConstHelper.CONTROLLERNAME, true) == 0).FirstOrDefault();

                    if (first != null)
                    {
                        return MvcInvoke(_httpContext, routeTable, first, arr[arr.Length - 1], nameValues, isPost);
                    }
                    else
                    {
                        filePath = _httpContext.Server.MapPath(url);
                        if (StaticResourcesCache.Exists(filePath))
                        {
                            return new FileResult(filePath, _httpContext.IsStaticsCached);
                        }
                    }
                    break;
            }
            return new ContentResult("o_o，找不到任何内容", System.Net.HttpStatusCode.NotFound);
        }

        public void Invoke(HttpContext httpContext)
        {
            _httpContext = httpContext;

            IHttpResult result;

            switch (_httpContext.Request.Method)
            {
                case ConstHelper.GET:
                case ConstHelper.POST:

                    if (_httpContext.Request.Query.Count > 0)
                    {
                        foreach (var item in _httpContext.Request.Query)
                        {
                            _httpContext.Request.Parmas[item.Key] = item.Value;
                        }
                    }
                    if (_httpContext.Request.Forms.Count > 0)
                    {
                        foreach (var item in _httpContext.Request.Forms)
                        {
                            _httpContext.Request.Parmas[item.Key] = item.Value;
                        }
                    }
                    result = GetActionResult();
                    break;
                case ConstHelper.OPTIONS:
                    result = new EmptyResult();
                    break;
                default:
                    result = new ContentResult("不支持的请求方式", System.Net.HttpStatusCode.NotImplemented);
                    break;
            }
            _httpContext.Response.SetResult(result);
            _httpContext.Response.End();
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
                        var method = arr.GetType().GetMethod(ConstHelper.ONACTIONEXECUTING);

                        if (method != null)
                        {
                            var goOn = (bool)arr.GetType().GetMethod(ConstHelper.ONACTIONEXECUTING).Invoke(arr, nargs.ToArray());

                            if (!goOn)
                            {
                                return new  ContentResult("o_o，当前逻辑已被拦截！", System.Net.HttpStatusCode.NotAcceptable);
                            }
                        }
                    }
                }

                var result = MethodInvoke(routing.Action, routing.Instance, nameValues);

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
                        var method = arr.GetType().GetMethod(ConstHelper.ONACTIONEXECUTED);
                        if (method != null)
                            method.Invoke(arr, nargs);
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("o_o，找不到"))
                {
                    return new  ContentResult(ex.Message, System.Net.HttpStatusCode.NotFound);
                }
                else
                {
                    return new  ContentResult("→_→，出错了：" + ex.Message, System.Net.HttpStatusCode.InternalServerError);
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
                result = new  ContentResult($"→_→，出错了：{obj}/{action.Name},出现异常：{ex.Message}", System.Net.HttpStatusCode.InternalServerError);
            }
            return result;
        }


    }
}
