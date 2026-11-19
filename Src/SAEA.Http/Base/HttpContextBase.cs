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
*修改时间：2026/11/19 20:10:00
*修改人： yswenli
*版本号： v26.11.16.1
*描述：新增 ResetForNextRequest 供可复用上下文复位；CallContext 改为每请求在 InitContext 里设置
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Http.Base.Net;
using SAEA.Http.Model;
using SAEA.Sockets.Pipeline;

using System;
using System.Net;
using System.Threading.Tasks;

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
        }

        /// <summary>
        /// 处理业务逻辑
        /// </summary>
        /// <param name="channel">连接通道</param>
        /// <param name="httpMessage">Http消息</param>
        public virtual void HttpHandle(IChannel channel, HttpMessage httpMessage)
        {
            InitContext(channel, httpMessage);
        }

        /// <summary>
        /// 异步处理业务逻辑；默认实现同步调用 <see cref="HttpHandle"/> 并返回已完成任务，
        /// 只有真正需要异步收尾的派生类才覆盖本方法，只重写 <see cref="HttpHandle"/> 的既有实现不受影响。
        /// </summary>
        /// <param name="channel">连接通道</param>
        /// <param name="httpMessage">Http消息</param>
        /// <returns>处理完成后的任务</returns>
        public virtual Task HttpHandleAsync(IChannel channel, HttpMessage httpMessage)
        {
            HttpHandle(channel, httpMessage);

            return Task.CompletedTask;
        }

        /// <summary>
        /// 初始化本次请求的 Request、Session 与 Response
        /// </summary>
        /// <param name="channel">连接通道</param>
        /// <param name="httpMessage">Http消息</param>
        protected void InitContext(IChannel channel, HttpMessage httpMessage)
        {
            // 上下文可能跨请求复用，Current 必须每请求重新绑定到当前异步流；放在构造函数里对复用实例不会再执行
            CallContext<IHttpContext>.SetData("ContextBase.Current", this);

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
        /// 异步获取操作结果；默认实现直接取同步结果，需要真正异步的派生类覆盖本方法
        /// </summary>
        /// <returns>业务处理结果</returns>
        public virtual Task<IHttpResult> GetActionResultAsync()
        {
            return Task.FromResult(GetActionResult());
        }

        /// <summary>
        /// 复位上下文，使同一个实例可以处理本连接的下一条请求。
        /// <para>
        /// 只有实现了 <see cref="SAEA.Http.Model.IReusableHttpContext"/> 的上下文会被框架复用并调用本方法；
        /// 复位后的状态必须与新建实例一致，否则上一次请求的数据会串到下一次请求。
        /// </para>
        /// </summary>
        public virtual void ResetForNextRequest()
        {
            Request.ResetForNextRequest();

            Response.ResetForNextRequest();

            // 新建实例的这两个属性都取自宿主，复位时按同一规则重新读取
            Session = null;

            WebConfig = _webHost.WebConfig;

            IsStaticsCached = _webHost.WebConfig.IsStaticsCached;
        }

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