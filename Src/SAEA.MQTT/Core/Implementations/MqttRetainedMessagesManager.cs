/****************************************************************************
*项目名称：SAEA.MQTT.Core.Implementations
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Core.Implementations
*类 名 称：MqttRetainedMessagesManager
*版 本 号： v4.1.2.5
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 15:36:19
*描述：
*=====================================================================
*修改时间：2019/1/15 15:36:19
*修 改 人： yswenli
*版 本 号： v4.1.2.5
*描    述：
*****************************************************************************/
using SAEA.MQTT.Common.Log;
using SAEA.MQTT.Interface;
using SAEA.MQTT.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SAEA.MQTT.Core.Implementations
{
    public class MqttRetainedMessagesManager
    {
        private readonly Dictionary<string, MqttMessage> _messages = new Dictionary<string, MqttMessage>();

        private readonly IMqttNetChildLogger _logger;
        private readonly IMqttServerOptions _options;

        public MqttRetainedMessagesManager(IMqttServerOptions options, IMqttNetChildLogger logger)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            _logger = logger.CreateChildLogger(nameof(MqttRetainedMessagesManager));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task LoadMessagesAsync()
        {
            if (_options.Storage == null)
            {
                return;
            }

            try
            {
                var retainedMessages = await _options.Storage.LoadRetainedMessagesAsync().ConfigureAwait(false);

                lock (_messages)
                {
                    _messages.Clear();
                    foreach (var retainedMessage in retainedMessages)
                    {
                        _messages[retainedMessage.Topic] = retainedMessage;
                    }
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Unhandled exception while loading retained messages.");
            }
        }

        public async Task HandleMessageAsync(string clientId, MqttMessage applicationMessage)
        {
            if (applicationMessage == null) throw new ArgumentNullException(nameof(applicationMessage));

            try
            {
                List<MqttMessage> messagesForSave = null;
                lock (_messages)
                {
                    var saveIsRequired = false;

                    if (applicationMessage.Payload?.Length == 0)
                    {
                        saveIsRequired = _messages.Remove(applicationMessage.Topic);
                        _logger.Verbose("Client '{0}' cleared retained message for topic '{1}'.", clientId, applicationMessage.Topic);
                    }
                    else
                    {
                        if (!_messages.TryGetValue(applicationMessage.Topic, out var existingMessage))
                        {
                            _messages[applicationMessage.Topic] = applicationMessage;
                            saveIsRequired = true;
                        }
                        else
                        {
                            if (existingMessage.QualityOfServiceLevel != applicationMessage.QualityOfServiceLevel || !existingMessage.Payload.SequenceEqual(applicationMessage.Payload ?? new byte[0]))
                            {
                                _messages[applicationMessage.Topic] = applicationMessage;
                                saveIsRequired = true;
                            }
                        }

                        _logger.Verbose("Client '{0}' set retained message for topic '{1}'.", clientId, applicationMessage.Topic);
                    }

                    if (saveIsRequired)
                    {
                        messagesForSave = new List<MqttMessage>(_messages.Values);
                    }
                }

                if (messagesForSave == null)
                {
                    _logger.Verbose("Skipped saving retained messages because no changes were detected.");
                    return;
                }

                if (_options.Storage != null)
                {
                    await _options.Storage.SaveRetainedMessagesAsync(messagesForSave).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Unhandled exception while handling retained messages.");
            }
        }

        public IList<MqttMessage> GetSubscribedMessages(ICollection<TopicFilter> topicFilters)
        {
            var retainedMessages = new List<MqttMessage>();

            lock (_messages)
            {
                foreach (var retainedMessage in _messages.Values)
                {
                    foreach (var topicFilter in topicFilters)
                    {
                        if (!MqttTopicFilterComparer.IsMatch(retainedMessage.Topic, topicFilter.Topic))
                        {
                            continue;
                        }

                        retainedMessages.Add(retainedMessage);
                        break;
                    }
                }
            }

            return retainedMessages;
        }

        public IList<MqttMessage> GetMessages()
        {
            lock (_messages)
            {
                return _messages.Values.ToList();
            }
        }

        public Task ClearMessagesAsync()
        {
            lock (_messages)
            {
                _messages.Clear();
            }

            if (_options.Storage != null)
            {
                return _options.Storage.SaveRetainedMessagesAsync(new List<MqttMessage>());
            }

            return Task.FromResult((object)null);
        }
    }
}
