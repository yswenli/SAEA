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
*文件名： MqttClientCredentials
*版本号： v26.4.23.1
*唯一标识：79c011bb-cf72-420b-ae3c-9f482df3277a
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientCredentials接口
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
    /// 用户名密码凭据的默认实现
    /// </summary>
    public class MqttClientCredentials : IMqttClientCredentials
    {
        /// <summary>
        /// CONNECT 报文中的用户名
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// CONNECT 报文中的密码原始字节，不按文本编码
        /// </summary>
        public byte[] Password { get; set; }
    }
}
