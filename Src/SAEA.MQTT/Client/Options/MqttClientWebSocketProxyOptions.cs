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
*文件名： MqttClientWebSocketProxyOptions
*版本号： v26.4.23.1
*唯一标识：1c2fa57d-ca4f-46a3-b452-1724595c8654
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientWebSocketProxyOptions接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.MQTT.Client.Options
{
    /// <summary>
    /// WebSocket 连接使用的代理选项
    /// </summary>
    public class MqttClientWebSocketProxyOptions
    {
        /// <summary>
        /// 代理地址，形如 http://host:port
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// 代理认证用户名，null 表示不认证
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// 代理认证密码，null 表示不认证
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// 代理认证域，null 表示不指定
        /// </summary>
        public string Domain { get; set; }

        /// <summary>
        /// 本地地址是否绕过代理，默认 false
        /// </summary>
        public bool BypassOnLocal { get; set; }

        /// <summary>
        /// 不经过代理的地址列表，null 表示不设置
        /// </summary>
        public string[] BypassList { get; set; }
    }
}