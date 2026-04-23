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
*命名空间：SAEA.MQTT.Server
*文件名： MqttQueuedApplicationMessage
*版本号： v26.4.23.1
*唯一标识：d9c4227f-245f-430a-942c-01f3c5b6e70c
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
using System;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Server
{
    public class MqttQueuedApplicationMessage
    {
        public MqttApplicationMessage ApplicationMessage { get; set; }

        public string SenderClientId { get; set; }

        public bool IsRetainedMessage { get; set; }

        public MqttQualityOfServiceLevel SubscriptionQualityOfServiceLevel { get; set; }

        [Obsolete("Use 'SubscriptionQualityOfServiceLevel' instead.")]
        public MqttQualityOfServiceLevel QualityOfServiceLevel
        {
            get => SubscriptionQualityOfServiceLevel;
            set => SubscriptionQualityOfServiceLevel = value;
        }

        public bool IsDuplicate { get; set; }
    }
}