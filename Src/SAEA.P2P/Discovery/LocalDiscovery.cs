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
*命名空间：SAEA.P2P.Discovery
*文件名： LocalDiscovery
*版本号： v26.4.23.1
*唯一标识：fb0291b1-d664-45e9-b1e1-62a3bce5f36c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 16:16:36
*描述：LocalDiscovery接口
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
    /// <summary>
    /// 局域网节点发现，周期组播与广播探测报文并维护已发现节点
    /// </summary>
    public class LocalDiscovery
    {
        private SaeaUdpClientSocket _socket;
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

        /// <summary>
        /// 收到其他节点的发现应答时触发，同一节点每次应答都会触发
        /// </summary>
        public event Action<DiscoveredNode> OnNodeDiscovered;
        /// <summary>
        /// 节点因超时被清理时触发，参数为被移除的节点
        /// </summary>
        public event Action<DiscoveredNode> OnNodeExpired;

        /// <summary>
        /// 已发现节点集合，键为节点标识
        /// </summary>
        public ConcurrentDictionary<string, DiscoveredNode> DiscoveredNodes => _discoveredNodes;

        /// <summary>
        /// 创建发现实例，此时尚未占用端口
        /// </summary>
        /// <param name="nodeId">本节点标识，同时作为默认节点名称</param>
        /// <param name="port">发现报文使用的端口，默认 39655</param>
        /// <param name="multicastGroup">组播地址，默认 224.0.0.250</param>
        /// <param name="interval">探测报文广播间隔，单位毫秒，默认 5000</param>
        /// <param name="timeout">节点过期阈值，单位毫秒，默认 30000</param>
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

        /// <summary>
        /// 设置对外广播的节点名称与服务列表，名称为 null 时回退为节点标识
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <param name="services">服务名列表，可为 null</param>
        public void SetNodeInfo(string nodeName, string[] services)
        {
            _nodeName = nodeName ?? _nodeId;
            _services = services;
        }

        /// <summary>
        /// 绑定发现端口、加入组播组并启动周期广播，已启动时直接返回
        /// </summary>
        public void Start()
        {
            if (_running) return;

            var option = SocketOptionBuilder.Instance
                .SetSocket(SAEASocketType.Udp)
                .UsePacket()
                .SetPort(_port)
                .ReusePort()
                .UseBroadcast()
                .SetMultiCastHost(_multicastGroup)
                .Build();

            _socket = SocketFactory.CreateClientSocket(option) as SaeaUdpClientSocket;
            _socket.Bind(new IPEndPoint(IPAddress.Any, _port));

            if (!string.IsNullOrEmpty(_multicastGroup))
            {
                _socket.Socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                    new MulticastOption(IPAddress.Parse(_multicastGroup)));
            }

            _socket.OnClientReceiveSpan += OnReceiveDataSpan;

            _broadcastTimer = new Timer(BroadcastDiscovery, null, 0, _interval);
            _running = true;

            P2PLogHelper.Info("LocalDiscovery", $"Started on port {_port}, multicast: {_multicastGroup}");
        }

        /// <summary>
        /// 停止周期广播并断开套接字，未启动时直接返回
        /// </summary>
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

        private void OnReceiveDataSpan(ReadOnlySpan<byte> data)
        {
            try
            {
                using (var frames = _coder.DecodeP2P(data))
                {
                    foreach (var frame in frames.Frames)
                    {
                        if ((P2PMessageType)frame.Type == P2PMessageType.LocalDiscover)
                        {
                            ProcessDiscoveryPacket(frame.Content);
                        }
                        else if ((P2PMessageType)frame.Type == P2PMessageType.LocalDiscoverAck)
                        {
                            ProcessDiscoveryAck(frame.Content);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                P2PLogHelper.Error("LocalDiscovery", ErrorCode.DiscoveryFailed, "Failed to process discovery packet", ex);
            }
        }

        private void ProcessDiscoveryPacket(ReadOnlyMemory<byte> content)
        {
            var text = P2PContent.GetString(content);
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

        private void ProcessDiscoveryAck(ReadOnlyMemory<byte> content)
        {
            var text = P2PContent.GetString(content);
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

        /// <summary>
        /// 清理超过超时阈值的节点并触发 OnNodeExpired，需由调用方周期驱动
        /// </summary>
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