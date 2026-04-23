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
*命名空间：SAEA.WebSocket
*文件名： WSServer
*版本号： v26.4.23.1
*唯一标识：188d4c80-74d0-4c82-a6ca-2586c62a2fec
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/03/18 02:16:04
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Authentication;

using SAEA.WebSocket.Core;
using SAEA.WebSocket.Model;

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
        /// <param name="maxConnects">连接数</param>
        public WSServer(int port = 39654, SslProtocols protocols = SslProtocols.None, string pfxPath = "", string pwd = "", int bufferSize = 64 * 1024, int maxConnects = 1000)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            if (protocols != SslProtocols.None && !string.IsNullOrEmpty(pfxPath))
            {
                _wsServer = new WSSServerImpl(protocols, pfxPath, pwd, port, bufferSize);
            }
            else
            {
                _wsServer = new WSServerImpl(port, bufferSize, maxConnects);
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
            if (string.IsNullOrEmpty(id)) return;
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
            if (string.IsNullOrEmpty(id)) return;
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
            if (string.IsNullOrEmpty(id)) return;
            _wsServer.Reply(id, data);
        }

        /// <summary>
        /// 发送关闭消息
        /// </summary>
        /// <param name="id">客户端ID</param>
        /// <param name="data">消息数据</param>
        public void Disconnect(string id, WSProtocal data)
        {
            if (string.IsNullOrEmpty(id)) return;
            _wsServer.Disconnect(id, data);
        }

        /// <summary>
        /// 断开客户端连接
        /// </summary>
        /// <param name="id">客户端ID</param>
        public void Disconnect(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
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