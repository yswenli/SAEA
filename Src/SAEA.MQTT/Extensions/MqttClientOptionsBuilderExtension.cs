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
*命名空间：SAEA.MQTT.Extensions
*文件名： MqttClientOptionsBuilderExtension
*版本号： v26.4.23.1
*唯一标识：afe96c78-312f-4ad0-8257-882133c4cf08
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientOptionsBuilderExtension接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Linq;
using SAEA.MQTT.Client.Options;

namespace SAEA.MQTT.Extensions
{
    /// <summary>
    /// 客户端选项构造器的连接地址扩展
    /// </summary>
    public static class MqttClientOptionsBuilderExtension
    {
        /// <summary>
        /// 按地址的协议方案配置服务器与凭据，地址中的用户信息用作账号密码
        /// </summary>
        /// <param name="builder">客户端选项构造器，不能为null</param>
        /// <param name="uri">连接地址，支持tcp、mqtt、mqtts、ws、wss方案</param>
        /// <returns>当前构造器</returns>
        public static MqttClientOptionsBuilder WithConnectionUri(this MqttClientOptionsBuilder builder, Uri uri)
        {
            var port = uri.IsDefaultPort ? null : (int?) uri.Port;
            switch (uri.Scheme.ToLower())
            {
                case "tcp":
                case "mqtt":
                    builder.WithTcpServer(uri.Host, port);
                    break;

                case "mqtts":
                    builder.WithTcpServer(uri.Host, port).WithTls();
                    break;

                case "ws":
                case "wss":
                    builder.WithWebSocketServer(uri.ToString());
                    break;

                default:
                    throw new ArgumentException("Unexpected scheme in uri.");
            }
            
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var userInfo = uri.UserInfo.Split(':');
                var username = userInfo[0];
                var password = userInfo.Length > 1 ? userInfo[1] : "";
                builder.WithCredentials(username, password);
            }

            return builder;
        }

        /// <summary>
        /// 按地址字符串的协议方案配置服务器与凭据
        /// </summary>
        /// <param name="builder">客户端选项构造器，不能为null</param>
        /// <param name="uri">绝对地址字符串，支持tcp、mqtt、mqtts、ws、wss方案</param>
        /// <returns>当前构造器</returns>
        public static MqttClientOptionsBuilder WithConnectionUri(this MqttClientOptionsBuilder builder, string uri)
        {
            return WithConnectionUri(builder, new Uri(uri, UriKind.Absolute));
        }
    }
}
