using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using SAEA.Sockets;
using SAEA.Sockets.Core.Udp;
using SAEA.Sockets.Model;
using SAEA.P2P.Protocol;
using SAEA.P2P.Common;

namespace SAEA.P2P.Discovery
{
    public class LocalDiscovery
    {
        private UdpClientSocket _socket;
        private P2PCoder _coder = new P2PCoder();
        private ConcurrentDictionary<string, DiscoveredNode> _discoveredNodes = new ConcurrentDictionary<string, DiscoveredNode>();

        private string _nodeId;
        private string _nodeName;
        private string[] _services;
        private int _port;
        private string _multicastGroup;
        private int _interval;
        private int _timeout;

        private Timer _broadcastTimer;
        private bool _running = false;

        public event Action<DiscoveredNode> OnNodeDiscovered;
        public event Action<DiscoveredNode> OnNodeExpired;

        public ConcurrentDictionary<string, DiscoveredNode> DiscoveredNodes => _discoveredNodes;

        public LocalDiscovery(string nodeId, int port = 39655, string multicastGroup = "224.0.0.250",
            int interval = 5000, int timeout = 30000)
        {
            _nodeId = nodeId;
            _nodeName = nodeId;
            _port = port;
            _multicastGroup = multicastGroup;
            _interval = interval;
            _timeout = timeout;
        }

        public void SetNodeInfo(string nodeName, string[] services)
        {
            _nodeName = nodeName ?? _nodeId;
            _services = services;
        }

        public void Start()
        {
            if (_running) return;

            var option = SocketOptionBuilder.Instance
                .SetSocket(SAEASocketType.Udp)
                .UseIocp()
                .SetPort(_port)
                .ReusePort()
                .UseBroadcast()
                .SetMultiCastHost(_multicastGroup)
                .Build();

            _socket = SocketFactory.CreateClientSocket(option) as UdpClientSocket;
            _socket.Bind(new IPEndPoint(IPAddress.Any, _port));

            if (!string.IsNullOrEmpty(_multicastGroup))
            {
                _socket.Socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                    new MulticastOption(IPAddress.Parse(_multicastGroup)));
            }

            _socket.OnReceive += OnReceiveData;

            _broadcastTimer = new Timer(BroadcastDiscovery, null, 0, _interval);
            _running = true;

            P2PLogHelper.Info("LocalDiscovery", $"Started on port {_port}, multicast: {_multicastGroup}");
        }

        public void Stop()
        {
            if (!_running) return;

            _broadcastTimer?.Dispose();
            _socket?.Disconnect();
            _running = false;

            P2PLogHelper.Info("LocalDiscovery", "Stopped");
        }

        private void BroadcastDiscovery(object state)
        {
            var content = Encoding.UTF8.GetBytes($"{_nodeId}|{_nodeName ?? ""}|{_services?.Length ?? 0}");
            var packet = _coder.EncodeP2P(P2PMessageType.LocalDiscover, content);

            var multicastAddr = new IPEndPoint(IPAddress.Parse(_multicastGroup), _port);
            _socket.SendAsync(multicastAddr, packet);

            var broadcastAddr = new IPEndPoint(IPAddress.Broadcast, _port);
            _socket.SendAsync(broadcastAddr, packet);

            P2PLogHelper.Trace("LocalDiscovery", "Broadcast discovery packet");
        }

        private void OnReceiveData(byte[] data)
        {
            try
            {
                var protocols = _coder.DecodeP2P(data);
                foreach (var p in protocols)
                {
                    if (p.GetMessageType() == P2PMessageType.LocalDiscover)
                    {
                        ProcessDiscoveryPacket(p.Content);
                    }
                    else if (p.GetMessageType() == P2PMessageType.LocalDiscoverAck)
                    {
                        ProcessDiscoveryAck(p.Content);
                    }
                }
            }
            catch (Exception ex)
            {
                P2PLogHelper.Error("LocalDiscovery", ErrorCode.DiscoveryFailed, "Failed to process discovery packet", ex);
            }
        }

        private void ProcessDiscoveryPacket(byte[] content)
        {
            if (content == null) return;

            var text = Encoding.UTF8.GetString(content);
            var parts = text.Split('|');
            if (parts.Length < 1) return;

            var nodeId = parts[0];
            if (nodeId == _nodeId) return;

            var nodeName = parts.Length > 1 ? parts[1] : "";

            var ackContent = Encoding.UTF8.GetBytes($"{_nodeId}|{_nodeName ?? ""}");
            var ackPacket = _coder.EncodeP2P(P2PMessageType.LocalDiscoverAck, ackContent);

            var broadcastAddr = new IPEndPoint(IPAddress.Broadcast, _port);
            _socket.SendAsync(broadcastAddr, ackPacket);

            P2PLogHelper.Debug("LocalDiscovery", $"Received discovery from {nodeId}, sent ack");
        }

        private void ProcessDiscoveryAck(byte[] content)
        {
            if (content == null) return;

            var text = Encoding.UTF8.GetString(content);
            var parts = text.Split('|');
            if (parts.Length < 1) return;

            var nodeId = parts[0];
            if (nodeId == _nodeId) return;

            var nodeName = parts.Length > 1 ? parts[1] : "";

            var node = _discoveredNodes.GetOrAdd(nodeId, id => new DiscoveredNode
            {
                NodeId = id,
                NodeName = nodeName,
                DiscoveredTime = DateTime.UtcNow
            });
            node.NodeName = nodeName;
            node.Refresh();

            OnNodeDiscovered?.Invoke(node);
            P2PLogHelper.Info("LocalDiscovery", $"Discovered node: {nodeId}");
        }

        public void CleanupExpiredNodes()
        {
            foreach (var kvp in _discoveredNodes)
            {
                if (kvp.Value.IsExpired(_timeout))
                {
                    _discoveredNodes.TryRemove(kvp.Key, out var removed);
                    OnNodeExpired?.Invoke(removed);
                    P2PLogHelper.Debug("LocalDiscovery", $"Node expired: {kvp.Key}");
                }
            }
        }
    }
}