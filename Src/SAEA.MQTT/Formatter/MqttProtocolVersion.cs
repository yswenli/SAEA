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
*命名空间：SAEA.MQTT.Formatter
*文件名： MqttProtocolVersion
*版本号： v26.4.23.1
*唯一标识：ea6fedb5-9113-4804-a939-59ea6cd86024
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttProtocolVersion接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.MQTT.Formatter
{
    /// <summary>
    /// MQTT 协议版本，取值与 CONNECT 报文中的协议级别一致
    /// </summary>
    public enum MqttProtocolVersion
    {
        /// <summary>
        /// 未指定或未能识别
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// MQTT 3.1.0，协议名 MQIsdp、协议级别 3
        /// </summary>
        V310 = 3,
        /// <summary>
        /// MQTT 3.1.1，协议名 MQTT、协议级别 4
        /// </summary>
        V311 = 4,
        /// <summary>
        /// MQTT 5.0，协议名 MQTT、协议级别 5
        /// </summary>
        V500 = 5
    }
}
