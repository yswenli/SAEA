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
*文件名： UDPServer
*版本号： v26.4.23.1
*唯一标识：35e155f3-7a42-4d40-a470-8025f066cd5e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/09/20 01:32:10
*描述：UDPServer服务端类
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
    /// UDP 服务端快捷封装：包模式下的监听、收发与断开门面
    /// </summary>
    /// <typeparam name="Coder">编解码器类型，需提供公共无参构造函数</typeparam>
    public class UDPServer<Coder> where Coder : class, ICoder
    {
        IServerSocket _udpServer;

        /// <summary>
        /// 接收数据事件（解析后的 ISocketProtocal 帧）。
        /// </summary>
        /// <remarks>
        /// 回调作用域：帧的 Content 由租用缓冲区支撑，仅在本次回调期间有效，必须同步消费；
        /// 如需跨回调保存，请复制帧内容。
        /// </remarks>
        public event Action<UDPServer<Coder>, string, ISocketProtocal> OnReceive;

        /// <summary>
        /// 错误事件，参数为相关会话 ID 与异常
        /// </summary>
        public event Action<string, Exception> OnError;

        /// <summary>
        /// 新会话事件，参数为新会话 ID
        /// </summary>
        public event Action<string> OnAccepted;

        /// <summary>
        /// 断开事件，参数为会话 ID；ID 为空时不触发
        /// </summary>
        public event Action<string> OnDisconnected;

        /// <summary>
        /// 以监听地址初始化，收发缓冲区按 UDP 最大报文长度配置
        /// </summary>
        /// <param name="endPoint">监听地址</param>
        /// <param name="timeOut">操作超时，单位毫秒，默认 5000</param>
        public UDPServer(IPEndPoint endPoint, int timeOut = 5000)
        {
            _udpServer = SocketFactory.CreateServerSocket(SocketOptionBuilder.Instance.SetSocket(SAEASocketType.Udp)
                .SetIPEndPoint(endPoint)
                .UsePacket<Coder>()
                .SetReadBufferSize(SocketOption.UDPMaxLength)
                .SetWriteBufferSize(SocketOption.UDPMaxLength)
                .SetActionTimeOut(timeOut)
                .Build());

            _udpServer.OnAccepted += UdpServer_OnAccepted;
            _udpServer.OnDisconnected += UdpServer_OnDisconnected;
            _udpServer.OnError += UdpServer_OnError;
            _udpServer.OnServerReceiveSpan += UdpServer_OnReceiveSpan;
        }

        /// <summary>
        /// 以端口初始化，监听所有网卡
        /// </summary>
        /// <param name="port">监听端口</param>
        /// <param name="timeOut">操作超时，单位毫秒，默认 5000</param>
        public UDPServer(int port, int timeOut = 5000) : this(new IPEndPoint(IPAddress.Any, port), timeOut)
        {

        }

        /// <summary>
        /// 启动监听
        /// </summary>
        public void Start()
        {
            _udpServer.Start();
        }

        /// <summary>
        /// 发送已构造好的协议对象，供派生类复用
        /// </summary>
        /// <param name="id">目标会话 ID</param>
        /// <param name="baseSocketProtocal">协议对象，其 Type 决定帧类型</param>
        protected void SendAsync(string id, BaseSocketProtocal baseSocketProtocal)
        {
            _udpServer.SendAsync(id, baseSocketProtocal);
        }

        /// <summary>
        /// 将数据包装为指定类型的协议帧发给指定会话；数组支撑的内存在快速路径零拷贝
        /// </summary>
        /// <param name="id">目标会话 ID</param>
        /// <param name="data">待发送数据（帧 body）</param>
        /// <param name="socketProtocalType">帧类型，默认 ChatMessage</param>
        /// <remarks>
        /// 零拷贝契约：数组支撑的 ReadOnlyMemory 在快速路径上零拷贝发送，发送完成前不得修改或复用该缓冲区；
        /// 非数组内存会在边界处发生一次复制。
        /// </remarks>
        public void SendAsync(string id, ReadOnlyMemory<byte> data, SocketProtocalType socketProtocalType = SocketProtocalType.ChatMessage)
        {
            SendAsync(id, new BaseSocketProtocal((byte)socketProtocalType, data));
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
            _udpServer.Send(id, data);
        }

        /// <summary>
        /// 向指定会话以 UTF-8 编码发送字符串，帧类型为 ChatMessage
        /// </summary>
        /// <param name="id">目标会话 ID</param>
        /// <param name="msg">待发送字符串</param>
        public void SendAsync(string id, string msg)
        {
            SendAsync(id, new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(msg)));
        }

        private void UdpServer_OnReceiveSpan(IUserToken userToken, ReadOnlySpan<byte> data)
        {
            var buffer = data.ToArray();
            using (var msgs = userToken.Coder.Decode(new System.Buffers.ReadOnlySequence<byte>(buffer)))
            {
                if (msgs.Count == 0) return;
                foreach (var msg in msgs.Frames)
                    OnReceive?.Invoke(this, userToken.ID, msg);
            }
        }

        private void UdpServer_OnError(string id, Exception ex)
        {
            OnError?.Invoke(id, ex);
        }

        private void UdpServer_OnDisconnected(string id, Exception ex)
        {
            if (string.IsNullOrEmpty(id)) return;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"UdpServer_OnDisconnected:{id}");
            OnDisconnected?.Invoke(id);
        }

        private void UdpServer_OnAccepted(object obj)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"UdpServer_OnAccepted:{((IUserToken)obj).ID}");
            OnAccepted?.Invoke(((IUserToken)obj).ID);
        }

    }

    /// <summary>
    /// UDP 服务端快捷封装，编解码器固定为 BaseCoder
    /// </summary>
    public class UDPServer : UDPServer<BaseCoder>
    {
        /// <summary>
        /// 以监听地址初始化
        /// </summary>
        /// <param name="endPoint">监听地址</param>
        /// <param name="timeOut">操作超时，单位毫秒</param>
        public UDPServer(IPEndPoint endPoint, int timeOut = 5000) : base(endPoint, timeOut)
        {

        }
        /// <summary>
        /// 以端口初始化，监听所有网卡
        /// </summary>
        /// <param name="port">监听端口</param>
        /// <param name="timeOut">操作超时，单位毫秒</param>
        public UDPServer(int port, int timeOut = 5000) : base(port, timeOut)
        {

        }
    }
}
