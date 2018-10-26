/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.MVC.Web
*文件名： WebHost
*版本号： V2.2.2.1
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
*版本号： V2.2.2.1
*描述：
*
*****************************************************************************/

using SAEA.MVC.Base.Net;
using SAEA.MVC.Common;
using SAEA.MVC.Model;
using SAEA.Sockets.Interface;
using System;

namespace SAEA.MVC.Hosting
{
    /// <summary>
    /// SAEA WebServer
    /// </summary>
    internal class WebHost : IWebHost
    {
        ServerSocket _serverSocket;

        /// <summary>
        /// 是否已启动
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        ///  SAEA WebConfig
        /// </summary>
        public WebConfig WebConfig { get; set; }

        /// <summary>
        /// 当解析到http请求时
        /// </summary>
        public event Action<IUserToken, IRequestDataReader> OnRequested;

        /// <summary>
        /// SAEA WebServer
        /// </summary>
        /// <param name="root">根目录</param>
        /// <param name="port">监听端口</param>
        /// <param name="isStaticsCached">是否启用静态缓存</param>
        /// <param name="isZiped">是压启用内容压缩</param>
        /// <param name="bufferSize">http处理数据缓存大小</param>
        /// <param name="count">http连接数上限</param>
        public WebHost(string root = "/html/", int port = 39654, bool isStaticsCached = true, bool isZiped = true, int bufferSize = 1024 * 100, int count = 10000)
        {
            WebConfig = new WebConfig()
            {
                Root = root,
                Port = port,
                IsStaticsCached = isStaticsCached,
                IsZiped = isZiped,
                HandleBufferSize = bufferSize,
                ClientCounts = count
            };

            _serverSocket = new ServerSocket(bufferSize, count);

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
        /// <param name="requestDataReader"></param>
        private void _serverSocket_OnRequested(IUserToken userToken, IRequestDataReader requestDataReader)
        {
            try
            {
                OnRequested.Invoke(userToken, requestDataReader);
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
