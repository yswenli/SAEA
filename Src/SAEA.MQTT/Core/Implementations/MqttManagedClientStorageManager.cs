/****************************************************************************
*项目名称：SAEA.MQTT.Core.Implementations
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Core.Implementations
*类 名 称：MqttManagedClientStorageManager
*版 本 号： v4.2.1.6
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/16 9:40:06
*描述：
*=====================================================================
*修改时间：2019/1/16 9:40:06
*修 改 人： yswenli
*版 本 号： v4.2.1.6
*描    述：
*****************************************************************************/
using SAEA.MQTT.Common;
using SAEA.MQTT.Interface;
using SAEA.MQTT.Model;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.Core.Implementations
{
    public class MqttManagedClientStorageManager
    {
        private readonly List<MqttManagedMessage> _messages = new List<MqttManagedMessage>();
        private readonly AsyncLock _messagesLock = new AsyncLock();

        private readonly IMqttManagedClientStorage _storage;

        public MqttManagedClientStorageManager(IMqttManagedClientStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public async Task<List<MqttManagedMessage>> LoadQueuedMessagesAsync()
        {
            var loadedMessages = await _storage.LoadQueuedMessagesAsync().ConfigureAwait(false);
            _messages.AddRange(loadedMessages);

            return _messages;
        }

        public async Task AddAsync(MqttManagedMessage applicationMessage)
        {
            if (applicationMessage == null) throw new ArgumentNullException(nameof(applicationMessage));

            using (await _messagesLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
            {
                _messages.Add(applicationMessage);
                await SaveAsync().ConfigureAwait(false);
            }
        }

        public async Task RemoveAsync(MqttManagedMessage applicationMessage)
        {
            if (applicationMessage == null) throw new ArgumentNullException(nameof(applicationMessage));

            using (await _messagesLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
            {
                var index = _messages.IndexOf(applicationMessage);
                if (index == -1)
                {
                    return;
                }

                _messages.RemoveAt(index);
                await SaveAsync().ConfigureAwait(false);
            }
        }

        private Task SaveAsync()
        {
            return _storage.SaveQueuedMessagesAsync(_messages);
        }
    }
}
