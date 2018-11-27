/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.Http.Web
*文件名： WebHost
*版本号： V3.3.3.4
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
*版本号： V3.3.3.4
*描述：
*
*****************************************************************************/

using SAEA.Http.Base;
using SAEA.Http.Base.Net;
using SAEA.Http.Common;
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
        public WebHost(IInvoker invoker, string root = "/html/", int port = 39654, bool isStaticsCached = true, bool isZiped = true, int bufferSize = 1024 * 10, int count = 10000)
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

            _serverSocket = new HttpSocket(bufferSize, count);

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
                _serverSocket.Start(WebConfig.Port);
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
                new HttpContext(this, userToken, httpMessage).HttpHandle();
            }
            catch (Exception ex)
            {
                LogHelper.WriteError("_serverSocket_OnDisconnected 断开连接", ex);
                _serverSocket.Disconnect(userToken, ex);
            }
        }

        public void End(IUserToken userToken, byte[] data)
        {
            _serverSocket.End(userToken, data);
        }

        internal void Close(IUserToken userToken)
        {
            _serverSocket.Disconnect(userToken);
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
