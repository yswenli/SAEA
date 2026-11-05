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
*唯一标识：a935c7f5-e71d-4d9d-98ad-2a714ccade0c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttQueuedApplicationMessage接口
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MqttQueuedApplicationMessage接口
*
*****************************************************************************/
using System;
using System.Collections.Generic;
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

        /// <summary>
        /// 消息的绝对到期时间。null 表示永不过期（MQTT 3.x 或未携带 Message Expiry Interval）。
        /// </summary>
        public DateTimeOffset? ExpiresAt { get; set; }

        /// <summary>
        /// 消息进入会话队列的时间。
        /// </summary>
        public DateTimeOffset EnqueuedAt { get; set; }

        /// <summary>
        /// 合并后的出站 retain 决策：为 true 时转发保留原始 <see cref="MqttApplicationMessage.Retain"/> 标志，
        /// 为 false 时出站消息的 Retain 恒为 false（retained 补发除外）。
        /// </summary>
        public bool RetainAsPublished { get; set; }

        /// <summary>
        /// 合并去重后的订阅标识符集合。可能为 null 或空，表示出站 PUBLISH 不携带订阅标识符。
        /// </summary>
        public List<uint> SubscriptionIdentifiers { get; set; }
    }
}