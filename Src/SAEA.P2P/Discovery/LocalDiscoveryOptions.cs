/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.P2P.Discovery
*文件名： LocalDiscoveryOptions
*版本号： v26.11.5.1
*唯一标识：322feced-d4d8-46a0-8a23-ccf31c4ae3a3
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 17:05:13
*描述：局域网发现的端点、周期与认证配置
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 17:05:13
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增局域网发现的端点、周期与认证配置
*
*****************************************************************************/
using System.Net;

namespace SAEA.P2P.Discovery
{
    /// <summary>
    /// 局域网发现配置：报文端口、组播地址、收发周期、节点过期阈值、共享密钥与广播开关
    /// </summary>
    public sealed class LocalDiscoveryOptions
    {
        /// <summary>
        /// 发现报文使用的 UDP 端口，发送与接收共用，默认 39655
        /// </summary>
        public int Port { get; set; } = 39655;

        /// <summary>
        /// 组播地址，默认 224.0.0.250；为空表示只发广播
        /// </summary>
        public string MulticastGroup { get; set; } = "224.0.0.250";

        /// <summary>
        /// 周期补发间隔，单位毫秒，默认 1000
        /// </summary>
        public int IntervalMilliseconds { get; set; } = 1000;

        /// <summary>
        /// 节点过期阈值，单位毫秒，超过该时长未收到报文即从列表移除，默认 30000
        /// </summary>
        public int TimeoutMilliseconds { get; set; } = 30000;

        /// <summary>
        /// 报文 HMAC-SHA256 的共享密钥，为空表示不启用认证
        /// </summary>
        public string SharedKey { get; set; } = null;

        /// <summary>
        /// 是否同时向受限广播地址 255.255.255.255 发送报文，默认 true
        /// </summary>
        public bool EnableBroadcast { get; set; } = true;

        /// <summary>
        /// 接收报文时绑定的本地地址，默认 <see cref="IPAddress.Any"/>；只支持 IPv4
        /// </summary>
        public IPAddress BindAddress { get; set; } = IPAddress.Any;
    }
}