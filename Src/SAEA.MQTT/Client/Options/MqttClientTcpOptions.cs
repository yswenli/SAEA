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
*命名空间：SAEA.MQTT.Client.Options
*文件名： MqttClientTcpOptions
*版本号： v26.4.23.1
*唯一标识：e59092d6-e022-44a1-895b-47d3f829ef1b
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientTcpOptions接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System.Net.Sockets;

namespace SAEA.MQTT.Client.Options
{
    /// <summary>
    /// TCP 通道选项，包含服务端地址、端口与套接字参数
    /// </summary>
    public class MqttClientTcpOptions : IMqttClientChannelOptions
    {
        /// <summary>
        /// 服务端主机名或 IP 地址
        /// </summary>
        public string Server { get; set; }

        /// <summary>
        /// 服务端端口，null 表示按 TLS 是否启用取 1883 或 8883
        /// </summary>
        public int? Port { get; set; }

        /// <summary>
        /// 发送与接收缓冲区大小（字节），默认 65536
        /// </summary>
        public int BufferSize { get; set; } = 65536;

        /// <summary>
        /// 是否启用 IPv4/IPv6 双栈，null 表示按套接字默认行为
        /// </summary>
        public bool? DualMode { get; set; }

        /// <summary>
        /// 是否禁用 Nagle 算法，默认 true 表示小包立即发送
        /// </summary>
        public bool NoDelay { get; set; } = true;

        /// <summary>
        /// 使用的地址族，默认 Unspecified 表示由系统自动选择
        /// </summary>
        public AddressFamily AddressFamily { get; set; } = AddressFamily.Unspecified;

        /// <summary>
        /// TLS 选项，默认是未启用 TLS 的实例
        /// </summary>
        public MqttClientTlsOptions TlsOptions { get; set; } = new MqttClientTlsOptions();

        /// <summary>
        /// 返回「主机:端口」形式的连接描述，端口未设置时按默认规则推导
        /// </summary>
        /// <returns>连接描述文本</returns>
        public override string ToString()
        {
            return Server + ":" + this.GetPort();
        }
    }
}
