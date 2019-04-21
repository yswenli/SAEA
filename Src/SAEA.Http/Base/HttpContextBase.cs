/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http.Base
*文件名： HttpContextBase
*版本号： v4.5.1.2
*唯一标识：af0b65c6-0f58-4221-9e52-7e3f0a4ffb24
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/4/21 16:46:31
*描述：
*
*=====================================================================
*修改标记
*修改时间：2019/4/21 16:46:31
*修改人： yswenli
*版本号： v4.5.1.2
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Http.Common;
using SAEA.Http.Model;
using SAEA.Sockets.Interface;
using System;

namespace SAEA.Http.Base
{
    /// <summary>
    /// 基础的http上下文类
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class HttpContextBase : IHttpContext
    {

        protected IWebHost _webHost;

        public HttpRequest Request
        {
            get;
            private set;
        }

        public HttpResponse Response
        {
            get;
            private set;
        }

        public HttpUtility Server
        {
            get;
            private set;
        }

        public HttpSession Session
        {
            get;
            private set;
        }

        public WebConfig WebConfig { get; set; }


        public HttpContextBase(IWebHost webHost, HttpMessage httpMessage)
        {
            _webHost = webHost;

            this.WebConfig = _webHost.WebConfig;

            this.Request = new HttpRequest();

            this.Response = new HttpResponse();

            this.Request.Init(httpMessage);

            this.Response.Init(_webHost, this.Request.Protocal, _webHost.WebConfig.IsZiped);

            this.Server = _webHost.HttpUtility;

            IsStaticsCached = _webHost.WebConfig.IsStaticsCached;
        }

        /// <summary>
        /// 初始化Session
        /// </summary>
        private void InitSession(IUserToken userToken)
        {
            var sessionID = string.Empty;

            if (!this.Request.Cookies.ContainsKey(ConstHelper.SESSIONID))
            {
                sessionID = HttpSessionManager.GeneratID();
            }
            else
            {
                sessionID = this.Request.Cookies[ConstHelper.SESSIONID].Value;
            }

            this.Session = HttpSessionManager.SetAndGet(sessionID);

            var domain = userToken.ID;

            if (this.Request.Headers.ContainsKey("Host"))
            {
                domain = this.Request.Headers["Host"];

                if (domain.IndexOf("www.", StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    domain = StringHelper.Substring(domain, 3);
                }

                HttpCookie.DefaultDomain = domain;
            }

            this.Response.Cookies[ConstHelper.SESSIONID] = new HttpCookie(ConstHelper.SESSIONID, sessionID);
        }

        public bool IsStaticsCached { get; set; }

        /// <summary>
        /// 处理业务逻辑
        /// </summary>
        /// <param name="userToken"></param>
        public void HttpHandle(IUserToken userToken)
        {
            IHttpResult result;

            this.InitSession(userToken);

            switch (this.Request.Method)
            {
                case ConstHelper.GET:
                case ConstHelper.POST:

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
                    result = GetActionResult();
                    break;
                case ConstHelper.OPTIONS:
                    result = new HttpEmptyResult();
                    break;
                default:
                    result = new HttpContentResult("不支持的请求方式", System.Net.HttpStatusCode.NotImplemented);
                    break;
            }

            Response.SetResult(result, this.Session.CacheCalcResult);

            Response.End(userToken);
        }

        public virtual IHttpResult GetActionResult()
        {
            string url = Request.Url;

            bool isPost = Request.Method == ConstHelper.POST;

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
                return new HttpFileResult(filePath, IsStaticsCached);
            }
            return new HttpContentResult("o_o，找不到任何内容", System.Net.HttpStatusCode.NotFound);
        }
    }
}
