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
*文件名： UDPClient
*版本号： v26.4.23.1
*唯一标识：ab903354-bc3c-4e69-bc2e-b561c411df16
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/09/20 01:32:10
*描述：UDPClient接口
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
    /// UDP 客户端快捷封装：包模式下的连接、收发与断开门面
    /// </summary>
    /// <typeparam name="Coder">编解码器类型，需提供公共无参构造函数</typeparam>
    public class UDPClient<Coder> : IDisposable where Coder : class, ICoder
    {
        IClientSocket _udpClient;

        BaseCoder _baseUnpacker;

        /// <summary>
        /// 接收数据事件（解析后的 ISocketProtocal 帧）。
        /// </summary>
        /// <remarks>
        /// 回调作用域：帧的 Content 由租用缓冲区支撑，仅在本次回调期间有效，必须同步消费；
        /// 如需跨回调保存，请复制帧内容。
        /// </remarks>
        public event Action<UDPClient<Coder>, ISocketProtocal> OnReceive;

        /// <summary>
        /// 错误事件，参数为本客户端实例与异常
        /// </summary>
        public event Action<UDPClient<Coder>, Exception> OnError;

        /// <summary>
        /// 断开事件，参数为本客户端实例；底层会话 ID 为空时不触发
        /// </summary>
        public event Action<UDPClient<Coder>> OnDisconnected;

        /// <summary>
        /// 以端地址初始化，收发缓冲区按 UDP 最大报文长度配置
        /// </summary>
        /// <param name="endPoint">服务端地址</param>
        public UDPClient(IPEndPoint endPoint)
        {
            var bContext = new BaseContext<Coder>();

            _udpClient = SocketFactory.CreateClientSocket(SocketOptionBuilder.Instance.SetSocket(SAEASocketType.Udp)
                .SetIPEndPoint(endPoint)
                .UsePacket(bContext)
                .SetReadBufferSize(SocketOption.UDPMaxLength)
                .SetWriteBufferSize(SocketOption.UDPMaxLength)
                .Build());

            _udpClient.OnDisconnected += UdpClient_OnDisconnected;
            _udpClient.OnClientReceiveSpan += UdpClient_OnReceiveSpan;
            _udpClient.OnError += UdpClient_OnError;

            _baseUnpacker = (BaseCoder)bContext.Unpacker;
        }

        /// <summary>
        /// 以 IP 与端口初始化
        /// </summary>
        /// <param name="ip">服务端 IP 地址</param>
        /// <param name="port">服务端端口</param>
        public UDPClient(string ip, int port) : this(new IPEndPoint(IPAddress.Parse(ip), port))
        {

        }

        /// <summary>
        /// 同步连接服务端
        /// </summary>
        public void Connect()
        {
            _udpClient.Connect();
        }

        /// <summary>
        /// 发送已构造好的协议对象，供派生类复用
        /// </summary>
        /// <param name="protocal">协议对象，其 Type 决定帧类型</param>
        protected void SendAsync(BaseSocketProtocal protocal)
        {
            _udpClient.SendAsync(protocal);
        }

        /// <summary>
        /// 将数据包装为指定类型的协议帧发送；数组支撑的内存在快速路径零拷贝，发送完成前不得修改或复用
        /// </summary>
        /// <param name="data">待发送数据（帧 body）</param>
        /// <param name="socketProtocalType">帧类型，默认 ChatMessage</param>
        /// <remarks>
        /// 零拷贝契约：数组支撑的 ReadOnlyMemory 在快速路径上零拷贝发送，发送完成前不得修改或复用该缓冲区；
        /// 非数组内存会在边界处发生一次复制。
        /// </remarks>
        public void SendAsync(ReadOnlyMemory<byte> data, SocketProtocalType socketProtocalType = SocketProtocalType.ChatMessage)
        {
            SendAsync(new BaseSocketProtocal((byte)socketProtocalType, data));
        }

        /// <summary>
        /// 以 UTF-8 编码发送字符串，帧类型为 ChatMessage
        /// </summary>
        /// <param name="msg">待发送字符串</param>
        public void SendAsync(string msg)
        {
            SendAsync(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(msg)));
        }

        private void UdpClient_OnError(string ID, Exception ex)
        {
            Console.WriteLine($"UdpClient_OnError {ID} :" + ex.Message);
            OnError?.Invoke(this, ex);
        }

        private void UdpClient_OnReceiveSpan(ReadOnlySpan<byte> data)
        {
            var buffer = data.ToArray();
            using (var msgs = _baseUnpacker.Decode(new System.Buffers.ReadOnlySequence<byte>(buffer)))
            {
                if (msgs.Count < 1) return;
                foreach (var msg in msgs.Frames)
                    OnReceive?.Invoke(this, msg);
            }
        }

        private void UdpClient_OnDisconnected(string id, Exception ex)
        {
            if (string.IsNullOrEmpty(id)) return;
            Console.WriteLine($"UdpClient_OnDisconnected {id} :" + ex.Message);
            OnDisconnected?.Invoke(this);
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            _udpClient.Disconnect();
        }

        /// <summary>
        /// 释放底层客户端套接字
        /// </summary>
        public void Dispose()
        {
            _udpClient?.Dispose();
        }
    }

    /// <summary>
    /// UDP 客户端快捷封装，编解码器固定为 BaseCoder
    /// </summary>
    public class UDPClient : UDPClient<BaseCoder>
    {
        /// <summary>
        /// 以端地址初始化
        /// </summary>
        /// <param name="endPoint">服务端地址</param>
        public UDPClient(IPEndPoint endPoint) : base(endPoint)
        {

        }
        /// <summary>
        /// 以 IP 与端口初始化
        /// </summary>
        /// <param name="ip">服务端 IP 地址</param>
        /// <param name="port">服务端端口</param>
        public UDPClient(string ip, int port) : base(ip, port)
        {

        }
    }
}
