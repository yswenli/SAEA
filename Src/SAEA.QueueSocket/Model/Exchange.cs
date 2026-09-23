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
*命名空间：SAEA.QueueSocket.Model
*文件名： Exchange
*版本号： v26.4.23.1
*唯一标识：8c048dc9-271d-4719-ba99-b96628e40e6a
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：Exchange交换类
*
*=====================================================================
*修改标记
*修改时间：2018/03/18 02:16:04
*修改人： yswenli
*版本号： v26.4.23.1
*描述：Exchange交换类
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Common.Caching;
using SAEA.Sockets.Interface;
using SAEA.QueueSocket.Net;
using SAEA.QueueSocket.Type;

namespace SAEA.QueueSocket.Model
{
    /// <summary>
    /// Exchange类，实现ISyncBase接口
    /// </summary>
    class Exchange : ISyncBase, IDisposable
    {
        // 同步锁对象
        object _syncLocker = new object();

        // 分类批量打包器
        ClassificationBatcher _classificationBatcher;

        /// <summary>
        /// 分类批量打事件
        /// </summary>
        public event OnClassificationBatchedHandler OnBatched;

        /// <summary>
        /// 获取同步锁对象
        /// </summary>
        public object SyncLocker
        {
            get
            {
                return _syncLocker;
            }
        }

        // 发布者数量
        long _pNum = 0;

        // 订阅者数量
        long _cNum = 0;

        // 接收消息数量
        long _inNum = 0;

        // 发送消息数量
        long _outNum = 0;

        // 绑定对象
        private Binding _binding;

        // 消息队列对象
        private MessageQueue _messageQueue;

        // 订阅者消息处理队列
        private ConcurrentDictionary<string, ConcurrentDictionary<string, Net.QueueCoder>> _subscribers;

        // 消息分发任务字典
        private ConcurrentDictionary<string, Lazy<Task>> _dispatchTasks;

        /// <summary>
        /// 初始化Exchange类的新实例
        /// </summary>
        /// <param name="maxPendingMsgCount">消息队列最大堆积数量，默认10000000</param>
        public Exchange(int maxPendingMsgCount = 10000000)
        {
            _binding = new Binding();

            _messageQueue = new MessageQueue(maxPendingMsgCount);

            // 优化：将批处理超时时间从20ms改为100ms，降低CPU使用率，同时保持批量大小5000以提高吞吐量
            _classificationBatcher = ClassificationBatcher.GetInstance(5000, 100);

            _classificationBatcher.OnBatched += _classificationBatcher_OnBatched;

            _subscribers = new ConcurrentDictionary<string, ConcurrentDictionary<string, Net.QueueCoder>>();

            _dispatchTasks = new ConcurrentDictionary<string, Lazy<Task>>();
        }

        /// <summary>
        /// 分类批量打事件处理程序
        /// </summary>
        /// <param name="id">分类ID</param>
        /// <param name="data">数据</param>
        private void _classificationBatcher_OnBatched(string id, byte[] data)
        {
            OnBatched?.Invoke(id, data);
        }

        /// <summary>
        /// 接受发布消息
        /// </summary>
        /// <param name="sessionID">会话ID</param>
        /// <param name="pInfo">队列消息</param>
        public void AcceptPublish(string sessionID, QueueMsg pInfo)
        {
            _binding.Set(sessionID, pInfo.Name, pInfo.Topic);

            _messageQueue.Enqueue(pInfo.Topic, pInfo.Data);

            _pNum = _binding.GetPublisherCount();

            Interlocked.Increment(ref _inNum);
        }



        /// <summary>
        /// 获取订阅数据
        /// </summary>
        /// <param name="sessionID">会话ID</param>
        /// <param name="sInfo">队列消息</param>
        /// <param name="qcoder">队列编码器</param>
        public void GetSubscribeData(string sessionID, QueueMsg sInfo, Net.QueueCoder qcoder)
        {
            if (!_binding.Exists(sInfo))
            {
                _binding.Set(sessionID, sInfo.Name, sInfo.Topic, false);

                _cNum = _binding.GetSubscriberCount();

                // 将订阅者添加到订阅者字典
                lock (_syncLocker)
                {
                    var topicSubscribers = _subscribers.GetOrAdd(sInfo.Topic,
                        (topic) => new ConcurrentDictionary<string, Net.QueueCoder>());

                    topicSubscribers.AddOrUpdate(sessionID, qcoder, (id, oldCoder) => qcoder);

                    EnsureDispatcher(sInfo.Topic);
                }
            }
        }

