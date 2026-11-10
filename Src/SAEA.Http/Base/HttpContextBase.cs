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
*命名空间：SAEA.Http.Base
*文件名： HttpContextBase
*版本号： v26.4.23.1
*唯一标识：ef9a4d2e-284e-469e-9241-86d3be19ff02
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/04/21 23:58:08
*描述：HttpContextBase类
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 10:40:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：删除异常与请求委托事件，处理入口改按连接通道，远端地址取自连接会话
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Http.Base.Net;
using SAEA.Http.Model;
using SAEA.Sockets.Pipeline;

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
        /// <summary>
        /// 所属的 Web 主机，构造时注入
        /// </summary>
        protected IWebHost _webHost;

        /// <summary>
        /// 获取Http请求对象
        /// </summary>
        public HttpRequest Request
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取Http响应对象
        /// </summary>
        public HttpResponse Response
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取Http服务器实用工具对象
        /// </summary>
        public HttpUtility Server
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取Http会话对象
        /// </summary>
        public HttpSession Session
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取或设置Web服务器配置
        /// </summary>
        public WebConfig WebConfig { get; set; }

        /// <summary>
        /// 获取或设置是否启用静态缓存
        /// </summary>
        public bool IsStaticsCached { get; set; }

        IWebSocketsFeature _webSockets;

        /// <summary>
        /// 获取 WebSocket 能力入口。默认实现不支持升级，需要 WebSocket 的上下文请覆盖本属性
        /// </summary>
        public virtual IWebSocketsFeature WebSockets
        {
            get
            {
                if (_webSockets == null)
                    _webSockets = new NotSupportedWebSocketsFeature(this);
                return _webSockets;
            }
        }

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

        /// <summary>
        /// 基础的http上下文类
        /// </summary>
        /// <param name="webHost">Web主机对象</param>
        public HttpContextBase(IWebHost webHost)
        {
            _webHost = webHost;

            this.WebConfig = _webHost.WebConfig;

            this.Request = new HttpRequest();

            this.Response = new HttpResponse();

            this.Server = _webHost.HttpUtility;

            IsStaticsCached = _webHost.WebConfig.IsStaticsCached;

            CallContext<IHttpContext>.SetData("ContextBase.Current", this);
        }

        /// <summary>
        /// 处理业务逻辑
        /// </summary>
        /// <param name="channel">连接通道</param>
        /// <param name="httpMessage">Http消息</param>
        public virtual void HttpHandle(IChannel channel, HttpMessage httpMessage)
        {
            Request.Init(httpMessage);

            string sessionID;

            if (!Request.Cookies.ContainsKey(ConstHelper.SESSIONID))
            {
                sessionID = HttpSessionManager.GeneratID();
            }
            else
            {
                sessionID = Request.Cookies[ConstHelper.SESSIONID].Value;
            }

            this.Session = HttpSessionManager.GetIfNotExistsSet(sessionID);

            var domain = channel == null ? string.Empty : channel.Id;

            if (this.Request.Headers.ContainsKey("Host"))
            {
                domain = Request.Headers["Host"];

                if (domain.IndexOf("www.", StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    domain = StringHelper.Substring(domain, 3);
                }

                HttpCookie.DefaultDomain = domain;
            }

            var clientSocket = GetClientSocket(channel);

            if (clientSocket != null && clientSocket.Connected)
            {
                Request.Headers["REMOTE_ADDR"] = ((IPEndPoint)clientSocket.RemoteEndPoint).Address.ToString();

                if (Request.Headers.ContainsKey("HTTP_X_FORWARDED_FOR"))
                {
                    Request.Headers["HTTP_X_FORWARDED_FOR"] += "," + this.Request.Headers["REMOTE_ADDR"];
                }

                Response.Init(_webHost, channel, Request.Protocal, _webHost.WebConfig.IsZiped);
                Response.Cookies[ConstHelper.SESSIONID] = new HttpCookie(ConstHelper.SESSIONID, sessionID);
            }
        }

        /// <summary>
        /// 取连接底层的客户端 socket；连接尚未登记或已回收时返回 null。
        /// </summary>
        /// <param name="channel">连接通道</param>
        /// <returns>客户端 socket；不可用时为 null</returns>
        private static System.Net.Sockets.Socket GetClientSocket(IChannel channel)
        {
            var session = HttpConnectionSession.TryGet(channel);
            if (session == null || session.Info == null)
                return null;

            return session.Info.ClientSocket;
        }

        /// <summary>
        /// 获取操作结果
        /// </summary>
        /// <returns>操作结果</returns>
        public abstract IHttpResult GetActionResult();

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Request.Dispose();
            Response.Dispose();
        }
    }
}