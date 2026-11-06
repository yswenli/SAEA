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
 *文件名： MqttRetainedMessagesManager
 *版本号： v26.4.23.1
 *唯一标识：178087a8-9928-4694-b102-a8ecb627b9a9
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2021/03/11 16:46:45
 *描述：MqttRetainedMessagesManager接口
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/6 12:04:26
 *修改人： yswenli
 *版本号： v26.11.5.1
 *描述：补全公开成员的 XML 注释
 *
 *****************************************************************************/
using SAEA.MQTT.Diagnostics;
using SAEA.MQTT.Implementations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SAEA.MQTT.Internal;

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 管理 retained 消息的内存表与持久化，按主题覆盖保存并在访问时惰性清理到期条目
    /// </summary>
    public sealed class MqttRetainedMessagesManager : IMqttRetainedMessagesManager
    {
        readonly AsyncLock _storageAccessLock = new AsyncLock();
        readonly Dictionary<string, MqttRetainedMessageEntry> _messages = new Dictionary<string, MqttRetainedMessageEntry>();

        IMqttNetScopedLogger _logger;
        IMqttServerOptions _options;

        // TODO: Get rid of the logger here!
        /// <summary>
        /// 注入服务端选项与日志器，完成使用前的初始化
        /// </summary>
        /// <param name="options">服务端选项，其中的存储实现决定是否持久化</param>
        /// <param name="logger">日志器，不能为 null</param>
        /// <returns>已完成的任务</returns>
        public Task Start(IMqttServerOptions options, IMqttNetLogger logger)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            _logger = logger.CreateScopedLogger(nameof(MqttRetainedMessagesManager));

            _options = options ?? throw new ArgumentNullException(nameof(options));
            return PlatformAbstractionLayer.CompletedTask;
        }

        /// <summary>
        /// 从存储加载 retained 消息并重建内存表，未配置存储时直接返回
        /// </summary>
        /// <returns>加载完成的任务，存储异常只记录日志不抛出</returns>
        public async Task LoadMessagesAsync()
        {
            if (_options.Storage == null)
            {
                return;
            }

            try
            {
                var now = MqttTime.UtcNow;

                IList<MqttRetainedMessageEntry> loadedEntries;
                var richStorage = _options.Storage as IMqttServerRetainedMessageStorage;
                if (richStorage != null)
                {
                    loadedEntries = await richStorage.LoadRetainedMessagesAsync().ConfigureAwait(false);
                }
                else
                {
                    // 旧存储只保存消息本身：RetainedAt 取当前时间，ExpiresAt 由消息自带的 interval 推导。
                    var retainedMessages = await _options.Storage.LoadRetainedMessagesAsync().ConfigureAwait(false);

                    loadedEntries = new List<MqttRetainedMessageEntry>();
                    if (retainedMessages != null)
                    {
                        foreach (var retainedMessage in retainedMessages)
                        {
                            if (retainedMessage == null)
                            {
                                continue;
                            }

                            loadedEntries.Add(new MqttRetainedMessageEntry(
                                retainedMessage,
                                now,
                                MqttMessageExpiry.GetExpiresAt(retainedMessage.MessageExpiryInterval, now)));
                        }
                    }
                }

                lock (_messages)
                {
                    _messages.Clear();

                    if (loadedEntries != null)
                    {
                        foreach (var entry in loadedEntries)
                        {
                            if (entry == null || entry.Message == null)
                            {
                                continue;
                            }

                            if (!entry.ExpiresAt.HasValue && entry.Message.MessageExpiryInterval.HasValue)
                            {
                                // 富存储实现未写 ExpiresAt 时的兼容兜底。
                                entry.ExpiresAt = MqttMessageExpiry.GetExpiresAt(entry.Message.MessageExpiryInterval, now);
                            }

                            _messages[entry.Message.Topic] = entry;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Unhandled exception while loading retained messages.");
            }
        }

        /// <summary>
        /// 按主题写入或清除 retained 消息，仅在内容或到期时间变化时写回存储
        /// </summary>
        /// <param name="clientId">发布方客户端标识，仅用于日志</param>
        /// <param name="applicationMessage">发布的应用消息，载荷为空的视作清除该主题的 retained 消息</param>
        /// <returns>处理完成的任务，存储异常只记录日志不抛出</returns>
        public async Task HandleMessageAsync(string clientId, MqttApplicationMessage applicationMessage)
        {
            if (applicationMessage == null) throw new ArgumentNullException(nameof(applicationMessage));

            try
            {
                List<MqttRetainedMessageEntry> entriesForSave = null;
                var saveIsRequired = false;
                var now = MqttTime.UtcNow;

                lock (_messages)
                {
                    var hasPayload = applicationMessage.Payload != null && applicationMessage.Payload.Length > 0;

                    if (!hasPayload)
                    {
                        saveIsRequired = _messages.Remove(applicationMessage.Topic);
                        _logger.Verbose("Client '{0}' cleared retained message for topic '{1}'.", clientId, applicationMessage.Topic);
                    }
                    else
                    {
                        var expiresAt = MqttMessageExpiry.GetExpiresAt(applicationMessage.MessageExpiryInterval, now);

                        if (expiresAt.HasValue && expiresAt.Value <= now)
                        {
                            // 已过期的 retained 消息不存储，并移除同 Topic 的旧条目。
                            saveIsRequired = _messages.Remove(applicationMessage.Topic);
                            _logger.Verbose("Client '{0}' published already expired retained message for topic '{1}'.", clientId, applicationMessage.Topic);
                        }
                        else
                        {
                            var newEntry = new MqttRetainedMessageEntry(applicationMessage, now, expiresAt);

                            if (!_messages.TryGetValue(applicationMessage.Topic, out var existingEntry))
                            {
                                _messages[applicationMessage.Topic] = newEntry;
                                saveIsRequired = true;
                            }
                            else
                            {
                                // retained 覆盖必须刷新 RetainedAt/ExpiresAt；仅在内容或到期时间变化时写存储。
                                _messages[applicationMessage.Topic] = newEntry;

                                saveIsRequired =
                                    existingEntry.ExpiresAt != expiresAt ||
                                    existingEntry.Message.QualityOfServiceLevel != applicationMessage.QualityOfServiceLevel ||
                                    !existingEntry.Message.Payload.SequenceEqual(applicationMessage.Payload ?? PlatformAbstractionLayer.EmptyByteArray);
                            }

                            _logger.Verbose("Client '{0}' set retained message for topic '{1}'.", clientId, applicationMessage.Topic);
                        }
                    }

                    if (saveIsRequired)
                    {
                        entriesForSave = _messages.Values.ToList();
                    }
                }

                if (saveIsRequired)
                {
                    await SaveEntriesAsync(entriesForSave).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Unhandled exception while handling retained messages.");
            }
        }

        /// <summary>
        /// 获取与给定主题过滤器匹配的 retained 消息
        /// </summary>
        /// <param name="topicFilters">客户端订阅的主题过滤器集合</param>
        /// <returns>匹配到的 retained 消息列表，无匹配时为空列表</returns>
        public async Task<IList<MqttApplicationMessage>> GetSubscribedMessagesAsync(ICollection<MqttTopicFilter> topicFilters)
        {
            var matchingEntries = await GetSubscribedMessageEntriesAsync(topicFilters).ConfigureAwait(false);
            return matchingEntries.Select(entry => entry.Message).ToList();
        }

        /// <summary>
        /// 获取与给定主题过滤器匹配的 retained 条目（含存留时间元数据），并清理已到期条目
        /// </summary>
        /// <param name="topicFilters">客户端订阅的主题过滤器集合，不能为 null</param>
        /// <returns>匹配到的 retained 条目列表，同一主题只返回一次</returns>
        public async Task<IList<MqttRetainedMessageEntry>> GetSubscribedMessageEntriesAsync(ICollection<MqttTopicFilter> topicFilters)
        {
            if (topicFilters == null) throw new ArgumentNullException(nameof(topicFilters));

            var now = MqttTime.UtcNow;
            var removedExpiredEntries = false;

            List<MqttRetainedMessageEntry> retainedEntries;
            lock (_messages)
            {
                var expiredTopics = _messages
                    .Where(pair => pair.Value.ExpiresAt.HasValue && now >= pair.Value.ExpiresAt.Value)
                    .Select(pair => pair.Key)
                    .ToList();

                foreach (var expiredTopic in expiredTopics)
                {
                    _messages.Remove(expiredTopic);
                    removedExpiredEntries = true;
                }

                retainedEntries = _messages.Values.ToList();
            }

            if (removedExpiredEntries)
            {
                // 尽力把惰性清理结果写回存储；失败只记录日志。
                await PersistCurrentEntriesAsync().ConfigureAwait(false);
            }

            var matchingRetainedMessages = new List<MqttRetainedMessageEntry>();
            foreach (var retainedEntry in retainedEntries)
            {
                foreach (var topicFilter in topicFilters)
                {
                    if (!MqttTopicFilterComparer.IsMatch(retainedEntry.Message.Topic, topicFilter.Topic))
                    {
                        continue;
                    }

                    matchingRetainedMessages.Add(retainedEntry);
                    break;
                }
            }

            return matchingRetainedMessages;
        }

        /// <summary>
        /// 获取全部未到期的 retained 消息，并清理已到期条目
        /// </summary>
        /// <returns>retained 消息列表，无消息时为空列表</returns>
        public async Task<IList<MqttApplicationMessage>> GetMessagesAsync()
        {
            var now = MqttTime.UtcNow;
            var removedExpiredEntries = false;

            List<MqttApplicationMessage> messages;
            lock (_messages)
            {
                var expiredTopics = _messages
                    .Where(pair => pair.Value.ExpiresAt.HasValue && now >= pair.Value.ExpiresAt.Value)
                    .Select(pair => pair.Key)
                    .ToList();

                foreach (var expiredTopic in expiredTopics)
                {
                    _messages.Remove(expiredTopic);
                    removedExpiredEntries = true;
                }

                messages = _messages.Values.Select(entry => entry.Message).ToList();
            }

            if (removedExpiredEntries)
            {
                await PersistCurrentEntriesAsync().ConfigureAwait(false);
            }

            return messages;
        }

        /// <summary>
        /// 清空内存中的 retained 消息，配置了存储时同步写回空集合
        /// </summary>
        /// <returns>清空完成的任务</returns>
        public async Task ClearMessagesAsync()
        {
            lock (_messages)
            {
                _messages.Clear();
            }

            if (_options.Storage != null)
            {
                await SaveEntriesAsync(new List<MqttRetainedMessageEntry>()).ConfigureAwait(false);
            }
        }

        async Task PersistCurrentEntriesAsync()
        {
            if (_options.Storage == null)
            {
                return;
            }

            try
            {
                List<MqttRetainedMessageEntry> entries;
                lock (_messages)
                {
                    entries = _messages.Values.ToList();
                }

                await SaveEntriesAsync(entries).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Unhandled exception while persisting retained messages.");
            }
        }

        async Task SaveEntriesAsync(IList<MqttRetainedMessageEntry> entries)
        {
            if (_options.Storage == null)
            {
                return;
            }

            using (await _storageAccessLock.WaitAsync(CancellationToken.None).ConfigureAwait(false))
            {
                var richStorage = _options.Storage as IMqttServerRetainedMessageStorage;
                if (richStorage != null)
                {
                    await richStorage.SaveRetainedMessagesAsync(entries).ConfigureAwait(false);
                }
                else
                {
                    // 旧存储接口只能保存消息本身，时间戳会丢失（已知限制）。
                    var messages = entries.Select(entry => entry.Message).ToList();
                    await _options.Storage.SaveRetainedMessagesAsync(messages).ConfigureAwait(false);
                }
            }
        }
    }
}