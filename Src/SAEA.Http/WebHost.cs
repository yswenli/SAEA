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
*唯一标识：e59016b9-6d64-4c89-8826-f139a38be098
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/28 14:19:50
*描述：WebHost类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:29
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
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
    /// SAEA Web 主机，组装 HttpSocket、WebConfig 与 HttpUtility 并对外提供收发能力
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
        ///  Web 服务器配置
        /// </summary>
        public WebConfig WebConfig { get; set; }

        /// <summary>
        /// Http 服务器实用工具，负责静态资源路径映射
        /// </summary>
        public HttpUtility HttpUtility
        {
            get;
            private set;
        }

        /// <summary>
        /// 路由扩展参数，由业务自行解释
        /// </summary>
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
        /// 底层连接断开事件（sessionId, 异常）
        /// </summary>
        public event Action<string, Exception> OnDisconnected;


        /// <summary>
        /// 创建 Web 主机，按参数初始化配置、静态资源工具与底层 socket
        /// </summary>
        /// <param name="httpContentType">自定义 IHttpContext 实现类型，null 或不合规时用内置 HttpContext</param>
        /// <param name="root">站点根目录名，默认为 wwwroot</param>
        /// <param name="port">监听端口，默认 39654</param>
        /// <param name="isStaticsCached">是否启用静态资源缓存</param>
        /// <param name="isZiped">是否启用 gzip 内容压缩</param>
        /// <param name="bufferSize">接收缓冲区字节数，默认 64KB</param>
        /// <param name="maxConnects">最大连接数，默认 1000</param>
        /// <param name="timeout">连接空闲超时秒数，默认 180</param>
        /// <param name="connectTimeout">连接建立超时秒数，默认 3</param>
        /// <param name="isDebug">是否使用调试版 socket 实现</param>
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
            _httpServer.OnDisconnected += (id, ex) => OnDisconnected?.Invoke(id, ex);
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
            // 已升级为 WebSocket 的连接：不再按 HTTP 请求分发
            // （同一 TCP 段里的第二个请求会在第一个请求升级之后才被分发，必须跳过）
            if ((userToken?.Coder as HttpCoder)?.IsRaw == true) return;

            var httpContext = (IHttpContext)Activator.CreateInstance(_httpContentType, this);

            try
            {
                if (OnException != null)
                    httpContext.OnException += HttpContext_OnException;

                if (OnRequestDelegate != null)
                    httpContext.OnRequestDelegate += HttpContext_OnRequestDelegate;

                httpContext.HttpHandle(userToken, httpMessage);
            }
            finally
            {
                // 已升级的上下文由 WebSocket 连接负责释放（HttpContextBase.Dispose 只清 Request/Response）
                if (!httpContext.Response.IsUpgraded)
                    httpContext.Dispose();
            }
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
        /// 设置/取消会话的空闲超时豁免
        /// </summary>
        /// <param name="id">会话ID</param>
        /// <param name="noTimeout">true 表示不参与空闲清理</param>
        public void SetNoTimeout(string id, bool noTimeout)
        {
            _httpServer.SetNoTimeout(id, noTimeout);
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