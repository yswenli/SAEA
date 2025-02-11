/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.WebSocket
*文件名： WSServer
*版本号： v7.0.0.1
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
*版本号： v7.0.0.1
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
    /// <summary>
    /// WebSocket服务器类
    /// </summary>
    public class WSServer
    {
        IWSServer _wsServer;

        /// <summary>
        /// 客户端连接事件
        /// </summary>
        public event Action<string> OnConnected;

        /// <summary>
        /// 客户端消息事件
        /// </summary>
        public event Action<string, WSProtocal> OnMessage;

        /// <summary>
        /// 客户端断开连接事件
        /// </summary>
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

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="port">端口号</param>
        /// <param name="protocols">SSL协议</param>
        /// <param name="pfxPath">证书路径</param>
        /// <param name="pwd">证书密码</param>
        /// <param name="bufferSize">缓冲区大小</param>
        /// <param name="count">连接数</param>
        public WSServer(int port = 39654, SslProtocols protocols = SslProtocols.None, string pfxPath = "", string pwd = "", int bufferSize = 1024, int count = 60000)
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

        /// <summary>
        /// 处理客户端连接事件
        /// </summary>
        /// <param name="id">客户端ID</param>
        private void WsServer_OnConnected(string id)
        {
            OnConnected?.Invoke(id);
        }

        /// <summary>
        /// 处理客户端消息事件
        /// </summary>
        /// <param name="str">消息内容</param>
        /// <param name="protocal">协议类型</param>
        private void WsServer_OnMessage(string str, WSProtocal protocal)
        {
            this.OnMessage?.Invoke(str, protocal);
        }

        /// <summary>
        /// 处理客户端断开连接事件
        /// </summary>
        /// <param name="id">客户端ID</param>
        private void WsServer_OnDisconnected(string id)
        {
            OnDisconnected?.Invoke(id);
        }

        /// <summary>
        /// 启动服务器
        /// </summary>
        /// <param name="backlog">挂起连接队列的最大长度</param>
        public void Start(int backlog = 10 * 1000)
        {
            _wsServer.Start(backlog);
        }

        /// <summary>
        /// 回复客户端消息
        /// </summary>
        /// <param name="id">客户端ID</param>
        /// <param name="data">消息数据</param>
        public void Reply(string id, WSProtocal data)
        {
            _wsServer.Reply(id, data);
        }

        /// <summary>
        /// 发送关闭消息
        /// </summary>
        /// <param name="id">客户端ID</param>
        /// <param name="data">消息数据</param>
        public void Disconnect(string id, WSProtocal data)
        {
            _wsServer.Disconnect(id, data);
        }

        /// <summary>
        /// 断开客户端连接
        /// </summary>
        /// <param name="id">客户端ID</param>
        public void Disconnect(string id)
        {
            _wsServer.Disconnect(id);
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        public void Stop()
        {
            _wsServer.Stop();
        }
    }
}
