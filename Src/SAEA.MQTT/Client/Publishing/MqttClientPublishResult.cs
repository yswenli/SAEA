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
*命名空间：SAEA.MQTT.Client.Publishing
*文件名： MqttClientPublishResult
*版本号： v26.4.23.1
*唯一标识：0bdf32f6-81d7-4bb2-979b-e9dfba7c364a
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientPublishResult接口
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MqttClientPublishResult接口
*
*****************************************************************************/
using System.Collections.Generic;
using SAEA.MQTT.Packets;

namespace SAEA.MQTT.Client.Publishing
{
    public class MqttClientPublishResult
    {
        public ushort? PacketIdentifier { get; set; }

        public MqttClientPublishReasonCode ReasonCode { get; set; } = MqttClientPublishReasonCode.Success;

        public string ReasonString { get; set; }

        public List<MqttUserProperty> UserProperties { get; set; }
    }
}
