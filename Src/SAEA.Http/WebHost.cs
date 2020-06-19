/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http.Web
*文件名： WebHost
*版本号： v5.0.0.1
*唯一标识：340c3ef0-2e98-4f25-998f-2bb369fa2794
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/10/12 00:48:06
*描述：
*
*=====================================================================
*修改标记
*创建时间：2018/10/12 00:48:06
*修改人： yswenli
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Http.Base;
using SAEA.Http.Base.Net;
using SAEA.Http.Model;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;

namespace SAEA.Http
{
    /// <summary>
    /// SAEA WebServer
    /// </summary>
    public class WebHost : IWebHost
    {
        HttpSocket _serverSocket;

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
        /// SAEA WebServer
        /// </summary>
        /// <param name="httpContentType">处理对象</param>
        /// <param name="root">根目录</param>
        /// <param name="port">监听端口</param>
        /// <param name="isStaticsCached">是否启用静态缓存</param>
        /// <param name="isZiped">是压启用内容压缩</param>
        /// <param name="bufferSize">http处理数据缓存大小</param>
        /// <param name="count">http连接数上限</param>
        /// <param name="timeOut">超时</param>
        /// <param name="isDebug">测试模式</param>
        public WebHost(Type httpContentType = null, string root = "wwwroot", int port = 39654, bool isStaticsCached = true, bool isZiped = true, int bufferSize = 1024 * 10, int count = 10000, int timeOut = 120 * 1000, bool isDebug = false)
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
                ClientCounts = count
            };

            HttpUtility = new HttpUtility(WebConfig.Root);

            _serverSocket = new HttpSocket(port, bufferSize, count, timeOut, isDebug);

            _serverSocket.OnRequested += _serverSocket_OnRequested;
        }



        /// <summary>
        /// 启动服务
        /// </summary>
        public void Start()
        {
            if (!IsRunning)
            {
                _serverSocket.Start();
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
            try
            {
                var httpContext = (IHttpContext)Activator.CreateInstance(_httpContentType, this, httpMessage);

                httpContext.HttpHandle(userToken);

            }
            catch (Exception ex)
            {
                LogHelper.Error("WebHost._serverSocket_OnDisconnected 意外断开连接", ex, httpMessage);
            }
        }

        /// <summary>
        /// 发送
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="data"></param>
        public void Send(IUserToken userToken, byte[] data)
        {
            _serverSocket.Send(userToken, data);
        }

        /// <summary>
        /// 结束
        /// </summary>
        /// <param name="userToken"></param>
        public void Disconnect(IUserToken userToken)
        {
            _serverSocket.Disconnecte(userToken);
        }

        /// <summary>
        /// 发送并结束
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="data"></param>
        public void End(IUserToken userToken, byte[] data)
        {
            _serverSocket.End(userToken, data);
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
                _serverSocket.Stop();
                IsRunning = false;
            }
        }


    }
}
