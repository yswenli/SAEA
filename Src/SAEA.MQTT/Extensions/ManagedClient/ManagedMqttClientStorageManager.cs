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
*命名空间：SAEA.MQTT.Extensions.ManagedClient
*文件名： ManagedMqttClientStorageManager
*版本号： v26.4.23.1
*唯一标识：f43290aa-445a-4ab4-bba1-bba16a4ceda2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：ManagedMqttClientStorageManager接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SAEA.MQTT.Internal;

namespace SAEA.MQTT.Extensions.ManagedClient
{
    /// <summary>
    /// 待发消息的内存缓存，并把变更同步写入外部存储
    /// </summary>
    public class ManagedMqttClientStorageManager
    {
        private readonly List<ManagedMqttApplicationMessage> _messages = new List<ManagedMqttApplicationMessage>();
        private readonly AsyncLock _messagesLock = new AsyncLock();

        private readonly IManagedMqttClientStorage _storage;

        /// <summary>
        /// 使用指定存储初始化实例
        /// </summary>
        /// <param name="storage">消息持久化存储，不能为null</param>
        public ManagedMqttClientStorageManager(IManagedMqttClientStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        /// <summary>
        /// 从存储加载已保存的消息并合并到内存缓存
        /// </summary>
        /// <returns>内存缓存中的全部消息，为内部列表本身而非副本</returns>
        public async Task<List<ManagedMqttApplicationMessage>> LoadQueuedMessagesAsync()
        {
            var loadedMessages = await _storage.LoadQueuedMessagesAsync().ConfigureAwait(false);
            _messages.AddRange(loadedMessages);

            return _messages;
        }

        /// <summary>
        /// 加入内存缓存并立即持久化全部消息
        /// </summary>
        /// <param name="applicationMessage">待加入的消息，不能为null</param>
        /// <returns>持久化完成的异步任务</returns>
        public async Task AddAsync(ManagedMqttApplicationMessage applicationMessage)
        {
            if (applicationMessage == null) throw new ArgumentNullException(nameof(applicationMessage));

            using (await _messagesLock.WaitAsync(CancellationToken.None).ConfigureAwait(false))
            {
                _messages.Add(applicationMessage);
                await SaveAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 按引用从内存缓存移除消息并立即持久化，未找到时直接返回
        /// </summary>
        /// <param name="applicationMessage">待移除的消息，不能为null</param>
        /// <returns>持久化完成的异步任务</returns>
        public async Task RemoveAsync(ManagedMqttApplicationMessage applicationMessage)
        {
            if (applicationMessage == null) throw new ArgumentNullException(nameof(applicationMessage));

            using (await _messagesLock.WaitAsync(CancellationToken.None).ConfigureAwait(false))
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
