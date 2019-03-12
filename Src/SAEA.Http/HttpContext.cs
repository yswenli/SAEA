/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http
*文件名： HttpContext
*版本号： v4.2.3.1
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
*版本号： v4.2.3.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Http.Base;
using SAEA.Http.Model;
using SAEA.Sockets.Interface;
using System;

namespace SAEA.Http
{
    /// <summary>
    /// SAEA.Http http上下文
    /// </summary>
    public class HttpContext : IDisposable
    {

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

        internal IInvoker Invoker { get; private set; }

        public WebConfig WebConfig { get; set; }


        internal HttpContext(IWebHost webHost, HttpMessage httpMessage)
        {
            this.WebConfig = webHost.WebConfig;

            this.Invoker = webHost.Invoker;

            this.Request = new HttpRequest();

            this.Response = new HttpResponse();

            this.Request.Init(httpMessage);

            this.Response.Init(webHost, this.Request.Protocal, webHost.WebConfig.IsZiped);

            this.Server = new HttpUtility(webHost.WebConfig.Root);

            IsStaticsCached = webHost.WebConfig.IsStaticsCached;
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

                if (domain.IndexOf("www.") == 0)
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
                    result = this.Invoker.GetActionResult(this);
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

        public void Dispose()
        {
            this.Response.Dispose();
            this.Request.Dispose();
            this.Invoker = null;
            this.WebConfig = null;
            this.Response = null;
            this.Request = null;
        }
    }
}
