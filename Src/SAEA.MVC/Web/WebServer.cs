using SAEA.MVC.Common;
using SAEA.MVC.Http;
using SAEA.MVC.Http.Base;
using SAEA.MVC.Http.Model;
using SAEA.MVC.Http.Net;
using SAEA.Sockets.Interface;
using System;

namespace SAEA.MVC.Web
{
    /// <summary>
    /// SAEA WebServer
    /// </summary>
    internal class WebServer
    {
        ServerSocket _serverSocket;

        /// <summary>
        /// 是否已启动
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        ///  SAEA WebServerConfig
        /// </summary>
        public WebServerConfig WebServerConfig { get; set; }

        /// <summary>
        /// SAEA WebServer
        /// </summary>
        /// <param name="root">根目录</param>
        /// <param name="port">监听端口</param>
        /// <param name="isStaticsCached">是否启用静态缓存</param>
        /// <param name="isZiped">是压启用内容压缩</param>
        /// <param name="bufferSize">http处理数据缓存大小</param>
        /// <param name="count">http连接数上限</param>
        public WebServer(string root = "/html/", int port = 39654, bool isStaticsCached = true, bool isZiped = true, int bufferSize = 1024 * 100, int count = 10000)
        {
            WebServerConfig = new WebServerConfig()
            {
                Root = root,
                Port = port,
                IsStaticsCached = isStaticsCached,
                IsZiped = isZiped,
                HandleBufferSize = bufferSize,
                ClientCounts = count
            };

            _serverSocket = new ServerSocket(bufferSize, count);

            _serverSocket.OnDisconnected += _serverSocket_OnDisconnected;

            _serverSocket.OnRequested += _serverSocket_OnRequested;

            _serverSocket.OnError += _serverSocket_OnError;
        }

        /// <summary>
        /// 启动服务
        /// </summary>
        public void Start()
        {
            if (!IsRunning)
            {
                _serverSocket.Start(WebServerConfig.Port);
                IsRunning = true;
            }
            
        }

        /// <summary>
        /// 处理http请求数据
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="requestDataReader"></param>
        private void _serverSocket_OnRequested(IUserToken userToken, RequestDataReader requestDataReader)
        {
            try
            {
                using (var httpContext = new HttpContext())
                {
                    httpContext.Init(this, userToken, requestDataReader, WebServerConfig.Root, WebServerConfig.IsZiped);

                    httpContext.HttpHandler();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteError("_serverSocket_OnDisconnected 断开连接", ex);
                _serverSocket.Disconnect(userToken, ex);
            }
        }

        internal void Reponse(IUserToken userToken, byte[] data)
        {
            _serverSocket.Reply(userToken, data);
        }

        internal void Close(IUserToken userToken)
        {
            _serverSocket.Disconnect(userToken);
        }


        private void _serverSocket_OnDisconnected(string ID, System.Exception ex)
        {
            if (ex != null)
            {
                if (ex.Message.IndexOf("远程连接已关闭", StringComparison.Ordinal) == 0)
                {
                    return;
                }
                LogHelper.WriteError("_serverSocket_OnDisconnected 断开连接", ex);
            }

        }

        private void _serverSocket_OnError(string ID, Exception ex)
        {
            LogHelper.WriteError(ID, ex);
        }


        /// <summary>
        /// 关闭服务
        /// </summary>
        public void Stop()
        {
            if (IsRunning)
            {
                _serverSocket.Stop();
                IsRunning = false;
            }            
        }


    }
}
