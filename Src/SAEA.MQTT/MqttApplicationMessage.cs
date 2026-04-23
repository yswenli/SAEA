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
*命名空间：SAEA.MQTT
*文件名： MqttApplicationMessage
*版本号： v26.4.23.1
*唯一标识：54b760a3-d06e-4c00-babc-7fe1a3db4ff0
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System.Collections.Generic;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT
{
    public class MqttApplicationMessage
    {
        public string Topic { get; set; }

        public byte[] Payload { get; set; }

        public MqttQualityOfServiceLevel QualityOfServiceLevel { get; set; }

        public bool Retain { get; set; }

        public List<MqttUserProperty> UserProperties { get; set; }

        public string ContentType { get; set; }

        public string ResponseTopic { get; set; }

        public MqttPayloadFormatIndicator? PayloadFormatIndicator { get; set; }

        public uint? MessageExpiryInterval { get; set; }

        public ushort? TopicAlias { get; set; }

        public byte[] CorrelationData { get; set; }

        public List<uint> SubscriptionIdentifiers { get; set; }
    }
}
