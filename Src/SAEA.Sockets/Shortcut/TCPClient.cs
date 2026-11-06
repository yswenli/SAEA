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
*文件名： TCPClient
*版本号： v26.4.23.1
*唯一标识：a478b77f-6323-44fd-851a-19e57236373c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/09/21 19:36:08
*描述：TCPClient接口
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
using SAEA.Sockets.Core;
using SAEA.Sockets.Interface;

namespace SAEA.Sockets.Shortcut
{
    /// <summary>
    /// TCP 客户端快捷封装：包模式下的连接、收发与断开门面
    /// </summary>
    /// <typeparam name="Coder">编解码器类型，需提供公共无参构造函数</typeparam>
    public class TCPClient<Coder> : IDisposable where Coder : class, ICoder
    {
        IClientSocket _clientSokcet;

        /// <summary>
        /// 接收数据事件（ReadOnlyMemory 版本）。底层由 Span 事件驱动，回调内数据已完成复制，可跨回调保存。
        /// </summary>
        public event Action<TCPClient<Coder>, ReadOnlyMemory<byte>> OnReceive;

        /// <summary>
        /// 错误事件，参数为本客户端实例与异常；底层连接、收发失败均从该事件上报
        /// </summary>
        public event Action<TCPClient<Coder>, Exception> OnError;

        /// <summary>
        /// 断开事件，参数为本客户端实例与断开原因；无异常触发时为 null
        /// </summary>
        public event Action<TCPClient<Coder>, Exception> OnDisconnect;

        /// <summary>
        /// 流
        /// </summary>
        public SocketStream SocketStream { get; private set; }

        /// <summary>
        /// 以端地址初始化，同时创建底层客户端与流门面
        /// </summary>
        /// <param name="endPoint">服务端地址</param>
        public TCPClient(IPEndPoint endPoint)
        {
            _clientSokcet = SocketFactory.CreateClientSocket(SocketOptionBuilder.Instance
               .SetSocket(Model.SAEASocketType.Tcp)
               .SetIPEndPoint(endPoint)
               .UsePacket<Coder>()
               .Build());

            _clientSokcet.OnClientReceiveSpan += ClientSokcet_OnReceiveSpan;
            _clientSokcet.OnDisconnected += ClientSokcet_OnDisconnected;
            _clientSokcet.OnError += ClientSokcet_OnError;

            SocketStream = new SocketStream(_clientSokcet);
        }

        /// <summary>
        /// 以 IP 与端口初始化
        /// </summary>
        /// <param name="ip">服务端 IP 地址</param>
        /// <param name="port">服务端端口</param>
        public TCPClient(string ip, int port) : this(new IPEndPoint(IPAddress.Parse(ip), port))
        {

        }

        /// <summary>
        /// 发起异步连接，成功与否通过 OnError / OnDisconnect 事件反馈，调用立即返回
        /// </summary>
        public void Connect()
        {
            _clientSokcet.ConnectAsync();
        }
        /// <summary>
        /// 发送原始字节（Memory）；数组支撑的内存在快速路径零拷贝，发送完成前不得修改或复用
        /// </summary>
        /// <param name="data">待发送数据</param>
        /// <remarks>
        /// 零拷贝契约：数组支撑的 ReadOnlyMemory 在快速路径上零拷贝发送，发送完成前不得修改或复用该缓冲区；
        /// 非数组内存会在边界处发生一次复制。
        /// </remarks>
        public void SendAsync(ReadOnlyMemory<byte> data)
        {
            _clientSokcet.SendAsync(data);
        }
        /// <summary>
        /// 同步发送原始字节（Span）
        /// </summary>
        /// <param name="data">待发送数据</param>
        /// <remarks>
        /// 零拷贝契约：数组支撑的 ReadOnlySpan 在快速路径上零拷贝发送，发送完成前不得修改或复用该缓冲区；
        /// 非数组内存或需要缓冲的实现会在边界处发生一次复制。
        /// </remarks>
        public void Send(ReadOnlySpan<byte> data)
        {
            _clientSokcet.Send(data);
        }
        /// <summary>
        /// 以 UTF-8 编码发送字符串
        /// </summary>
        /// <param name="str">待发送字符串</param>
        public void SendAsync(string str)
        {
            _clientSokcet.SendAsync(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(str)));
        }


        private void ClientSokcet_OnError(string ID, Exception ex)
        {
            OnError?.Invoke(this, ex);
        }

        private void ClientSokcet_OnDisconnected(string ID, Exception ex)
        {
            OnDisconnect?.Invoke(this, ex);
        }

        private void ClientSokcet_OnReceiveSpan(ReadOnlySpan<byte> data)
        {
            OnReceive?.Invoke(this, data.ToArray());
        }
        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            _clientSokcet.Disconnect();
        }
        /// <summary>
        /// 释放底层客户端套接字
        /// </summary>
        public void Dispose()
        {
            _clientSokcet.Dispose();
        }
    }

    /// <summary>
    /// TCP 客户端快捷封装，编解码器固定为 BaseCoder
    /// </summary>
    public class TCPClient : TCPClient<BaseCoder>
    {
        /// <summary>
        /// 以端地址初始化
        /// </summary>
        /// <param name="endPoint">服务端地址</param>
        public TCPClient(IPEndPoint endPoint) : base(endPoint)
        {

        }

        /// <summary>
        /// 以 IP 与端口初始化
        /// </summary>
        /// <param name="ip">服务端 IP 地址</param>
        /// <param name="port">服务端端口</param>
        public TCPClient(string ip, int port) : base(ip, port)
        {

        }
    }
}
