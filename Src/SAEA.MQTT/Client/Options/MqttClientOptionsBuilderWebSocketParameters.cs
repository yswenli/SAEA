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
*文件名： MqttClientOptionsBuilderWebSocketParameters
*版本号： v26.4.23.1
*唯一标识：46baee2e-e23d-4df3-9cd9-a9be817f6d06
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientOptionsBuilderWebSocketParameters接口
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
    /// WebSocket 通道的可选参数，供 WithWebSocketServer 使用
    /// </summary>
    public class MqttClientOptionsBuilderWebSocketParameters
    {
        /// <summary>
        /// 握手请求头，null 表示不附加自定义请求头
        /// </summary>
        public IDictionary<string, string> RequestHeaders { get; set; }

        /// <summary>
        /// 握手使用的 Cookie 容器，null 表示不使用 Cookie
        /// </summary>
        public CookieContainer CookieContainer { get; set; }
    }
}
