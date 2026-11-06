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
*文件名： TCPChannel
*版本号： v26.4.23.1
*唯一标识：281803fa-4d64-4e87-ac98-3007c84197f4
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 16:16:36
*描述：TCPChannel通道类
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
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Model;
using SAEA.P2P.Protocol;
using SAEA.P2P.Common;

namespace SAEA.P2P.Channel
{
    /// <summary>
    /// 基于 TCP 的点对点通道，负载经 P2PCoder 封装后收发
    /// </summary>
    public class TCPChannel
    {
        private SaeaClientSocket _socket;
        private P2PCoder _coder = new P2PCoder();
        private ChannelState _state = ChannelState.Closed;

        /// <summary>
        /// 收到用户数据时触发，参数为已从帧中拷贝出的负载
        /// </summary>
        public event Action<byte[]> OnDataReceived;
        /// <summary>
        /// 底层连接断开时触发
        /// </summary>
        public event Action OnDisconnected;
        /// <summary>
        /// 解帧或收发发生异常时触发，参数为异常对象
        /// </summary>
        public event Action<Exception> OnError;

        /// <summary>
        /// 当前通道状态，连接成功后为 Open
        /// </summary>
        public ChannelState State => _state;
        /// <summary>
        /// 底层 socket 是否处于已连接状态
        /// </summary>
        public bool IsConnected => _socket?.Connected ?? false;

        /// <summary>
        /// 创建尚未连接的空闲通道
        /// </summary>
        public TCPChannel()
        {
        }

        /// <summary>
        /// 连接目标地址，成功后状态置为 Open
        /// </summary>
        /// <param name="host">目标主机名或 IP 地址</param>
        /// <param name="port">目标端口</param>
        /// <param name="timeout">连接超时，单位毫秒，默认 5000</param>
        public void Connect(string host, int port, int timeout = 5000)
        {
            var option = SocketOptionBuilder.Instance
                .SetSocket(SAEASocketType.Tcp)
                .UsePacket()
                .SetIP(host)
                .SetPort(port)
                .SetConnectTimeout(timeout)
                .Build();

            _socket = SocketFactory.CreateClientSocket(option) as SaeaClientSocket;
            _socket.OnClientReceiveSpan += OnReceiveSpan;
            _socket.OnDisconnected += (id, ex) =>
            {
                _state = ChannelState.Closed;
                OnDisconnected?.Invoke();
            };
            _socket.OnError += (id, ex) => OnError?.Invoke(ex);

            _socket.Connect();
            _state = ChannelState.Open;

            P2PLogHelper.Info("TCPChannel", $"Connected to {host}:{port}");
        }

        /// <summary>
        /// 以 UserData 帧发送负载，状态非 Open 或连接已断开时抛出 P2PException
        /// </summary>
        /// <param name="data">待发送的负载，按原字节封装</param>
        public void Send(byte[] data)
        {
            if (_state != ChannelState.Open || !_socket.Connected)
                throw new P2PException(ErrorCode.PunchFailed);

            var packet = _coder.EncodeP2P(P2PMessageType.UserData, data);
            _socket.SendAsync(packet);
            P2PLogHelper.Trace("TCPChannel", $"Sent {data.Length} bytes");
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
        /// 断开连接并置为 Closed，未连接时无副作用
        /// </summary>
        public void Close()
        {
            _socket?.Disconnect();
            _state = ChannelState.Closed;
            P2PLogHelper.Debug("TCPChannel", "Closed");
        }
    }
}