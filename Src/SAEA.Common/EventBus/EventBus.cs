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
*文件名： EventBus
*版本号： v26.11.5.1
*唯一标识：fe3db3e5-7a89-487c-a21e-aa39383e7b8e
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 12:40:36
*描述：进程内事件总线
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Common.EventBus
{
    /// <summary>
    /// 进程内主题事件总线，按主题把消息分发给订阅者；未启动时发布的消息直接丢弃
    /// </summary>
    public static class EventBus
    {
        private static readonly object _lock = new object();
        private static readonly ConcurrentDictionary<string, TopicChannel> _topics = new ConcurrentDictionary<string, TopicChannel>();
        private static EventBusHost _host;

        /// <summary>
        /// 总线是否已启动，未启动时发布的消息被丢弃
        /// </summary>
        public static bool Enabled { get; private set; }

        /// <summary>
        /// 预先注册主题通道，主题已存在时忽略新容量
        /// </summary>
        /// <param name="topic">主题名</param>
        /// <param name="capacity">消息队列容量，大于 0 时有界且写满时等待，否则无界</param>
        public static void RegisterTopic(string topic, int capacity = 0)
        {
            _topics.GetOrAdd(topic, _ => new TopicChannel(capacity));
        }

        /// <summary>
        /// 同步发布消息，主题未注册时丢弃，有界队列写满时阻塞调用线程
        /// </summary>
        /// <param name="topic">主题名</param>
        /// <param name="data">消息数据</param>
        /// <param name="context">随消息传递的上下文对象，可为 null</param>
        public static void Publish(string topic, byte[] data, object context = null)
        {
            if (!Enabled) return;

            var msg = new EventMessage(topic, data, context);
            if (_topics.TryGetValue(topic, out var tc))
            {
                tc.PublishAsync(msg).GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// 异步发布消息，主题未注册时丢弃，有界队列写满时等待
        /// </summary>
        /// <param name="topic">主题名</param>
        /// <param name="data">消息数据</param>
        /// <param name="context">随消息传递的上下文对象，可为 null</param>
        /// <returns>写入通道完成的任务</returns>
        public static async ValueTask PublishAsync(string topic, byte[] data, object context = null)
        {
            if (!Enabled) return;

            var msg = new EventMessage(topic, data, context);
            if (_topics.TryGetValue(topic, out var tc))
            {
                await tc.PublishAsync(msg);
            }
        }

        /// <summary>
        /// 订阅主题，使用不带取消令牌的处理委托
        /// </summary>
        /// <param name="topic">主题名，未注册时自动创建无界通道</param>
        /// <param name="handler">消息处理委托</param>
        /// <param name="retryCount">处理失败的最大重试次数</param>
        /// <param name="retryDelay">重试间隔，为 null 时按指数退避递增</param>
        /// <returns>订阅者标识，用于取消订阅</returns>
        public static string Subscribe(
            string topic,
            Func<EventMessage, Task> handler,
            int retryCount = 3,
            TimeSpan? retryDelay = null)
        {
            var tc = _topics.GetOrAdd(topic, _ => new TopicChannel());
            var sub = new EventSubscriber(
                (msg, ct) => handler(msg),
                retryCount,
                retryDelay);

            tc.AddSubscriber(sub);

            if (_host != null && _host.IsRunning)
            {
                _host.StartTopicDispatchLoop(tc, topic);
            }

            return sub.Id;
        }

        /// <summary>
        /// 订阅主题，处理委托可收到取消令牌
        /// </summary>
        /// <param name="topic">主题名，未注册时自动创建无界通道</param>
        /// <param name="handler">消息处理委托，参数为消息与总线取消令牌</param>
        /// <param name="retryCount">处理失败的最大重试次数</param>
        /// <param name="retryDelay">重试间隔，为 null 时按指数退避递增</param>
        /// <returns>订阅者标识，用于取消订阅</returns>
        public static string Subscribe(
            string topic,
            Func<EventMessage, CancellationToken, Task> handler,
            int retryCount = 3,
            TimeSpan? retryDelay = null)
        {
            var tc = _topics.GetOrAdd(topic, _ => new TopicChannel());
            var sub = new EventSubscriber(handler, retryCount, retryDelay);

            tc.AddSubscriber(sub);

            if (_host != null && _host.IsRunning)
            {
                _host.StartTopicDispatchLoop(tc, topic);
            }

            return sub.Id;
        }

        /// <summary>
        /// 取消订阅，主题或订阅者不存在时忽略
        /// </summary>
        /// <param name="topic">主题名</param>
        /// <param name="subscriberId">订阅时返回的订阅者标识</param>
        public static void Unsubscribe(string topic, string subscriberId)
        {
            if (_topics.TryGetValue(topic, out var tc))
            {
                tc.RemoveSubscriber(subscriberId);
            }
        }

        /// <summary>
        /// 启动总线并为已注册主题建立分发循环，已处于运行状态时直接返回
        /// </summary>
        public static void Start()
        {
            lock (_lock)
            {
                if (_host != null && _host.IsRunning) return;
                _host = new EventBusHost(new Dictionary<string, TopicChannel>(_topics));
                _host.Start();
                Enabled = true;
            }
        }

        /// <summary>
        /// 停止总线，先停止接收新消息再关闭宿主
        /// </summary>
        /// <param name="timeout">等待分发任务结束的最长时间，为 null 时不等待</param>
        /// <returns>停止完成的任务</returns>
        public static async Task StopAsync(TimeSpan? timeout = null)
        {
            lock (_lock)
            {
                Enabled = false;
            }

            if (_host != null)
            {
                await _host.StopAsync(timeout);
            }
        }

        /// <summary>
        /// 停止总线、释放宿主并清空主题注册，释放后需重新注册主题才能发布
        /// </summary>
        public static void Dispose()
        {
            Enabled = false;
            try { _host?.Dispose(); } catch { }
            _host = null;
            _topics.Clear();
        }
    }
}