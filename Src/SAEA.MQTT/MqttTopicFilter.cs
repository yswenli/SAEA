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
*文件名： MqttTopicFilter
*版本号： v26.4.23.1
*唯一标识：15230fed-673a-4009-9846-c8a1f237d2f5
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MQTT类
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MQTT类
*
*****************************************************************************/
using SAEA.MQTT.Protocol;
using System;

namespace SAEA.MQTT
{
    [Obsolete("Use MqttTopicFilter instead. It is just a renamed version to align with general namings in this lib.")]
    public class TopicFilter : MqttTopicFilter
    {
    }

    public class MqttTopicFilter
    {
        public string Topic { get; set; }

        public MqttQualityOfServiceLevel QualityOfServiceLevel { get; set; }

        /// <summary>
        /// This is only supported when using MQTTv5.
        /// </summary>
        public bool? NoLocal { get; set; }

        /// <summary>
        /// This is only supported when using MQTTv5.
        /// </summary>
        public bool? RetainAsPublished { get; set; }

        /// <summary>
        /// This is only supported when using MQTTv5.
        /// </summary>
        public MqttRetainHandling? RetainHandling { get; set; }

        public override string ToString()
        {
            return string.Concat(
                "TopicFilter: [Topic=",
                Topic,
                "] [QualityOfServiceLevel=",
                QualityOfServiceLevel,
                "] [NoLocal=",
                NoLocal,
                "] [RetainAsPublished=",
                RetainAsPublished,
                "] [RetainHandling=",
                RetainHandling,
                "]");
        }
    }
}