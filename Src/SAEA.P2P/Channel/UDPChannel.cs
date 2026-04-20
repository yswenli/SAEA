using System;
using System.Net;
using SAEA.Sockets;
using SAEA.Sockets.Core.Udp;
using SAEA.Sockets.Model;
using SAEA.P2P.Protocol;
using SAEA.P2P.Common;

namespace SAEA.P2P.Channel
{
    public class UDPChannel
    {
        private UdpClientSocket _socket;
        private P2PCoder _coder = new P2PCoder();
        private ChannelState _state = ChannelState.Closed;
        private IPEndPoint _remoteEndPoint;

        public event Action<byte[]> OnDataReceived;
        public event Action<Exception> OnError;

        public ChannelState State => _state;
        public IPEndPoint RemoteEndPoint => _remoteEndPoint;
        public bool IsConnected => _state == ChannelState.Open;

        public UDPChannel()
        {
        }

        public void Bind(int port)
        {
            var option = SocketOptionBuilder.Instance
                .SetSocket(SAEASocketType.Udp)
                .UseIocp()
                .SetPort(port)
                .ReusePort()
                .Build();

            _socket = SocketFactory.CreateClientSocket(option) as UdpClientSocket;
            _socket.Bind(new IPEndPoint(IPAddress.Any, port));
            _socket.OnReceive += OnReceive;
            _socket.OnError += (id, ex) => OnError?.Invoke(ex);

            _state = ChannelState.Bound;
            P2PLogHelper.Debug("UDPChannel", $"Bound to port {port}");
        }

        public void Connect(IPEndPoint remote)
        {
            _remoteEndPoint = remote;
            _state = ChannelState.Open;
            P2PLogHelper.Debug("UDPChannel", $"Connected to {remote}");
        }

        public void Send(byte[] data)
        {
            if (_state != ChannelState.Open || _remoteEndPoint == null)
                throw new P2PException(ErrorCode.PunchFailed);

            var packet = _coder.EncodeP2P(P2PMessageType.UserData, data);
            _socket.SendAsync(_remoteEndPoint, packet);
            P2PLogHelper.Trace("UDPChannel", $"Sent {data.Length} bytes to {_remoteEndPoint}");
        }

        public void SendRaw(byte[] data, IPEndPoint target)
        {
            _socket.SendAsync(target, data);
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
            P2PLogHelper.Debug("UDPChannel", "Closed");
        }
    }
}