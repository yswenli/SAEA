/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http.Web
*文件名： WebHost
*版本号： v4.5.6.7
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
*版本号： v4.5.6.7
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Http.Base;
using SAEA.Http.Base.Net;
using SAEA.Http.Model;
using SAEA.Sockets.Interface;
using System;

namespace SAEA.Http
{
    /// <summary>
    /// SAEA WebServer
    /// </summary>
    public class WebHost : IWebHost
    {
        HttpSocket _serverSocket;

        /// <summary>
        /// 是否已启动
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        ///  SAEA WebConfig
        /// </summary>
        public WebConfig WebConfig { get; set; }


        public IInvoker Invoker { get; private set; }

        /// <summary>
        /// SAEA WebServer
        /// </summary>
        /// <param name="invoker">处理对象</param>
        /// <param name="root">根目录</param>
        /// <param name="port">监听端口</param>
        /// <param name="isStaticsCached">是否启用静态缓存</param>
        /// <param name="isZiped">是压启用内容压缩</param>
        /// <param name="bufferSize">http处理数据缓存大小</param>
        /// <param name="count">http连接数上限</param>
        /// <param name="timeOut">超时</param>
        /// <param name="isDebug">测试模式</param>
        public WebHost(IInvoker invoker, string root = "wwwroot", int port = 39654, bool isStaticsCached = true, bool isZiped = true, int bufferSize = 1024 * 10, int count = 10000, int timeOut = 120 * 1000, bool isDebug = false)
        {
            Invoker = invoker;

            WebConfig = new WebConfig()
            {
                Root = root,
                Port = port,
                IsStaticsCached = isStaticsCached,
                IsZiped = isZiped,
                HandleBufferSize = bufferSize,
                ClientCounts = count
            };

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
        /// 处理http请求数据
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="httpMessage"></param>
        private void _serverSocket_OnRequested(IUserToken userToken, HttpMessage httpMessage)
        {
            try
            {
                var httpContext = HttpContext.Create(this, httpMessage);

                httpContext.HttpHandle(userToken);
            }
            catch (Exception ex)
            {
                LogHelper.Error("WebHost._serverSocket_OnDisconnected 意外断开连接", ex);
            }
        }

        public void End(IUserToken userToken, byte[] data)
        {
            _serverSocket.End(userToken, data);
        }


        private void _serverSocket_OnError(string ID, Exception ex)
        {
            LogHelper.Error(ID, ex);
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
