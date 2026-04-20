using System;
using SAEA.P2P.Builder;
using SAEA.P2P.Core;
using SAEA.P2P.Common;
using SAEA.P2P.NAT;

namespace SAEA.P2P
{
    public static class P2PQuick
    {
        public static P2PClient Client(string serverAddr, int port, string nodeId)
        {
            var options = new P2PClientBuilder()
                .SetServer(serverAddr, port)
                .SetNodeId(nodeId)
                .EnableHolePunch()
                .EnableRelay()
                .EnableLogging()
                .Build();
            
            return new P2PClient(options);
        }
        
        public static P2PClient Client(string serverAddr, int port, string nodeId, string password)
        {
            var options = new P2PClientBuilder()
                .SetServer(serverAddr, port)
                .SetNodeId(nodeId)
                .SetNodeIdPassword(password)
                .EnableHolePunch()
                .EnableRelay()
                .EnableLogging()
                .Build();
            
            return new P2PClient(options);
        }
        
        public static P2PClient ClientFull(string serverAddr, int port, string nodeId, 
            HolePunchStrategy strategy = HolePunchStrategy.PreferDirect,
            bool encryption = false, string key = null,
            bool localDiscovery = false)
        {
            var builder = new P2PClientBuilder()
                .SetServer(serverAddr, port)
                .SetNodeId(nodeId)
                .EnableHolePunch(strategy)
                .EnableRelay();
            
            if (encryption && !string.IsNullOrEmpty(key))
                builder.EnableEncryption(key);
            
            if (localDiscovery)
                builder.EnableLocalDiscovery();
            
            builder.EnableLogging();
            
            return new P2PClient(builder.Build());
        }
        
        public static P2PClient LocalNet(string nodeId)
        {
            var options = new P2PClientBuilder()
                .SetNodeId(nodeId)
                .EnableLocalDiscovery()
                .EnableLogging()
                .Build();
            
            return new P2PClient(options);
        }
        
        public static P2PClient LocalNet(string nodeId, int port, string multicast = "224.0.0.250")
        {
            var options = new P2PClientBuilder()
                .SetNodeId(nodeId)
                .EnableLocalDiscovery(port, multicast)
                .EnableLogging()
                .Build();
            
            return new P2PClient(options);
        }
        
        public static P2PServer Server(int port)
        {
            var options = new P2PServerBuilder()
                .SetPort(port)
                .EnableRelay()
                .EnableLogging()
                .Build();
            
            return new P2PServer(options);
        }
        
        public static P2PServer Server(int port, int maxNodes, bool relay = true)
        {
            var builder = new P2PServerBuilder()
                .SetPort(port)
                .SetMaxNodes(maxNodes);
            
            if (relay)
                builder.EnableRelay();
            
            builder.EnableLogging();
            
            return new P2PServer(builder.Build());
        }
    }
}