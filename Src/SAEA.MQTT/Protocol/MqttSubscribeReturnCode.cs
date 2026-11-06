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
*命名空间：SAEA.MQTT.Protocol
*文件名： MqttSubscribeReturnCode
*版本号： v26.4.23.1
*唯一标识：1ff8b4dc-3307-489e-8cc0-beaf49adacbb
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttSubscribeReturnCode接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.MQTT.Protocol
{
    /// <summary>
    /// MQTT 3.1.1 订阅确认（SUBACK）返回码
    /// </summary>
    public enum MqttSubscribeReturnCode
    {
        /// <summary>
        /// 订阅成功，授予的最大 QoS 为 0
        /// </summary>
        SuccessMaximumQoS0 = 0x00,
        /// <summary>
        /// 订阅成功，授予的最大 QoS 为 1
        /// </summary>
        SuccessMaximumQoS1 = 0x01,
        /// <summary>
        /// 订阅成功，授予的最大 QoS 为 2
        /// </summary>
        SuccessMaximumQoS2 = 0x02,
        /// <summary>
        /// 订阅失败
        /// </summary>
        Failure = 0x80
    }
}
