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
*修改时间：2026/11/13 18:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：恢复无信令局域网直连入口，改用局域网发现与 ICE 建链
*
*****************************************************************************/
using System;
using SAEA.P2P.Builder;
using SAEA.P2P.Core;
using SAEA.P2P.Discovery;
using SAEA.P2P.Ice;
using SAEA.P2P.Turn;

namespace SAEA.P2P
{
    /// <summary>
    /// P2P 客户端与服务端的快捷构造入口：预置信令、局域网发现与 ICE/TURN 配置
    /// </summary>
    public static class P2PQuick
    {
        /// <summary>
        /// 创建连接指定信令服务器的客户端，ICE 与 TURN 沿用构建器默认配置
        /// </summary>
        /// <param name="serverAddr">信令服务器地址</param>
        /// <param name="port">信令服务器端口</param>
        /// <param name="nodeId">本地节点标识</param>
        /// <returns>尚未连接服务端的客户端实例</returns>
        /// <exception cref="Common.P2PException">服务器地址或端口非法、节点标识为空</exception>
        public static P2PClient Client(string serverAddr, int port, string nodeId)
        {
            return new P2PClientBuilder()
                .SetServer(serverAddr, port)
                .SetNodeId(nodeId)
                .BuildClient();
        }

        /// <summary>
        /// 创建携带节点口令的客户端，注册阶段启用挑战应答认证
        /// </summary>
        /// <param name="serverAddr">信令服务器地址</param>
        /// <param name="port">信令服务器端口</param>
        /// <param name="nodeId">本地节点标识</param>
        /// <param name="password">节点口令，用于挑战应答认证，null 视为不启用认证</param>
        /// <returns>尚未连接服务端的客户端实例</returns>
        /// <exception cref="Common.P2PException">服务器地址或端口非法、节点标识为空</exception>
        public static P2PClient Client(string serverAddr, int port, string nodeId, string password)
        {
            return new P2PClientBuilder()
                .SetServer(serverAddr, port)
                .SetNodeId(nodeId)
                .SetNodeIdPassword(password)
                .BuildClient();
        }

        /// <summary>
        /// 按需覆写 ICE 与 TURN 配置的完整构造入口，未传入的配置沿用构建器默认值
        /// </summary>
        /// <param name="serverAddr">信令服务器地址</param>
        /// <param name="port">信令服务器端口</param>
        /// <param name="nodeId">本地节点标识</param>
        /// <param name="ice">ICE 会话配置，null 时沿用构建器默认值</param>
        /// <param name="turn">TURN 中继配置，null 时 TURN 阶段不可用</param>
        /// <param name="enableTls">是否启用信令连接的 TLS</param>
        /// <returns>尚未连接服务端的客户端实例</returns>
        /// <exception cref="Common.P2PException">服务器地址或端口非法、节点标识为空，或当前平台不支持 TCP 同时打开</exception>
        /// <exception cref="ArgumentException">ICE 会话配置或 TURN 配置非法</exception>
        public static P2PClient ClientFull(string serverAddr, int port, string nodeId,
            IceSessionOptions ice = null, TurnClientOptions turn = null, bool enableTls = false)
        {
            var builder = new P2PClientBuilder()
                .SetServer(serverAddr, port)
                .SetNodeId(nodeId);

            if (ice != null)
                builder.UseIce(ice);

            if (turn != null)
                builder.UseTurn(turn);

            if (enableTls)
                builder.EnableTls();

            return builder.BuildClient();
        }

        /// <summary>
        /// 无信令局域网直连入口：按默认发现配置（39655 端口、224.0.0.250 组播）启动发现。
        /// 无信令模式没有身份鉴权，只应在可信局域网使用；HMAC 只保证报文完整性，需要时通过
        /// <see cref="LocalDiscoveryOptions.SharedKey"/> 配置共享密钥
        /// </summary>
        /// <param name="nodeId">本地节点标识，不能为空白</param>
        /// <returns>已启动局域网发现的客户端实例</returns>
        /// <exception cref="Common.P2PException">节点标识为空白</exception>
        public static P2PClient LocalNet(string nodeId)
        {
            var client = new P2PClient(nodeId, new LocalDiscoveryOptions(), new IceSessionOptions());
            // StartLocalAsync 只做绑定与启动，没有真正的异步等待，这里同步取结果不会死锁
            client.StartLocalAsync().GetAwaiter().GetResult();
            return client;
        }

        /// <summary>
        /// 指定发现端口与组播地址的无信令局域网直连入口。无信令模式没有身份鉴权，只应在可信局域网使用；
        /// HMAC 只保证报文完整性，需要时通过 <see cref="LocalDiscoveryOptions.SharedKey"/> 配置共享密钥
        /// </summary>
        /// <param name="nodeId">本地节点标识，不能为空白</param>
        /// <param name="port">发现用 UDP 端口，发送与接收共用</param>
        /// <param name="multicast">组播地址，默认 224.0.0.250；为空表示只发广播</param>
        /// <returns>已启动局域网发现的客户端实例</returns>
        /// <exception cref="Common.P2PException">节点标识为空白</exception>
        /// <exception cref="ArgumentOutOfRangeException">发现端口越界或间隔、阈值非法</exception>
        public static P2PClient LocalNet(string nodeId, int port, string multicast = "224.0.0.250")
        {
            var discovery = new LocalDiscoveryOptions { Port = port, MulticastGroup = multicast };
            var client = new P2PClient(nodeId, discovery, new IceSessionOptions());
            client.StartLocalAsync().GetAwaiter().GetResult();
            return client;
        }

        /// <summary>
        /// 创建 P2P 服务端，最大节点数取构建器默认值，日志启用
        /// </summary>
        /// <param name="port">服务端监听端口</param>
        /// <returns>尚未启动监听的服务端实例</returns>
        /// <exception cref="Common.P2PException">端口非法</exception>
        public static P2PServer Server(int port)
        {
            var builder = new P2PServerBuilder()
                .SetPort(port)
                .EnableLogging();

            return new P2PServer(builder.Build());
        }

        /// <summary>
        /// 创建限制最大节点数的 P2P 服务端，日志启用
        /// </summary>
        /// <param name="port">服务端监听端口</param>
        /// <param name="maxNodes">允许接入的最大节点数，必须大于 0，否则构建器抛出异常</param>
        /// <returns>尚未启动监听的服务端实例</returns>
        /// <exception cref="Common.P2PException">端口非法，或最大节点数不大于 0</exception>
        public static P2PServer Server(int port, int maxNodes)
        {
            var builder = new P2PServerBuilder()
                .SetPort(port)
                .SetMaxNodes(maxNodes)
                .EnableLogging();

            return new P2PServer(builder.Build());
        }
    }
}