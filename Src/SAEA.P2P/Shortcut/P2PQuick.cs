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
*命名空间：SAEA.P2P
*文件名： P2PQuick
*版本号： v26.4.23.1
*唯一标识：1fb83300-80a7-475e-ac43-0bd3fc7bd65c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 16:31:18
*描述：
*
*=====================================================================
*修改标记
*修改时间：2026/04/20 16:31:18
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using SAEA.P2P.Builder;
using SAEA.P2P.Core;
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