        void EnsureDispatcher(string topic)
        {
            _ = _dispatchTasks.GetOrAdd(topic, t => new Lazy<Task>(() => Task.Run(() => DispatchLoop(t)), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        }

        async Task DispatchLoop(string topic)
        {
            const int batchSize = 1000;
            const int maxWaitTime = 50;
            var stopwatch = new System.Diagnostics.Stopwatch();

            while (true)
            {
                if (!(_subscribers.TryGetValue(topic, out var subs) && subs.Count > 0))
                {
                    lock (_syncLocker)
                    {
                        if (_subscribers.TryGetValue(topic, out subs) && subs.Count > 0)
                        {
                            continue;
                        }

                        _dispatchTasks.TryRemove(topic, out var _);

                        break;
                    }
                }

                try
                {
                    var messages = new List<byte[]>();
                    stopwatch.Restart();

                    while (messages.Count < batchSize && stopwatch.ElapsedMilliseconds < maxWaitTime)
                    {
                        if (!_messageQueue.TryDequeue(topic, out var msg))
                        {
                            await Task.Delay(5);
                            continue;
                        }

                        if (msg != null && msg.Length > 0)
                        {
                            messages.Add(msg);
                        }
                    }

                    if (messages.Count > 0)
                    {
                        var currentSubs = subs.ToArray();

                        foreach (var sub in currentSubs)
                        {
                            try
                            {
                                if (subs.TryGetValue(sub.Key, out var coder))
                                {
                                    var bindInfo = _binding.GetBingInfo(sub.Key);
                                    if (bindInfo != null)
                                    {
                                        var nameBytes = string.IsNullOrEmpty(bindInfo.Name) ? null : Encoding.UTF8.GetBytes(bindInfo.Name);
                                        var topicBytes = string.IsNullOrEmpty(topic) ? null : Encoding.UTF8.GetBytes(topic);
                                        var fixedLen = 1 + 12 + (nameBytes == null ? 0 : nameBytes.Length) + (topicBytes == null ? 0 : topicBytes.Length);

                                        long bufferSize = 0;
                                        for (int i = 0; i < messages.Count; i++)
                                        {
                                            bufferSize += fixedLen + messages[i].Length;
                                        }

                                        var buffer = new byte[bufferSize];
                                        var bufOffset = 0;
                                        for (int i = 0; i < messages.Count; i++)
                                        {
                                            bufOffset = QueueCoder.WriteFrame(buffer, bufOffset, QueueSocketMsgType.Data, nameBytes, topicBytes, messages[i]);
                                            Interlocked.Increment(ref _outNum);
                                        }

                                        _classificationBatcher.Insert(sub.Key, buffer);
                                    }
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                }
                catch
                {
                    await Task.Delay(10);
                }
            }
        }

        /// <summary>
        /// 取消订阅
        /// </summary>
        /// <param name="sessionID">会话ID</param>
        /// <param name="sInfo">队列消息</param>
        public void Unsubscribe(string sessionID, QueueMsg sInfo)
        {
            Interlocked.Decrement(ref _cNum);
            _binding.Del(sessionID, sInfo.Topic);

            lock (_syncLocker)
            {
                if (_subscribers.TryGetValue(sInfo.Topic, out var topicSubscribers))
                {
                    topicSubscribers.TryRemove(sessionID, out var _);

                    if (topicSubscribers.IsEmpty)
                    {
                        _subscribers.TryRemove(sInfo.Topic, out var _);
                    }
                }
            }
        }

        /// <summary>
        /// 清除会话
        /// </summary>
        /// <param name="sessionID">会话ID</param>
        public void Clear(string sessionID)
        {
            lock (_syncLocker)
            {
                var data = _binding.GetBingInfo(sessionID);

                if (data != null)
                {
                    if (data.Flag)
                    {
                        Interlocked.Decrement(ref _pNum);
                    }
                    else
                    {
                        Interlocked.Decrement(ref _cNum);
                    }
                    _binding.Remove(sessionID);
                }
            }
        }

        /// <summary>
        /// 获取连接信息
        /// </summary>
        /// <returns>连接信息元组</returns>
        public Tuple<long, long, long, long> GetConnectInfo()
        {
            return new Tuple<long, long, long, long>(_pNum, _cNum, _inNum, _outNum);
        }

        /// <summary>
        /// 获取队列信息
        /// </summary>
        /// <returns>队列信息列表</returns>
        public List<Tuple<string, long>> GetQueueInfo()
        {
            List<Tuple<string, long>> result = new List<Tuple<string, long>>();
            var dic = _messageQueue.ToList();
            if (!dic.IsEmpty)
            {
                foreach (var item in dic)
                {
                    var count = _messageQueue.GetCount(item.Key);
                    var t = new Tuple<string, long>(item.Key, count);
                    result.Add(t);
                }
            }
            return result;
        }

        /// <summary>
        /// 处理会话断开
        /// </summary>
        /// <param name="sessionID"></param>
        public void SessionClosed(string sessionID)
        {
            _binding.Remove(sessionID);
            _cNum = _binding.GetSubscriberCount();

            // 从订阅者字典中移除会话ID
            lock (_syncLocker)
            {
                foreach (var topic in _subscribers.Keys)
                {
                    if (_subscribers.TryGetValue(topic, out var subscribers))
                    {
                        subscribers.TryRemove(sessionID, out var _);

                        if (subscribers.IsEmpty)
                        {
                            _subscribers.TryRemove(topic, out var _);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            // 清理订阅者字典
            if (_subscribers != null)
            {
                _subscribers.Clear();
            }

            // 清理分发任务字典
            if (_dispatchTasks != null)
            {
                _dispatchTasks.Clear();
            }

            // 释放其他资源
            if (_binding != null)
            {
                _binding.Dispose();
            }

            if (_messageQueue != null)
            {
                _messageQueue.Dispose();
            }
        }
    }
}