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
 *命名空间：SAEA.MQTT.Tests.TestInfrastructure
 *文件名： InMemoryServerStorage
 *版本号： v26.4.23.1
 *唯一标识：6f0f0f70-2a4b-4d4d-9a0e-2f0d7a0b1c0a
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2021/03/11 16:46:45
 *描述：InMemoryServerStorage类
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/05 00:00:00
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：InMemoryServerStorage类
 *
 *****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SAEA.MQTT.Internal;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Server;

namespace SAEA.MQTT.Tests.TestInfrastructure
{
    /// <summary>
    /// 同时实现旧 / 新 retained 存储接口的内存替身。
    /// 富接口路径保存绝对到期时间，用于验证重启后 Message Expiry 语义是否保留。
    /// </summary>
    internal sealed class InMemoryServerStorage : IMqttServerStorage, IMqttServerRetainedMessageStorage
    {
        readonly object _syncRoot = new object();
        List<MqttRetainedMessageEntry> _entries = new List<MqttRetainedMessageEntry>();

        public int EntryCount
        {
            get
            {
                lock (_syncRoot)
                {
                    return _entries.Count;
                }
            }
        }

        public bool ContainsTopic(string topic)
        {
            lock (_syncRoot)
            {
                return _entries.Any(entry => entry.Message != null && entry.Message.Topic == topic);
            }
        }

        Task IMqttServerStorage.SaveRetainedMessagesAsync(IList<MqttApplicationMessage> messages)
        {
            var now = MqttTime.UtcNow;

            lock (_syncRoot)
            {
                _entries = (messages ?? new List<MqttApplicationMessage>())
                    .Where(message => message != null)
                    .Select(message => new MqttRetainedMessageEntry(
                        CloneMessage(message),
                        now,
                        MqttMessageExpiry.GetExpiresAt(message.MessageExpiryInterval, now)))
                    .ToList();
            }

            return Task.CompletedTask;
        }

        Task<IList<MqttApplicationMessage>> IMqttServerStorage.LoadRetainedMessagesAsync()
        {
            lock (_syncRoot)
            {
                return Task.FromResult<IList<MqttApplicationMessage>>(
                    _entries.Select(entry => CloneMessage(entry.Message)).ToList());
            }
        }

        Task IMqttServerRetainedMessageStorage.SaveRetainedMessagesAsync(IList<MqttRetainedMessageEntry> entries)
        {
            lock (_syncRoot)
            {
                _entries = (entries ?? new List<MqttRetainedMessageEntry>())
                    .Where(entry => entry != null && entry.Message != null)
                    .Select(CloneEntry)
                    .ToList();
            }

            return Task.CompletedTask;
        }

        Task<IList<MqttRetainedMessageEntry>> IMqttServerRetainedMessageStorage.LoadRetainedMessagesAsync()
        {
            lock (_syncRoot)
            {
                return Task.FromResult<IList<MqttRetainedMessageEntry>>(_entries.Select(CloneEntry).ToList());
            }
        }

        static MqttRetainedMessageEntry CloneEntry(MqttRetainedMessageEntry entry)
        {
            return new MqttRetainedMessageEntry(
                CloneMessage(entry.Message),
                entry.RetainedAt,
                entry.ExpiresAt);
        }

        static MqttApplicationMessage CloneMessage(MqttApplicationMessage message)
        {
            if (message == null)
            {
                return null;
            }

            return new MqttApplicationMessage
            {
                Topic = message.Topic,
                Payload = message.Payload == null ? null : (byte[])message.Payload.Clone(),
                QualityOfServiceLevel = message.QualityOfServiceLevel,
                Retain = message.Retain,
                UserProperties = message.UserProperties == null ? null : new List<MqttUserProperty>(message.UserProperties),
                ContentType = message.ContentType,
                ResponseTopic = message.ResponseTopic,
                PayloadFormatIndicator = message.PayloadFormatIndicator,
                MessageExpiryInterval = message.MessageExpiryInterval,
                TopicAlias = message.TopicAlias,
                CorrelationData = message.CorrelationData == null ? null : (byte[])message.CorrelationData.Clone(),
                SubscriptionIdentifiers = message.SubscriptionIdentifiers == null ? null : new List<uint>(message.SubscriptionIdentifiers)
            };
        }
    }
}