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
*命名空间：SAEA.MQTT.Server
*文件名： MqttServerTcpEndpointBaseOptions
*版本号： v26.4.23.1
*唯一标识：c6fed7e2-09ff-4883-b428-17723ae77649
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttServerTcpEndpointBaseOptions接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System.Net;

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 服务端 TCP 端点（明文或 TLS）的公共监听配置
    /// </summary>
    public abstract class MqttServerTcpEndpointBaseOptions
    {
        /// <summary>
        /// 是否启用该端点
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// 监听端口
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// 监听队列长度，即尚未被接受的连接可排队的数量，默认 10
        /// </summary>
        public int ConnectionBacklog { get; set; } = 10;

        /// <summary>
        /// 是否禁用 Nagle 算法以降低小包延迟，默认 true
        /// </summary>
        public bool NoDelay { get; set; } = true;

#if WINDOWS_UWP
        /// <summary>
        /// 收发缓冲区大小（字节），默认 4096
        /// </summary>
        public int BufferSize { get; set; } = 4096;
#endif

        /// <summary>
        /// 绑定的 IPv4 地址，默认 IPAddress.Any
        /// </summary>
        public IPAddress BoundInterNetworkAddress { get; set; } = IPAddress.Any;

        /// <summary>
        /// 绑定的 IPv6 地址，默认 IPAddress.IPv6Any
        /// </summary>
        public IPAddress BoundInterNetworkV6Address { get; set; } = IPAddress.IPv6Any;

        /// <summary>
        /// 是否允许复用本地地址，在 Linux 上启用需要管理员权限
        /// </summary>
        public bool ReuseAddress { get; set; }
    }
}