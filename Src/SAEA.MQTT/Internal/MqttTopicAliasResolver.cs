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
 *文件名： MqttTopicAliasResolver
 *版本号： v26.4.23.1
 *唯一标识：9a4f4f4e-1c6a-4c7e-8a5e-3d9b2f7c1104
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2021/03/11 16:46:45
 *描述：MqttTopicAliasResolver类
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/05 00:00:00
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：MqttTopicAliasResolver类
 *
 *****************************************************************************/
using System.Collections.Generic;

namespace SAEA.MQTT.Internal
{
    /// <summary>
    /// MQTT 5.0 入站 Topic Alias 解析器。每条网络连接持有独立实例，映射绝不跨连接复用。
    /// </summary>
    internal sealed class MqttTopicAliasResolver
    {
        readonly object _syncRoot = new object();
        readonly Dictionary<ushort, string> _aliases = new Dictionary<ushort, string>();

        public MqttTopicAliasResolver(ushort maximum)
        {
            Maximum = maximum;
        }

        public ushort Maximum { get; }

        /// <summary>
        /// 解析一条 PUBLISH 的 Topic Alias。Topic 非空时建立或覆盖映射；
        /// Topic 为空时按映射还原，未命中返回 <see cref="AliasError.Unknown"/>。
        /// </summary>
        public bool TryResolve(ushort alias, string topic, out string resolvedTopic, out AliasError error)
        {
            lock (_syncRoot)
            {
                resolvedTopic = null;

                if (Maximum == 0)
                {
                    error = AliasError.ExceedsMaximum;
                    return false;
                }

                if (alias == 0)
                {
                    error = AliasError.Zero;
                    return false;
                }

                if (alias > Maximum)
                {
                    error = AliasError.ExceedsMaximum;
                    return false;
                }

                if (!string.IsNullOrEmpty(topic))
                {
                    _aliases[alias] = topic;
                    resolvedTopic = topic;
                    error = AliasError.None;
                    return true;
                }

                string mappedTopic;
                if (_aliases.TryGetValue(alias, out mappedTopic))
                {
                    resolvedTopic = mappedTopic;
                    error = AliasError.None;
                    return true;
                }

                error = AliasError.Unknown;
                return false;
            }
        }

        /// <summary>
        /// 清空当前连接的映射。对象销毁时自然失效，此方法仅用于显式释放路径。
        /// </summary>
        public void Clear()
        {
            lock (_syncRoot)
            {
                _aliases.Clear();
            }
        }

        internal enum AliasError
        {
            None,
            Zero,
            ExceedsMaximum,
            Unknown
        }
    }
}