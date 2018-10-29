/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.Http
*文件名： HttpInvoker
*版本号： V3.1.1.0
*唯一标识：eeefb8e0-9493-4a07-b469-fc24db360a1b
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/8 16:34:03
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/8 16:34:03
*修改人： yswenli
*版本号： V3.1.1.0
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Http.Common;
using SAEA.Http.Model;
using System;

namespace SAEA.Http
{
    public class HttpInvoker : IInvoker
    {
        public object Parma { get; set; }

        protected HttpContext _httpContext;

        public HttpInvoker(object parma)
        {
            this.Parma = parma;
        }

        protected IHttpResult GetActionResult()
        {
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
                        return new HttpContentResult("o_o，当前内容禁止访问！", System.Net.HttpStatusCode.Forbidden);
                    }
                    if (System.Text.RegularExpressions.Regex.IsMatch(url, item))
                    {
                        return new HttpContentResult("o_o，当前内容禁止访问！", System.Net.HttpStatusCode.Forbidden);
                    }
                }
            }
            var arr = url.Split("/", StringSplitOptions.RemoveEmptyEntries);
            var filePath = string.Empty;
            filePath = _httpContext.Server.MapPath(url);
            if (StaticResourcesCache.Exists(filePath))
            {
                return new HttpFileResult(filePath, _httpContext.IsStaticsCached);
            }
            return new HttpContentResult("o_o，找不到任何内容", System.Net.HttpStatusCode.NotFound);
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
                    result = new HttpEmptyResult();
                    break;
                default:
                    result = new HttpContentResult("不支持的请求方式", System.Net.HttpStatusCode.NotImplemented);
                    break;
            }
            _httpContext.Response.SetResult(result);
            _httpContext.Response.End();
        }
    }
}
