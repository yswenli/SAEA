/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MVC
*文件名： HttpContext
*版本号： v6.0.0.1
*唯一标识：e00bb57f-e3ee-4efe-a7cf-f23db767c1d0
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/4/21 16:43:26
*描述：
*
*=====================================================================
*修改标记
*修改时间：2019/4/21 16:43:26
*修改人： yswenli
*版本号： v6.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.NameValue;
using SAEA.Http;
using SAEA.Http.Base;
using SAEA.Http.Common;
using SAEA.Http.Model;
using SAEA.Sockets.Interface;
using System;
using System.Linq;

namespace SAEA.MVC
{
    /// <summary>
    /// mvc HttpContext
    /// </summary>
    public class HttpContext : HttpContextBase, IHttpContext
    {
        RouteTable _routeTable = null;

        /// <summary>
        /// 自定义异常事件
        /// </summary>
        public override event ExceptionHandler OnException;

        /// <summary>
        /// 自定义http处理
        /// </summary>
        public override event RequestDelegate OnRequestDelegate;

        /// <summary>
        /// mvc HttpContext
        /// </summary>
        /// <param name="webHost"></param>
        /// <param name="httpMessage"></param>
        public HttpContext(IWebHost webHost, HttpMessage httpMessage) : base(webHost, httpMessage)
        {
            _routeTable = _webHost.RouteParam as RouteTable;
        }

        /// <summary>
        /// 处理业务逻辑
        /// </summary>
        /// <param name="userToken"></param>
        public override void HttpHandle(IUserToken userToken)
        {
            IHttpResult result = null;

            try
            {
                this.InitSession(userToken);

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
                        if (OnRequestDelegate != null)
                        {
                            OnRequestDelegate.Invoke(this);
                        }
                        else
                        {
                            result = GetActionResult();
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
                result = OnException.Invoke(this, ex);

                if (result == null)
                {
                    result = new ContentResult("请求发生异常：" + ex.Message, System.Net.HttpStatusCode.InternalServerError);
                }
            }

            if (result != null && !(result is IBigDataResult || result is IEventStream))
            {
                Response.SetCached(result, this.Session.CacheCalcString);

                Response.End();
            }
        }

        /// <summary>
        /// 重新获取结果方法
        /// </summary>
        /// <returns></returns>
        public override IHttpResult GetActionResult()
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

            var arr = url.Split("/", StringSplitOptions.RemoveEmptyEntries);

            var filePath = string.Empty;

            NameValueCollection nameValues;

            switch (arr.Length)
            {
                case 0:

                    filePath = Server.MapPath(WebConfig.HomePage);

                    if (string.IsNullOrWhiteSpace(filePath))
                    {
                        new ContentResult($"o_o，找不到任何内容 url:{url} filePath:{filePath}", System.Net.HttpStatusCode.NotFound);
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

                        return MvcInvoker.InvokeResult(_routeTable, d, WebConfig.DefaultRoute.Value, nameValues, isPost);
                    }

                case 1:
                    filePath = Server.MapPath(url);
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

                            return MvcInvoker.InvokeResult(_routeTable, first, arr[arr.Length - 1], nameValues, isPost);
                        }
                    }

                    filePath = Server.MapPath(url);

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
