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
*唯一标识：027f46fd-2bbe-40c6-8a57-516b941b61f0
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MQTT服务端类
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MQTT服务端类
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
    public sealed class MqttRetainedMessagesManager : IMqttRetainedMessagesManager
    {
        readonly AsyncLock _storageAccessLock = new AsyncLock();
        readonly Dictionary<string, MqttApplicationMessage> _messages = new Dictionary<string, MqttApplicationMessage>();

        IMqttNetScopedLogger _logger;
        IMqttServerOptions _options;

        // TODO: Get rid of the logger here!
        public Task Start(IMqttServerOptions options, IMqttNetLogger logger)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            _logger = logger.CreateScopedLogger(nameof(MqttRetainedMessagesManager));

            _options = options ?? throw new ArgumentNullException(nameof(options));
            return PlatformAbstractionLayer.CompletedTask;
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

                    if (retainedMessages != null)
                    {
                        foreach (var retainedMessage in retainedMessages)
                        {
                            _messages[retainedMessage.Topic] = retainedMessage;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Unhandled exception while loading retained messages.");
            }
        }

        public async Task HandleMessageAsync(string clientId, MqttApplicationMessage applicationMessage)
        {
            if (applicationMessage == null) throw new ArgumentNullException(nameof(applicationMessage));

            try
            {
                List<MqttApplicationMessage> messagesForSave = null;
                var saveIsRequired = false;
                
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
                        if (!_messages.TryGetValue(applicationMessage.Topic, out var existingMessage))
                        {
                            _messages[applicationMessage.Topic] = applicationMessage;
                            saveIsRequired = true;
                        }
                        else
                        {
                            if (existingMessage.QualityOfServiceLevel != applicationMessage.QualityOfServiceLevel || !existingMessage.Payload.SequenceEqual(applicationMessage.Payload ?? PlatformAbstractionLayer.EmptyByteArray))
                            {
                                _messages[applicationMessage.Topic] = applicationMessage;
                                saveIsRequired = true;
                            }
                        }

                        _logger.Verbose("Client '{0}' set retained message for topic '{1}'.", clientId, applicationMessage.Topic);
                    }

                    if (saveIsRequired)
                    {
                        messagesForSave = new List<MqttApplicationMessage>(_messages.Values);
                    }
                }

                if (saveIsRequired)
                {
                    if (_options.Storage != null)
                    {
                        using (await _storageAccessLock.WaitAsync(CancellationToken.None).ConfigureAwait(false))
                        {
                            await _options.Storage.SaveRetainedMessagesAsync(messagesForSave).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Unhandled exception while handling retained messages.");
            }
        }

        public Task<IList<MqttApplicationMessage>> GetSubscribedMessagesAsync(ICollection<MqttTopicFilter> topicFilters)
        {
            if (topicFilters == null) throw new ArgumentNullException(nameof(topicFilters));

            var matchingRetainedMessages = new List<MqttApplicationMessage>();

            List<MqttApplicationMessage> retainedMessages;
            lock (_messages)
            {
                retainedMessages = _messages.Values.ToList();
            }

            foreach (var retainedMessage in retainedMessages)
            {
                foreach (var topicFilter in topicFilters)
                {
                    if (!MqttTopicFilterComparer.IsMatch(retainedMessage.Topic, topicFilter.Topic))
                    {
                        continue;
                    }

                    matchingRetainedMessages.Add(retainedMessage);
                    break;
                }
            }

            return Task.FromResult((IList<MqttApplicationMessage>)matchingRetainedMessages);
        }

        public Task<IList<MqttApplicationMessage>> GetMessagesAsync()
        {
            lock (_messages)
            {
                return Task.FromResult((IList<MqttApplicationMessage>)_messages.Values.ToList());
            }
        }

        public async Task ClearMessagesAsync()
        {
            lock (_messages)
            {
                _messages.Clear();
            }

            if (_options.Storage != null)
            {
                using (await _storageAccessLock.WaitAsync(CancellationToken.None).ConfigureAwait(false))
                {
                    await _options.Storage.SaveRetainedMessagesAsync(new List<MqttApplicationMessage>()).ConfigureAwait(false);
                }
            }
        }
    }
}
