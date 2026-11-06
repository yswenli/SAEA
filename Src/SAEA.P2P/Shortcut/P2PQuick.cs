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
*唯一标识：2aeecf46-c380-4a8e-a789-338586f3cff2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 16:31:18
*描述：P2PQuick接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:20:16
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.P2P.Builder;
using SAEA.P2P.Core;
using SAEA.P2P.NAT;

namespace SAEA.P2P
{
    /// <summary>
    /// P2P 客户端与服务端的快捷构造入口：按场景预置打洞、中继与日志开关
    /// </summary>
    public static class P2PQuick
    {
        /// <summary>
        /// 创建启用打洞、中继与日志的客户端，未配置鉴权与加密
        /// </summary>
        /// <param name="serverAddr">信令服务器地址</param>
        /// <param name="port">信令服务器端口</param>
        /// <param name="nodeId">本地节点标识</param>
        /// <returns>尚未连接服务端的客户端实例</returns>
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
        
        /// <summary>
        /// 创建携带节点口令的客户端，在默认开关之外启用挑战响应鉴权
        /// </summary>
        /// <param name="serverAddr">信令服务器地址</param>
        /// <param name="port">信令服务器端口</param>
        /// <param name="nodeId">本地节点标识</param>
        /// <param name="password">节点口令，用于挑战响应鉴权</param>
        /// <returns>尚未连接服务端的客户端实例</returns>
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
        
        /// <summary>
        /// 按需配置打洞策略、加密与局域网发现的完整构造入口，中继与日志始终启用
        /// </summary>
        /// <param name="serverAddr">信令服务器地址</param>
        /// <param name="port">信令服务器端口</param>
        /// <param name="nodeId">本地节点标识</param>
        /// <param name="strategy">打洞策略，默认优先直连</param>
        /// <param name="encryption">是否启用加密</param>
        /// <param name="key">AES 密钥，仅当 encryption 为 true 且非空时才会生效</param>
        /// <param name="localDiscovery">是否启用局域网组播发现</param>
        /// <returns>尚未连接服务端的客户端实例</returns>
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
        
        /// <summary>
        /// 创建仅启用局域网发现的客户端，不配置信令服务器，沿用默认发现端口与组播地址
        /// </summary>
        /// <param name="nodeId">本地节点标识</param>
        /// <returns>尚未启动发现的客户端实例</returns>
        public static P2PClient LocalNet(string nodeId)
        {
            var options = new P2PClientBuilder()
                .SetNodeId(nodeId)
                .EnableLocalDiscovery()
                .EnableLogging()
                .Build();
            
            return new P2PClient(options);
        }
        
        /// <summary>
        /// 创建仅启用局域网发现的客户端，并指定发现端口与组播地址
        /// </summary>
        /// <param name="nodeId">本地节点标识</param>
        /// <param name="port">发现用 UDP 端口，取值 1-65535，越界时由构建器抛出 P2PException</param>
        /// <param name="multicast">组播地址，默认 224.0.0.250，为空时由构建器抛出 P2PException</param>
        /// <returns>尚未启动发现的客户端实例</returns>
        public static P2PClient LocalNet(string nodeId, int port, string multicast = "224.0.0.250")
        {
            var options = new P2PClientBuilder()
                .SetNodeId(nodeId)
                .EnableLocalDiscovery(port, multicast)
                .EnableLogging()
                .Build();
            
            return new P2PClient(options);
        }
        
        /// <summary>
        /// 创建启用中继与日志的 P2P 服务端，最大节点数取构建器默认值 10000
        /// </summary>
        /// <param name="port">服务端监听端口</param>
        /// <returns>尚未启动监听的服务端实例</returns>
        public static P2PServer Server(int port)
        {
            var options = new P2PServerBuilder()
                .SetPort(port)
                .EnableRelay()
                .EnableLogging()
                .Build();
            
            return new P2PServer(options);
        }
        
        /// <summary>
        /// 创建限制最大节点数的 P2P 服务端，并按需启用中继
        /// </summary>
        /// <param name="port">服务端监听端口</param>
        /// <param name="maxNodes">允许接入的最大节点数，必须大于 0，否则构建器抛出 P2PException</param>
        /// <param name="relay">是否启用中继转发，默认启用</param>
        /// <returns>尚未启动监听的服务端实例</returns>
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