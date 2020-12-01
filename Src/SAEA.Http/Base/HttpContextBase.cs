/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http.Base
*文件名： HttpContextBase
*版本号： v5.0.0.1
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
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Http.Model;
using SAEA.Sockets.Interface;
using System;
using System.Net;

namespace SAEA.Http.Base
{
    /// <summary>
    /// 基础的http上下文类
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class HttpContextBase : IHttpContext
    {

        protected IWebHost _webHost;

        /// <summary>
        /// 自定义异常事件
        /// </summary>
        public abstract event ExceptionHandler OnException;

        /// <summary>
        /// 自定义http处理
        /// </summary>
        public abstract event RequestDelegate OnRequestDelegate;

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

        /// <summary>
        /// 获取当前上下文IHttpContext
        /// </summary>
        public static IHttpContext Current
        {
            get
            {
                return CallContext<IHttpContext>.GetData("ContextBase.Current");
            }
        }


        public HttpContextBase(IWebHost webHost, HttpMessage httpMessage)
        {
            _webHost = webHost;

            this.WebConfig = _webHost.WebConfig;

            this.Request = new HttpRequest();

            this.Response = new HttpResponse();

            this.Request.Init(httpMessage);

            this.Server = _webHost.HttpUtility;

            IsStaticsCached = _webHost.WebConfig.IsStaticsCached;

            CallContext<IHttpContext>.SetData("ContextBase.Current", this);
        }

        /// <summary>
        /// 初始化Session
        /// </summary>
        public void InitSession(IUserToken userToken)
        {
            string sessionID;

            if (!this.Request.Cookies.ContainsKey(ConstHelper.SESSIONID))
            {
                sessionID = HttpSessionManager.GeneratID();
            }
            else
            {
                sessionID = this.Request.Cookies[ConstHelper.SESSIONID].Value;
            }

            this.Session = HttpSessionManager.GetIfNotExistsSet(sessionID);

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

            this.Request.Headers["REMOTE_ADDR"] = ((IPEndPoint)userToken.Socket.RemoteEndPoint).Address.ToString();

            if (this.Request.Headers.ContainsKey("HTTP_X_FORWARDED_FOR"))
            {
                this.Request.Headers["HTTP_X_FORWARDED_FOR"] += "," + this.Request.Headers["REMOTE_ADDR"];
            }

            this.Response.Init(_webHost, userToken, this.Request.Protocal, _webHost.WebConfig.IsZiped);
            this.Response.Cookies[ConstHelper.SESSIONID] = new HttpCookie(ConstHelper.SESSIONID, sessionID);
        }

        public bool IsStaticsCached { get; set; }

        /// <summary>
        /// 处理业务逻辑
        /// </summary>
        /// <param name="userToken"></param>
        public abstract void HttpHandle(IUserToken userToken);

        public abstract IHttpResult GetActionResult();

    }
}
