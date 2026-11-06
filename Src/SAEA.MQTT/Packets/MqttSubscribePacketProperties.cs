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
*命名空间：SAEA.MQTT.Packets
*文件名： MqttSubscribePacketProperties
*版本号： v26.4.23.1
*唯一标识：ebe751cd-16e1-4cb4-8723-00cf9e0d8eef
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttSubscribePacketProperties接口
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

namespace SAEA.MQTT.Packets
{
    /// <summary>
    /// SUBSCRIBE 报文的属性集合，均为 MQTT 5.0 新增
    /// </summary>
    public sealed class MqttSubscribePacketProperties
    {
        /// <summary>
        /// 订阅标识符，服务端转发匹配消息时会带上该值
        /// </summary>
        public uint? SubscriptionIdentifier { get; set; }

        /// <summary>
        /// 用户自定义属性
        /// </summary>
        public List<MqttUserProperty> UserProperties { get; set; }
    }
}
