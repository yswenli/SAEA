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
*命名空间：SAEA.P2P.Channel
*文件名： UDPChannel
*版本号： v26.4.23.1
*唯一标识：6902acaa-b8fc-4b10-9698-1d16bfc9f310
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 16:16:36
*描述：UDPChannel通道类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:20:16
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Net;
using SAEA.Sockets;
using SAEA.Sockets.Core.Udp;
using SAEA.Sockets.Model;
using SAEA.P2P.Protocol;
using SAEA.P2P.Common;

namespace SAEA.P2P.Channel
{
    /// <summary>
    /// 基于 UDP 的点对点通道，除按帧收发外还支持指定目标的原始发送
    /// </summary>
    public class UDPChannel
    {
        private UdpClientSocket _socket;
        private P2PCoder _coder = new P2PCoder();
        private ChannelState _state = ChannelState.Closed;
        private IPEndPoint _remoteEndPoint;

        /// <summary>
        /// 收到用户数据时触发，参数为已从帧中拷贝出的负载
        /// </summary>
        public event Action<byte[]> OnDataReceived;
        /// <summary>
        /// 解帧或收发发生异常时触发，参数为异常对象
        /// </summary>
        public event Action<Exception> OnError;

        /// <summary>
        /// 当前通道状态，绑定端口后为 Bound，指定远端后为 Open
        /// </summary>
        public ChannelState State => _state;
        /// <summary>
        /// 当前发送目标，未调用 Connect 时为 null
        /// </summary>
        public IPEndPoint RemoteEndPoint => _remoteEndPoint;
        /// <summary>
        /// 是否已指定远端地址（状态为 Open）
        /// </summary>
        public bool IsConnected => _state == ChannelState.Open;

        /// <summary>
        /// 创建尚未绑定端口的通道
        /// </summary>
        public UDPChannel()
        {
        }

        /// <summary>
        /// 绑定本地端口并启用端口复用，成功后状态置为 Bound
        /// </summary>
        /// <param name="port">本地监听端口</param>
        public void Bind(int port)
        {
            var option = SocketOptionBuilder.Instance
                .SetSocket(SAEASocketType.Udp)
                .UsePacket()
                .SetPort(port)
                .ReusePort()
                .Build();

            _socket = SocketFactory.CreateClientSocket(option) as UdpClientSocket;
            _socket.Bind(new IPEndPoint(IPAddress.Any, port));
            _socket.OnClientReceiveSpan += OnReceiveSpan;
            _socket.OnError += (id, ex) => OnError?.Invoke(ex);

            _state = ChannelState.Bound;
            P2PLogHelper.Debug("UDPChannel", $"Bound to port {port}");
        }

        /// <summary>
        /// 记录发送目标并置为 Open，不产生实际网络交互
        /// </summary>
        /// <param name="remote">对端地址</param>
        public void Connect(IPEndPoint remote)
        {
            _remoteEndPoint = remote;
            _state = ChannelState.Open;
            P2PLogHelper.Debug("UDPChannel", $"Connected to {remote}");
        }

        /// <summary>
        /// 以 UserData 帧发送到已记录的远端，状态非 Open 或远端为空时抛出 P2PException
        /// </summary>
        /// <param name="data">待发送的负载，按原字节封装</param>
        public void Send(byte[] data)
        {
            if (_state != ChannelState.Open || _remoteEndPoint == null)
                throw new P2PException(ErrorCode.PunchFailed);

            var packet = _coder.EncodeP2P(P2PMessageType.UserData, data);
            _socket.SendAsync(_remoteEndPoint, packet);
            P2PLogHelper.Trace("UDPChannel", $"Sent {data.Length} bytes to {_remoteEndPoint}");
        }

        /// <summary>
        /// 不经 P2P 编码，把原始字节直接发往指定地址
        /// </summary>
        /// <param name="data">原文数据</param>
        /// <param name="target">目标地址</param>
        public void SendRaw(byte[] data, IPEndPoint target)
        {
            _socket.SendAsync(target, data);
        }

        private void OnReceiveSpan(ReadOnlySpan<byte> data)
        {
            try
            {
                using (var frames = _coder.DecodeP2P(data))
                {
                    foreach (var frame in frames.Frames)
                    {
                        if ((P2PMessageType)frame.Type == P2PMessageType.UserData && !frame.Content.IsEmpty)
                        {
                            OnDataReceived?.Invoke(frame.Content.ToArray());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }
        }

        /// <summary>
        /// 断开套接字并置为 Closed，未绑定时无副作用
        /// </summary>
        public void Close()
        {
            _socket?.Disconnect();
            _state = ChannelState.Closed;
            P2PLogHelper.Debug("UDPChannel", "Closed");
        }
    }
}