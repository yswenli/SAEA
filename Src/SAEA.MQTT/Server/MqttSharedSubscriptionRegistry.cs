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
 *文件名： MqttSharedSubscriptionRegistry
 *版本号： v26.4.23.1
 *唯一标识：4c2d9e81-7a63-4f15-b8c4-1e9d0a5f3b72
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/05 00:00:00
 *描述：MqttSharedSubscriptionRegistry类
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/05 00:00:00
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：MqttSharedSubscriptionRegistry类
 *
 *****************************************************************************/
using System;
using System.Collections.Generic;

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 共享订阅键：(ShareName, Filter)。
    /// </summary>
    internal readonly struct MqttSharedSubscriptionKey : IEquatable<MqttSharedSubscriptionKey>
    {
        public MqttSharedSubscriptionKey(string shareName, string filter)
        {
            ShareName = shareName;
            Filter = filter;
        }

        public string ShareName { get; }

        public string Filter { get; }

        public bool Equals(MqttSharedSubscriptionKey other)
        {
            return string.Equals(ShareName, other.ShareName, StringComparison.Ordinal)
                   && string.Equals(Filter, other.Filter, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is MqttSharedSubscriptionKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + (ShareName != null ? StringComparer.Ordinal.GetHashCode(ShareName) : 0);
                hash = (hash * 31) + (Filter != null ? StringComparer.Ordinal.GetHashCode(Filter) : 0);
                return hash;
            }
        }

        public override string ToString()
        {
            return ShareName + "/" + Filter;
        }
    }

    /// <summary>
    /// 全局共享订阅注册表。单锁保护；锁内只做选择与增删，不做入队或回调。
    /// </summary>
    internal sealed class MqttSharedSubscriptionRegistry
    {
        readonly object _syncRoot = new object();

        readonly Dictionary<MqttSharedSubscriptionKey, List<MqttSharedSubscriptionMember>> _members =
            new Dictionary<MqttSharedSubscriptionKey, List<MqttSharedSubscriptionMember>>();

        readonly Dictionary<MqttSharedSubscriptionKey, int> _roundRobin =
            new Dictionary<MqttSharedSubscriptionKey, int>();

        public void Add(string shareName, string filter, MqttSharedSubscriptionMember member)
        {
            if (string.IsNullOrEmpty(shareName)) throw new ArgumentNullException(nameof(shareName));
            if (string.IsNullOrEmpty(filter)) throw new ArgumentNullException(nameof(filter));
            if (member == null) throw new ArgumentNullException(nameof(member));
            if (string.IsNullOrEmpty(member.ClientId)) throw new ArgumentNullException(nameof(member));

            var key = new MqttSharedSubscriptionKey(shareName, filter);

            lock (_syncRoot)
            {
                List<MqttSharedSubscriptionMember> list;
                if (!_members.TryGetValue(key, out list))
                {
                    list = new List<MqttSharedSubscriptionMember>();
                    _members[key] = list;
                }

                var existingIndex = list.FindIndex(m => string.Equals(m.ClientId, member.ClientId, StringComparison.Ordinal));
                if (existingIndex >= 0)
                {
                    // 同一客户端重复订阅同一个键：原位替换，不重复加入轮转环。
                    list[existingIndex] = member;
                }
                else
                {
                    list.Add(member);
                }

                if (!_roundRobin.ContainsKey(key))
                {
                    _roundRobin[key] = 0;
                }
            }
        }

        public void Remove(string shareName, string filter, string clientId)
        {
            if (string.IsNullOrEmpty(shareName) || string.IsNullOrEmpty(filter) || string.IsNullOrEmpty(clientId))
            {
                return;
            }

            var key = new MqttSharedSubscriptionKey(shareName, filter);

            lock (_syncRoot)
            {
                List<MqttSharedSubscriptionMember> list;
                if (!_members.TryGetValue(key, out list))
                {
                    return;
                }

                list.RemoveAll(m => string.Equals(m.ClientId, clientId, StringComparison.Ordinal));

                if (list.Count == 0)
                {
                    _members.Remove(key);
                    _roundRobin.Remove(key);
                    return;
                }

                int index;
                if (_roundRobin.TryGetValue(key, out index) && index >= list.Count)
                {
                    _roundRobin[key] = index % list.Count;
                }
            }
        }

        public void RemoveClient(string clientId)
        {
            if (string.IsNullOrEmpty(clientId))
            {
                return;
            }

            lock (_syncRoot)
            {
                if (_members.Count == 0)
                {
                    return;
                }

                List<MqttSharedSubscriptionKey> emptyKeys = null;

                foreach (var pair in _members)
                {
                    pair.Value.RemoveAll(m => string.Equals(m.ClientId, clientId, StringComparison.Ordinal));

                    if (pair.Value.Count == 0)
                    {
                        if (emptyKeys == null)
                        {
                            emptyKeys = new List<MqttSharedSubscriptionKey>();
                        }

                        emptyKeys.Add(pair.Key);
                        continue;
                    }

                    int index;
                    if (_roundRobin.TryGetValue(pair.Key, out index) && index >= pair.Value.Count)
                    {
                        _roundRobin[pair.Key] = index % pair.Value.Count;
                    }
                }

                if (emptyKeys != null)
                {
                    foreach (var emptyKey in emptyKeys)
                    {
                        _members.Remove(emptyKey);
                        _roundRobin.Remove(emptyKey);
                    }
                }
            }
        }

        public IReadOnlyList<MqttSharedSubscriptionKey> GetMatchingKeys(string topic)
        {
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            lock (_syncRoot)
            {
                var result = new List<MqttSharedSubscriptionKey>();
                foreach (var pair in _members)
                {
                    if (pair.Value.Count == 0)
                    {
                        continue;
                    }

                    if (MqttTopicFilterComparer.IsMatch(topic, pair.Key.Filter))
                    {
                        result.Add(pair.Key);
                    }
                }

                return result;
            }
        }

        public bool TrySelect(MqttSharedSubscriptionKey key, out MqttSharedSubscriptionMember member)
        {
            lock (_syncRoot)
            {
                List<MqttSharedSubscriptionMember> list;
                if (!_members.TryGetValue(key, out list) || list.Count == 0)
                {
                    member = null;
                    return false;
                }

                int index;
                if (!_roundRobin.TryGetValue(key, out index))
                {
                    index = 0;
                }

                index = index % list.Count;
                if (index < 0)
                {
                    index = 0;
                }

                member = list[index];
                _roundRobin[key] = (index + 1) % list.Count;
                return true;
            }
        }

        public bool TrySelect(string shareName, string filter, out MqttSharedSubscriptionMember member)
        {
            if (string.IsNullOrEmpty(shareName) || string.IsNullOrEmpty(filter))
            {
                member = null;
                return false;
            }

            return TrySelect(new MqttSharedSubscriptionKey(shareName, filter), out member);
        }

        internal int GetMemberCount(string shareName, string filter)
        {
            var key = new MqttSharedSubscriptionKey(shareName, filter);

            lock (_syncRoot)
            {
                List<MqttSharedSubscriptionMember> list;
                return _members.TryGetValue(key, out list) ? list.Count : 0;
            }
        }

        internal int TotalMemberCount
        {
            get
            {
                lock (_syncRoot)
                {
                    var count = 0;
                    foreach (var pair in _members)
                    {
                        count += pair.Value.Count;
                    }

                    return count;
                }
            }
        }
    }
}