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
*唯一标识：5548d2be-5642-44cf-9265-fa20c07f9f7f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/4/20 16:16:36
*描述：TCP通道类，管理TCP连接和数据传输
*
*=====================================================================
*修改标记
*修改时间：2026/4/20 16:16:36
*修改人： yswenli
*版本号： v26.4.23.1
*描述：TCP通道类，管理TCP连接和数据传输
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
    public class TCPChannel
    {
        private IocpClientSocket _socket;
        private P2PCoder _coder = new P2PCoder();
        private ChannelState _state = ChannelState.Closed;

        public event Action<byte[]> OnDataReceived;
        public event Action OnDisconnected;
        public event Action<Exception> OnError;

        public ChannelState State => _state;
        public bool IsConnected => _socket?.Connected ?? false;

        public TCPChannel()
        {
        }

        public void Connect(string host, int port, int timeout = 5000)
        {
            var option = SocketOptionBuilder.Instance
                .SetSocket(SAEASocketType.Tcp)
                .UseIocp()
                .SetIP(host)
                .SetPort(port)
                .SetConnectTimeout(timeout)
                .Build();

            _socket = SocketFactory.CreateClientSocket(option) as IocpClientSocket;
            _socket.OnReceive += OnReceive;
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

        public void Send(byte[] data)
        {
            if (_state != ChannelState.Open || !_socket.Connected)
                throw new P2PException(ErrorCode.PunchFailed);

            var packet = _coder.EncodeP2P(P2PMessageType.UserData, data);
            _socket.SendAsync(packet);
            P2PLogHelper.Trace("TCPChannel", $"Sent {data.Length} bytes");
        }

        private void OnReceive(byte[] data)
        {
            try
            {
                var protocols = _coder.DecodeP2P(data);
                foreach (var p in protocols)
                {
                    if (p.GetMessageType() == P2PMessageType.UserData && p.Content != null)
                    {
                        OnDataReceived?.Invoke(p.Content);
                    }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }
        }

        public void Close()
        {
            _socket?.Disconnect();
            _state = ChannelState.Closed;
            P2PLogHelper.Debug("TCPChannel", "Closed");
        }
    }
}