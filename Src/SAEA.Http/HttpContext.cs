/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http
*文件名： HttpContext
*版本号： v6.0.0.1
*唯一标识：af0b65c6-0f58-4221-9e52-7e3f0a4ffb24
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/10 16:46:31
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/10 16:46:31
*修改人： yswenli
*版本号： v6.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Http.Base;
using SAEA.Http.Common;
using SAEA.Http.Model;
using SAEA.Sockets.Interface;
using System;

namespace SAEA.Http
{
    /// <summary>
    /// SAEA.Http http上下文
    /// </summary>
    internal class HttpContext : HttpContextBase, IHttpContext
    {
        /// <summary>
        /// 自定义异常事件
        /// </summary>
        public override event ExceptionHandler OnException;

        /// <summary>
        /// 自定义http处理
        /// </summary>
        public override event RequestDelegate OnRequestDelegate;


        public HttpContext(IWebHost webHost, HttpMessage httpMessage) : base(webHost, httpMessage)
        {

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

                switch (Request.Method)
                {
                    case ConstHelper.GET:
                    case ConstHelper.POST:
                    case ConstHelper.PUT:
                    case ConstHelper.DELETE:

                        if (Request.Query.Count > 0)
                        {
                            foreach (var item in Request.Query)
                            {
                                Request.Parmas[item.Key] = item.Value;
                            }
                        }
                        if (Request.Forms.Count > 0)
                        {
                            foreach (var item in Request.Forms)
                            {
                                Request.Parmas[item.Key] = item.Value;
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
                        result = new HttpEmptyResult();
                        break;
                    default:
                        result = new HttpContentResult("不支持的请求方式", System.Net.HttpStatusCode.HttpVersionNotSupported);
                        break;
                }
            }
            catch (Exception ex)
            {
                result = OnException?.Invoke(this, ex);
            }

            if (result != null)
            {
                if (!(result is IBigDataResult))
                {
                    if (result is IFileResult && _webHost.WebConfig.IsStaticsCached)
                    {
                        Response.SetCached(result, "60,60");
                    }
                    else
                    {
                        Response.SetCached(result, this.Session.CacheCalcString);
                    }
                    Response.End();
                }
            }
        }
        public override IHttpResult GetActionResult()
        {
            string url = Request.Url;

            //禁止访问
            var flist = WebConfig.ForbiddenAccessList;

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

            if (url == "/")
            {
                url = "/index.html";
            }

            var filePath = Server.MapPath(url);

            if (StaticResourcesCache.Exists(filePath))
            {
                if (StaticResourcesCache.IsBigFile(filePath))
                {
                    return new HttpBigDataResult(filePath);
                }
                else
                {
                    return new HttpFileResult(filePath, IsStaticsCached);
                }
            }
            return new HttpContentResult("o_o，找不到任何内容", System.Net.HttpStatusCode.NotFound);
        }
    }
}
