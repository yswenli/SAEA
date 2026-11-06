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
*文件名： MqttClientWebSocketOptions
*版本号： v26.4.23.1
*唯一标识：30fe6726-2365-443d-a6b5-baab45249073
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientWebSocketOptions接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System.Collections.Generic;
using System.Net;

namespace SAEA.MQTT.Client.Options
{
    /// <summary>
    /// WebSocket 通道选项，包含地址、握手请求头与代理设置
    /// </summary>
    public class MqttClientWebSocketOptions : IMqttClientChannelOptions
    {
        /// <summary>
        /// WebSocket 服务端地址，形如 ws://host:port/path
        /// </summary>
        public string Uri { get; set; }

        /// <summary>
        /// 握手请求头，null 表示不附加自定义请求头
        /// </summary>
        public IDictionary<string, string> RequestHeaders { get; set; }

        /// <summary>
        /// 握手协商的子协议列表，默认只包含 mqtt
        /// </summary>
        public ICollection<string> SubProtocols { get; set; } = new List<string> { "mqtt" };

        /// <summary>
        /// 握手使用的 Cookie 容器，null 表示不使用 Cookie
        /// </summary>
        public CookieContainer CookieContainer { get; set; }

        /// <summary>
        /// 代理选项，null 表示直连
        /// </summary>
        public MqttClientWebSocketProxyOptions ProxyOptions { get; set; }

        /// <summary>
        /// TLS 选项，默认是未启用 TLS 的实例
        /// </summary>
        public MqttClientTlsOptions TlsOptions { get; set; } = new MqttClientTlsOptions();

        /// <summary>
        /// 返回 WebSocket 服务端地址
        /// </summary>
        /// <returns>WebSocket 服务端地址</returns>
        public override string ToString()
        {
            return Uri;
        }
    }
}
