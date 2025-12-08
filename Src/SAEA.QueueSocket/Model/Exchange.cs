/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.QueueSocket.Model
*文件名： Exchange
*版本号： v7.0.0.1
*唯一标识：6a576aad-edcc-446d-b7e5-561a622549bf
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/5 16:36:44
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/5 16:36:44
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Common.Caching;
using SAEA.Sockets.Interface;

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
        private ConcurrentDictionary<string, Task> _dispatchTasks;

        /// <summary>
        /// 初始化Exchange类的新实例
        /// </summary>
        /// <param name="maxPendingMsgCount">消息队列最大堆积数量，默认10000000</param>
        public Exchange(int maxPendingMsgCount = 10000000)
        {
            _binding = new Binding();

            _messageQueue = new MessageQueue(maxPendingMsgCount);

            // 优化：将批处理超时时间从100ms改为20ms，提高消息处理频率，同时增加批量大小到5000以提高吞吐量
            _classificationBatcher = ClassificationBatcher.GetInstance(5000, 20);

            _classificationBatcher.OnBatched += _classificationBatcher_OnBatched;

            _subscribers = new ConcurrentDictionary<string, ConcurrentDictionary<string, Net.QueueCoder>>();

            _dispatchTasks = new ConcurrentDictionary<string, Task>();
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
                var topicSubscribers = _subscribers.GetOrAdd(sInfo.Topic,
                    (topic) => new ConcurrentDictionary<string, Net.QueueCoder>());

                topicSubscribers.AddOrUpdate(sessionID, qcoder, (id, oldCoder) => qcoder);

                // 启动或获取该主题的消息分发任务，使用Task.Run确保任务立即执行
                _dispatchTasks.GetOrAdd(sInfo.Topic, topic => Task.Run(async () =>
                {
                    // 批量处理参数
                    const int batchSize = 1000; // 每次处理1000条消息
                    const int maxWaitTime = 50; // 最大等待时间50ms
                    var stopwatch = new System.Diagnostics.Stopwatch();

                    while (_subscribers.TryGetValue(topic, out var subs) && subs.Count > 0)
                    {
                        var messages = new List<byte[]>();
                        stopwatch.Restart();

                        // 批量获取消息
                        while (messages.Count < batchSize && stopwatch.ElapsedMilliseconds < maxWaitTime)
                        {
                            var msg = await _messageQueue.DequeueAsync(topic);
                            if (msg != null && msg.Length > 0)
                            {
                                messages.Add(msg);
                            }
                            else
                            {
                                // 没有更多消息，退出循环
                                break;
                            }
                        }

                        // 如果有消息需要处理
                        if (messages.Count > 0)
                        {
                            // 复制当前订阅者列表，避免在分发过程中修改列表
                            var currentSubs = subs.ToArray();

                            // 对每个订阅者批量发送消息
                            foreach (var sub in currentSubs)
                            {
                                // 检查订阅者是否仍然存在
                                if (subs.TryGetValue(sub.Key, out var coder))
                                {
                                    // 获取订阅者的主题信息（只需要获取一次）
                                    var bindInfo = _binding.GetBingInfo(sub.Key);
                                    if (bindInfo != null)
                                    {
                                        // 批量处理消息
                                        foreach (var msg in messages)
                                        {
                                            Interlocked.Increment(ref _outNum);
                                            _classificationBatcher.Insert(sub.Key, coder.Data(bindInfo.Name, topic, msg));
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // 当没有订阅者时，移除该主题的分发任务
                    _dispatchTasks.TryRemove(topic, out var _);
                }));
            }
        }

        /// <summary>
        /// 取消订阅
        /// </summary>
        /// <param name="sInfo">队列消息</param>
        public void Unsubscribe(QueueMsg sInfo)
        {
            Interlocked.Decrement(ref _cNum);
            _binding.Del(sInfo.Name, sInfo.Topic);
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
            foreach (var topic in _subscribers.Keys)
            {
                if (_subscribers.TryGetValue(topic, out var subscribers))
                {
                    subscribers.TryRemove(sessionID, out var _);
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
