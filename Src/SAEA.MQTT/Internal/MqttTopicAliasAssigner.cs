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
 *命名空间：SAEA.MQTT.Internal
 *文件名： MqttTopicAliasAssigner
 *版本号： v26.4.23.1
 *唯一标识：4b3c1d2e-5f60-4718-9a3b-8c7d6e5f1105
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2021/03/11 16:46:45
 *描述：MqttTopicAliasAssigner类
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/05 00:00:00
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：MqttTopicAliasAssigner类
 *
 *****************************************************************************/
using System.Collections.Generic;

namespace SAEA.MQTT.Internal
{
    /// <summary>
    /// MQTT 5.0 出站 Topic Alias 分配器。每条网络连接持有独立实例。
    /// 首次出现主题返回 includeTopic=true；再次出现返回 includeTopic=false。
    /// 映射满时按 FIFO 淘汰最早建立的别名。
    /// </summary>
    internal sealed class MqttTopicAliasAssigner
    {
        readonly object _syncRoot = new object();
        readonly Dictionary<string, ushort> _topicToAlias = new Dictionary<string, ushort>();
        readonly Dictionary<ushort, string> _aliasToTopic = new Dictionary<ushort, string>();
        readonly LinkedList<ushort> _assignmentOrder = new LinkedList<ushort>();

        ushort _nextAlias = 1;

        public MqttTopicAliasAssigner(ushort maximum)
        {
            Maximum = maximum;
        }

        public ushort Maximum { get; }

        /// <summary>
        /// 为指定主题分配别名。Maximum 为 0 或主题为空时返回 false 且不写别名。
        /// </summary>
        public bool TryAssign(string topic, out ushort alias, out bool includeTopic)
        {
            alias = 0;
            includeTopic = true;

            if (Maximum == 0 || string.IsNullOrEmpty(topic))
            {
                return false;
            }

            lock (_syncRoot)
            {
                ushort existingAlias;
                if (_topicToAlias.TryGetValue(topic, out existingAlias))
                {
                    alias = existingAlias;
                    includeTopic = false;
                    return true;
                }

                if (_aliasToTopic.Count >= Maximum)
                {
                    // FIFO：淘汰最早建立的别名并立即复用。
                    var oldestAlias = _assignmentOrder.First.Value;
                    _assignmentOrder.RemoveFirst();

                    string evictedTopic;
                    if (_aliasToTopic.TryGetValue(oldestAlias, out evictedTopic))
                    {
                        _aliasToTopic.Remove(oldestAlias);
                        _topicToAlias.Remove(evictedTopic);
                    }

                    alias = oldestAlias;
                }
                else
                {
                    alias = FindAvailableAlias();
                }

                _topicToAlias[topic] = alias;
                _aliasToTopic[alias] = topic;
                _assignmentOrder.AddLast(alias);

                _nextAlias = NextAlias(alias);
                includeTopic = true;
                return true;
            }
        }

        ushort FindAvailableAlias()
        {
            var candidate = _nextAlias;
            for (var i = 0; i < Maximum; i++)
            {
                if (!_aliasToTopic.ContainsKey(candidate))
                {
                    return candidate;
                }

                candidate = NextAlias(candidate);
            }

            // 仅在映射已满时才会走到这里，调用方已提前处理满表分支。
            return candidate;
        }

        ushort NextAlias(ushort alias)
        {
            return alias >= Maximum ? (ushort)1 : (ushort)(alias + 1);
        }

        /// <summary>
        /// 清空当前连接的映射。对象销毁时自然失效，此方法仅用于显式释放路径。
        /// </summary>
        public void Clear()
        {
            lock (_syncRoot)
            {
                _topicToAlias.Clear();
                _aliasToTopic.Clear();
                _assignmentOrder.Clear();
                _nextAlias = 1;
            }
        }
    }
}