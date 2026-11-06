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
*命名空间：SAEA.Sockets.Shortcut
*文件名： TCPServer
*版本号： v26.4.23.1
*唯一标识：6c46016f-1e63-4f6d-a4ef-5e520d3e9ac6
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/09/21 19:36:08
*描述：TCPServer服务端类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:27
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Net;
using System.Text;

using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

namespace SAEA.Sockets.Shortcut
{
    /// <summary>
    /// TCP 服务端快捷封装：包模式下的监听、收发与断开门面
    /// </summary>
    /// <typeparam name="Coder">编解码器类型，需提供公共无参构造函数</typeparam>
    public class TCPServer<Coder> where Coder : class, ICoder
    {
        IServerSocket _serverSokcet;


        /// <summary>
        /// 新连接事件，参数为本服务端实例与新连接的会话 ID
        /// </summary>
        public event Action<TCPServer<Coder>, string> OnAccept;

        /// <summary>
        /// 接收数据事件（ReadOnlyMemory 版本）。底层由 Span 事件驱动，回调内数据已完成复制，可跨回调保存。
        /// </summary>
        public event Action<TCPServer<Coder>, IUserToken, ReadOnlyMemory<byte>> OnReceive;

        /// <summary>
        /// 错误事件，参数为本服务端实例、相关会话 ID 与异常
        /// </summary>
        public event Action<TCPServer<Coder>, string, Exception> OnError;

        /// <summary>
        /// 断开事件，参数为本服务端实例、会话 ID 与断开原因；无异常触发时为 null
        /// </summary>
        public event Action<TCPServer<Coder>, string, Exception> OnDisconnect;

        /// <summary>
        /// 以监听地址初始化，并订阅底层服务端事件
        /// </summary>
        /// <param name="endpoint">监听地址</param>
        public TCPServer(IPEndPoint endpoint)
        {
            //tcpserver
            _serverSokcet = SocketFactory.CreateServerSocket(SocketOptionBuilder.Instance
                .SetSocket(Model.SAEASocketType.Tcp)
                .SetIPEndPoint(endpoint)
                .UsePacket<Coder>()
                .Build());

            _serverSokcet.OnAccepted += ServerSokcet_OnAccepted;
            _serverSokcet.OnDisconnected += ServerSokcet_OnDisconnected;
            _serverSokcet.OnError += ServerSokcet_OnError;
            _serverSokcet.OnServerReceiveSpan += ServerSokcet_OnReceiveSpan;
        }

        /// <summary>
        /// 以端口初始化，监听所有网卡（IPAddress.Any）
        /// </summary>
        /// <param name="port">监听端口</param>
        public TCPServer(int port) : this(new IPEndPoint(IPAddress.Any, port))
        {

        }

        /// <summary>
        /// 启动监听
        /// </summary>
        public void Start()
        {
            _serverSokcet.Start();
        }

        /// <summary>
        /// 停止监听并清理所有连接与会话
        /// </summary>
        public void Stop()
        {
            _serverSokcet.Stop();
        }

        /// <summary>
        /// 向指定会话发送原始字节（Memory）；数组支撑的内存在快速路径零拷贝，发送完成前不得修改或复用
        /// </summary>
        /// <param name="id">目标会话 ID</param>
        /// <param name="data">待发送数据</param>
        /// <remarks>
        /// 零拷贝契约：数组支撑的 ReadOnlyMemory 在快速路径上零拷贝发送，发送完成前不得修改或复用该缓冲区；
        /// 非数组内存会在边界处发生一次复制。
        /// </remarks>
        public void SendAsync(string id, ReadOnlyMemory<byte> data)
        {
            _serverSokcet.SendAsync(id, data);
        }

        /// <summary>
        /// 向指定会话同步发送原始字节（Span）
        /// </summary>
        /// <param name="id">目标会话 ID</param>
        /// <param name="data">待发送数据</param>
        /// <remarks>
        /// 零拷贝契约：数组支撑的 ReadOnlySpan 在快速路径上零拷贝发送，发送完成前不得修改或复用该缓冲区；
        /// 非数组内存或需要缓冲的实现会在边界处发生一次复制。
        /// </remarks>
        public void Send(string id, ReadOnlySpan<byte> data)
        {
            _serverSokcet.Send(id, data);
        }

        /// <summary>
        /// 向指定会话以 UTF-8 编码发送字符串
        /// </summary>
        /// <param name="id">目标会话 ID</param>
        /// <param name="str">待发送字符串</param>
        public void SendAsync(string id, string str)
        {
            _serverSokcet.SendAsync(id, new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(str)));
        }

        private void ServerSokcet_OnReceiveSpan(IUserToken userToken, ReadOnlySpan<byte> data)
        {
            OnReceive?.Invoke(this, userToken, data.ToArray());
        }

        private void ServerSokcet_OnError(string id, Exception ex)
        {
            OnError?.Invoke(this, id, ex);
        }

        private void ServerSokcet_OnDisconnected(string id, Exception ex)
        {
            OnDisconnect?.Invoke(this, id, ex);
        }

        private void ServerSokcet_OnAccepted(object obj)
        {
            var userToken = ResolveUserToken(obj);

            if (userToken != null)
            {
                OnAccept?.Invoke(this, userToken.ID);
            }
            else if (obj is ChannelInfo channelInfo)
            {
                OnAccept?.Invoke(this, channelInfo.ID);
            }
            else
            {
                OnError?.Invoke(this, "unknown", new InvalidCastException("unexpected OnAccepted payload: " + obj?.GetType().FullName));
            }
        }

        private static IUserToken ResolveUserToken(object obj)
        {
            if (obj is IUserToken userToken) return userToken;

            if (obj is ChannelInfo channelInfo) return channelInfo.UserToken;

            return null;
        }

        /// <summary>
        /// 断开指定会话
        /// </summary>
        /// <param name="id">目标会话 ID</param>
        public void Disconnect(string id)
        {
            _serverSokcet.Disconnect(id);
        }
    }

    /// <summary>
    /// TCP 服务端快捷封装，编解码器固定为 BaseCoder
    /// </summary>
    public class TCPServer : TCPServer<BaseCoder>
    {
        /// <summary>
        /// 以监听地址初始化
        /// </summary>
        /// <param name="endPoint">监听地址</param>
        public TCPServer(IPEndPoint endPoint) : base(endPoint)
        {

        }
        /// <summary>
        /// 以端口初始化，监听所有网卡
        /// </summary>
        /// <param name="port">监听端口</param>
        public TCPServer(int port) : base(port)
        {

        }
    }
}
