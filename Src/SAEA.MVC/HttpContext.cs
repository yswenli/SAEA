/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MVC
*文件名： HttpContext
*版本号： v4.5.1.2
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
*版本号： v4.5.1.2
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Http.Base;
using SAEA.Http.Common;
using SAEA.Http.Model;
using System;
using System.Linq;

namespace SAEA.MVC
{
    public class HttpContext : HttpContextBase, IHttpContext
    {
        RouteTable _routeTable = null;

        public HttpContext(IWebHost webHost, HttpMessage httpMessage) : base(webHost, httpMessage)
        {
            _routeTable = _webHost.RouteParam as RouteTable;
        }

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
                        return new FileResult(filePath, IsStaticsCached);
                    }
                    else
                    {
                        var d = RouteTable.Types.Where(b => (string.Compare(b.Name, WebConfig.DefaultRoute.Name, true) == 0 || string.Compare(b.Name, WebConfig.DefaultRoute.Name + ConstHelper.CONTROLLERNAME, true) == 0)).FirstOrDefault();

                        nameValues = Request.Parmas.ToNameValueCollection();

                        return MvcInvoker.InvokeResult(this, _routeTable, d, WebConfig.DefaultRoute.Value, nameValues, isPost);
                    }

                case 1:
                    filePath = Server.MapPath(url);
                    if (StaticResourcesCache.Exists(filePath))
                    {
                        return new FileResult(filePath, IsStaticsCached);
                    }
                    break;

                default:
                    var controllerName = arr[arr.Length - 2];

                    var first = RouteTable.Types.Where(b => string.Compare(b.Name, controllerName + ConstHelper.CONTROLLERNAME, true) == 0).FirstOrDefault();

                    if (first != null)
                    {
                        nameValues = Request.Parmas.ToNameValueCollection();

                        return MvcInvoker.InvokeResult(this, _routeTable, first, arr[arr.Length - 1], nameValues, isPost);
                    }
                    else
                    {
                        filePath = Server.MapPath(url);
                        if (StaticResourcesCache.Exists(filePath))
                        {
                            return new FileResult(filePath, IsStaticsCached);
                        }
                    }
                    break;
            }


            return new ContentResult("o_o，找不到任何内容 url:" + url, System.Net.HttpStatusCode.NotFound);
        }
    }
}
