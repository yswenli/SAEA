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
*命名空间：SAEA.Http
*文件名： WebHost
*版本号： v26.4.23.1
*唯一标识：9e8fa79e-3f38-41c7-af96-7648ce0cf679
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/28 14:19:50
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/10/28 14:19:50
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;

using SAEA.Common;
using SAEA.Http.Base;
using SAEA.Http.Base.Net;
using SAEA.Http.Model;
using SAEA.Sockets.Interface;

namespace SAEA.Http
{
    /// <summary>
    /// SAEA WebServer
    /// </summary>
    public class WebHost : IWebHost
    {
        IHttpSocket _httpServer;

        Type _httpContentType = typeof(HttpContext);

        /// <summary>
        /// 是否已启动
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        ///  SAEA WebConfig
        /// </summary>
        public WebConfig WebConfig { get; set; }

        public HttpUtility HttpUtility
        {
            get;
            private set;
        }

        public object RouteParam { get; set; }



        /// <summary>
        /// 自定义异常事件
        /// </summary>
        public event ExceptionHandler OnException;

        /// <summary>
        /// 自定义http处理
        /// </summary>
        public event RequestDelegate OnRequestDelegate;


        /// <summary>
        /// WebHost
        /// </summary>
        /// <param name="httpContentType"></param>
        /// <param name="root"></param>
        /// <param name="port"></param>
        /// <param name="isStaticsCached"></param>
        /// <param name="isZiped"></param>
        /// <param name="bufferSize"></param>
        /// <param name="maxConnects"></param>
        /// <param name="timeout"></param>
        /// <param name="connectTimeout"></param>
        /// <param name="isDebug"></param>
        public WebHost(Type httpContentType = null,
            string root = "wwwroot",
            int port = 39654,
            bool isStaticsCached = true,
            bool isZiped = true,
            int bufferSize = 64 * 1024,
            int maxConnects = 1000,
            double timeout = 180,
            double connectTimeout = 3,
            bool isDebug = false)
        {
            if (httpContentType != null && _httpContentType.GetInterface("SAEA.Http.Model.IHttpContext", true) != null)
            {
                _httpContentType = httpContentType;
            }
            else
            {
                _httpContentType = typeof(HttpContext);
            }

            WebConfig = new WebConfig()
            {
                Root = root,
                Port = port,
                IsStaticsCached = isStaticsCached,
                IsZiped = isZiped,
                HandleBufferSize = bufferSize,
                MaxConnects = maxConnects,
                Timeout = timeout
            };

            HttpUtility = new HttpUtility(WebConfig.Root);

            if (isDebug)

                _httpServer = new HttpSocketDebug(port, bufferSize, maxConnects, timeout, connectTimeout);

            else

                _httpServer = new HttpSocket(port, bufferSize, maxConnects, timeout, connectTimeout);

            _httpServer.OnRequested += _serverSocket_OnRequested;
            _httpServer.OnError += _httpServer_OnError;
        }

        private void _httpServer_OnError(Exception ex)
        {
            var httpContext = HttpContext.Current;
            if (httpContext != null && OnException != null)
            {
                OnException.Invoke(HttpContext.Current, ex);
            }
            else
            {
                LogHelper.Error("httpServer_OnError", ex);
            }
        }


        /// <summary>
        /// 启动服务
        /// </summary>
        public void Start()
        {
            if (!IsRunning)
            {
                _httpServer.Start();
                IsRunning = true;
            }
        }


        /// <summary>
        /// 设置需要跨域的自定义headers
        /// </summary>
        /// <param name="headers"></param>
        public void SetCrossDomainHeaders(params string[] headers)
        {
            ConstHelper.SetCrossDomainHeaders(headers);
        }
        /// <summary>
        /// 设置需要跨域的自定义headers
        /// </summary>
        /// <param name="headers"></param>
        public void SetCrossDomainHeaders(string headers)
        {
            ConstHelper.SetCrossDomainHeaders(headers);
        }

        /// <summary>
        /// 处理http请求数据
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="httpMessage"></param>
        private void _serverSocket_OnRequested(IUserToken userToken, HttpMessage httpMessage)
        {
            using IHttpContext httpContext = (IHttpContext)Activator.CreateInstance(_httpContentType, this);

            if (OnException != null)
                httpContext.OnException += HttpContext_OnException;

            if (OnRequestDelegate != null)
                httpContext.OnRequestDelegate += HttpContext_OnRequestDelegate;

            httpContext.HttpHandle(userToken, httpMessage);
        }

        private void HttpContext_OnRequestDelegate(IHttpContext context)
        {
            OnRequestDelegate?.Invoke(context);
        }

        private IHttpResult HttpContext_OnException(IHttpContext httpContext, Exception ex)
        {
            return OnException?.Invoke(httpContext, ex);
        }

        /// <summary>
        /// 发送
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="data"></param>
        public void Send(IUserToken userToken, byte[] data)
        {
            _httpServer.Send(userToken, data);
        }

        /// <summary>
        /// 结束
        /// </summary>
        /// <param name="userToken"></param>
        public void Disconnect(IUserToken userToken)
        {
            _httpServer.Disconnecte(userToken);
        }

        /// <summary>
        /// 发送并结束
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="data"></param>
        public void End(IUserToken userToken, byte[] data)
        {
            _httpServer.End(userToken, data);
        }

        /// <summary>
        /// 重启
        /// </summary>
        public void Restart()
        {
            this.Stop();
            this.Start();
        }

        /// <summary>
        /// 关闭服务
        /// </summary>
        public void Stop()
        {
            if (IsRunning)
            {
                _httpServer.Stop();
                IsRunning = false;
            }
        }


    }
}