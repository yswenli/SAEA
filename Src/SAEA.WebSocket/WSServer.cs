/****************************************************************************
*Copyright (c) 2018-2021yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.WebSocket
*文件名： WSServer
*版本号： v6.0.0.1
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： v6.0.0.1
*描述：
*
*****************************************************************************/

using SAEA.WebSocket.Core;
using SAEA.WebSocket.Model;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Authentication;

namespace SAEA.WebSocket
{
    public class WSServer
    {
        IWSServer _wsServer;

        public event Action<string> OnConnected;

        public event Action<string, WSProtocal> OnMessage;

        public event Action<string> OnDisconnected;

        /// <summary>
        /// 已成功连接的客户端
        /// </summary>
        public List<string> Clients
        {
            get
            {
                return _wsServer.Clients;
            }
        }


        public WSServer(int port = 16666, SslProtocols protocols = SslProtocols.None, string pfxPath = "", string pwd = "", int bufferSize = 1024, int count = 60000)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            if (protocols != SslProtocols.None && !string.IsNullOrEmpty(pfxPath))
            {
                _wsServer = new WSSServerImpl(protocols, pfxPath, pwd, port, bufferSize);
            }
            else
            {
                _wsServer = new WSServerImpl(port, bufferSize, count);

            }
            _wsServer.OnConnected += WsServer_OnConnected;
            _wsServer.OnMessage += WsServer_OnMessage;
            _wsServer.OnDisconnected += WsServer_OnDisconnected;
        }

        private void WsServer_OnConnected(string id)
        {
            OnConnected?.Invoke(id);
        }

        private void WsServer_OnMessage(string str, WSProtocal protocal)
        {
            this.OnMessage?.Invoke(str, protocal);
        }

        private void WsServer_OnDisconnected(string id)
        {
            OnDisconnected?.Invoke(id);
        }

        public void Start(int backlog = 10 * 1000)
        {
            _wsServer.Start(backlog);
        }

        /// <summary>
        /// 回复客户端消息
        /// </summary>
        /// <param name="id"></param>
        /// <param name="data"></param>
        public void Reply(string id, WSProtocal data)
        {
            _wsServer.Reply(id, data);
        }

        /// <summary>
        /// 发送关闭
        /// </summary>
        /// <param name="id"></param>
        /// <param name="data"></param>
        public void Disconnect(string id, WSProtocal data)
        {
            _wsServer.Disconnect(id, data);
        }

        /// <summary>
        /// 关闭
        /// </summary>
        /// <param name="id"></param>
        public void Disconnect(string id)
        {
            _wsServer.Disconnect(id);
        }

        public void Stop()
        {
            _wsServer.Stop();
        }

    }
}
