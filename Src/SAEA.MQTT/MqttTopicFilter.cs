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
*唯一标识：09f66e3e-ba59-4941-95d7-35e75ee03905
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttTopicFilter接口
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MqttTopicFilter接口
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

        /// <summary>
        /// 共享订阅解析后的内部过滤器；非共享订阅时为 null（调用方使用 <see cref="Topic"/> 兜底）。
        /// </summary>
        internal string ParsedFilter { get; set; }

        /// <summary>
        /// 共享订阅的组名；null 表示非共享订阅。
        /// </summary>
        internal string ShareName { get; set; }

        /// <summary>
        /// MQTT 5.0 SUBSCRIBE 包携带的订阅标识符。
        /// </summary>
        internal uint? SubscriptionIdentifier { get; set; }

        internal bool IsShared => ShareName != null;

        /// <summary>
        /// 解析 <c>$share/{ShareName}/{filter}</c> 形式的共享订阅过滤器。
        /// 非共享过滤器返回 false，而不是报错。
        /// </summary>
        internal static bool TryParseShared(string topic, out string shareName, out string filter)
        {
            shareName = null;
            filter = null;

            if (string.IsNullOrEmpty(topic))
            {
                return false;
            }

            const string prefix = "$share/";
            if (!topic.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            var rest = topic.Substring(prefix.Length);
            if (rest.Length == 0)
            {
                return false;
            }

            var separatorIndex = rest.IndexOf('/');
            if (separatorIndex <= 0 || separatorIndex == rest.Length - 1)
            {
                return false;
            }

            var candidateShareName = rest.Substring(0, separatorIndex);
            if (candidateShareName.IndexOf('+') >= 0 || candidateShareName.IndexOf('#') >= 0)
            {
                return false;
            }

            var candidateFilter = rest.Substring(separatorIndex + 1);
            if (!IsValidTopicFilter(candidateFilter))
            {
                return false;
            }

            shareName = candidateShareName;
            filter = candidateFilter;
            return true;
        }

        static bool IsValidTopicFilter(string filter)
        {
            if (string.IsNullOrEmpty(filter))
            {
                return false;
            }

            for (var i = 0; i < filter.Length; i++)
            {
                var current = filter[i];

                if (current == '\0')
                {
                    return false;
                }

                if (current == '#')
                {
                    // '#' 只能出现在最后一个 level，且该 level 只能是 '#'。
                    if (i != filter.Length - 1)
                    {
                        return false;
                    }

                    if (i > 0 && filter[i - 1] != '/')
                    {
                        return false;
                    }
                }
                else if (current == '+')
                {
                    // '+' 必须独占一个 level。
                    if (i > 0 && filter[i - 1] != '/')
                    {
                        return false;
                    }

                    if (i < filter.Length - 1 && filter[i + 1] != '/')
                    {
                        return false;
                    }
                }
            }

            return true;
        }

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