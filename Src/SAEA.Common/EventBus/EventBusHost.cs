/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.Common.EventBus
*文件名： EventBusHost
*版本号： v26.11.5.1
*唯一标识：e24be0d3-2870-415d-9ff4-8ae0380d7a9d
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 12:40:36
*描述：事件总线宿主
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:45
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Common.EventBus
{
    /// <summary>
    /// 主题事件总线的分发宿主，为每个主题维护独立的消费循环
    /// </summary>
    public class EventBusHost : IDisposable
    {
        private readonly Dictionary<string, TopicChannel> _topics;
        private readonly Dictionary<string, Task> _dispatchTasks;
        private readonly CancellationTokenSource _cts;
        private bool _isRunning;
        private readonly object _lock = new object();

        /// <summary>
        /// 分发宿主是否正在运行
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// 用给定主题表创建分发宿主，主题的新增需在 Start 前完成
        /// </summary>
        /// <param name="topics">主题名与通道的映射，为 null 时用空表</param>
        public EventBusHost(Dictionary<string, TopicChannel> topics)
        {
            _topics = topics ?? new Dictionary<string, TopicChannel>();
            _dispatchTasks = new Dictionary<string, Task>();
            _cts = new CancellationTokenSource();
        }

        /// <summary>
        /// 启动宿主并为当前全部主题建立分发循环，已在运行时直接返回
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                if (_isRunning) return;
                _isRunning = true;
                
                foreach (var topic in _topics)
                {
                    StartTopicDispatchLoop(topic.Value, topic.Key);
                }
            }
        }

        /// <summary>
        /// 为单个主题启动消费循环，主题无订阅者时不启动；循环在取消令牌取消或订阅者清空后结束
        /// </summary>
        /// <param name="topicChannel">主题通道</param>
        /// <param name="topicName">主题名，用于登记分发任务</param>
        public void StartTopicDispatchLoop(TopicChannel topicChannel, string topicName)
        {
            if (topicChannel.GetSubscribers().Count == 0) return;
            
            var task = Task.Run(async () =>
            {
                try
                {
                    while (!_cts.IsCancellationRequested && topicChannel.GetSubscribers().Count > 0)
                    {
                        try
                        {
                            var msg = await topicChannel.ReadAsync(_cts.Token);
                            await DispatchToSubscribers(topicChannel, msg);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch
                        {
                            break;
                        }
                    }
                }
                catch (Exception)
                {
                }
            }, _cts.Token);
            
            _dispatchTasks[topicName] = task;
        }

        private async Task DispatchToSubscribers(TopicChannel topicChannel, EventMessage message)
        {
            var subscribers = topicChannel.GetSubscribers();
            
            foreach (var sub in subscribers)
            {
                await ExecuteWithRetry(sub, message);
            }
        }

        private async Task ExecuteWithRetry(EventSubscriber subscriber, EventMessage message)
        {
            var retryCount = subscriber.RetryCount;
            
            for (int attempt = 0; attempt <= retryCount; attempt++)
            {
                try
                {
                    await subscriber.Handler(message, _cts.Token);
                    return;
                }
                catch (Exception)
                {
                    if (attempt < retryCount)
                    {
                        var delay = subscriber.RetryDelay ?? TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt));
                        await Task.Delay(delay, _cts.Token);
                    }
                }
            }
        }

        /// <summary>
        /// 请求停止分发并在给定超时内等待循环退出，随后关闭各主题通道
        /// </summary>
        /// <param name="timeout">等待分发任务结束的最长时间，为 null 时不等待</param>
        /// <returns>停止完成的任务</returns>
        public async Task StopAsync(TimeSpan? timeout = null)
        {
            lock (_lock)
            {
                if (!_isRunning) return;
                _isRunning = false;
                _cts.Cancel();
            }

            if (timeout != null)
            {
                var tasks = new List<Task>(_dispatchTasks.Values);
                await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(timeout.Value));
            }
            
            foreach (var tc in _topics.Values)
            {
                tc.Complete();
            }
        }

        /// <summary>
        /// 同步停止分发并释放取消令牌
        /// </summary>
        public void Dispose()
        {
            StopAsync().GetAwaiter().GetResult();
            _cts?.Dispose();
        }
    }
}