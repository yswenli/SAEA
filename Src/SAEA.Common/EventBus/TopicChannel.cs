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
*文件名： TopicChannel
*版本号： v26.11.5.1
*唯一标识：9e30e509-dfc0-4f24-8ab8-4f5098194483
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 12:40:36
*描述：按主题划分的事件通道
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
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SAEA.Common.EventBus
{
    /// <summary>
    /// 主题通道，内部维护有界或无界消息队列及订阅者列表
    /// </summary>
    public class TopicChannel
    {
        private readonly Channel<EventMessage> _channel;
        private readonly List<EventSubscriber> _subscribers;
        private readonly object _subscriberLock = new object();

        /// <summary>
        /// 创建主题通道
        /// </summary>
        /// <param name="capacity">队列容量，大于 0 时有界且写满时等待，否则无界不限制容量</param>
        public TopicChannel(int capacity = 0)
        {
            _subscribers = new List<EventSubscriber>();
            
            if (capacity > 0)
            {
                _channel = Channel.CreateBounded<EventMessage>(new BoundedChannelOptions(capacity)
                {
                    FullMode = BoundedChannelFullMode.Wait
                });
            }
            else
            {
                _channel = Channel.CreateUnbounded<EventMessage>();
            }
        }

        /// <summary>
        /// 发布消息到通道，有界队列写满时异步等待
        /// </summary>
        /// <param name="message">待发布的消息</param>
        /// <returns>写入完成的任务</returns>
        public async ValueTask PublishAsync(EventMessage message)
        {
            await _channel.Writer.WriteAsync(message);
        }

        /// <summary>
        /// 从通道读取一条消息，无消息时异步等待
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>读取到的消息</returns>
        public async ValueTask<EventMessage> ReadAsync(CancellationToken cancellationToken)
        {
            return await _channel.Reader.ReadAsync(cancellationToken);
        }

        /// <summary>
        /// 添加订阅者，线程安全
        /// </summary>
        /// <param name="subscriber">订阅者</param>
        public void AddSubscriber(EventSubscriber subscriber)
        {
            lock (_subscriberLock)
            {
                _subscribers.Add(subscriber);
            }
        }

        /// <summary>
        /// 按标识移除订阅者，不存在时忽略，线程安全
        /// </summary>
        /// <param name="subscriberId">订阅者标识</param>
        public void RemoveSubscriber(string subscriberId)
        {
            lock (_subscriberLock)
            {
                _subscribers.RemoveAll(s => s.Id == subscriberId);
            }
        }

        /// <summary>
        /// 获取订阅者快照，返回的副本不受后续增删影响
        /// </summary>
        /// <returns>当前订阅者副本</returns>
        public IReadOnlyList<EventSubscriber> GetSubscribers()
        {
            lock (_subscriberLock)
            {
                return _subscribers.ToArray();
            }
        }

        /// <summary>
        /// 关闭通道写入端，之后不再允许发布消息
        /// </summary>
        public void Complete()
        {
            _channel.Writer.Complete();
        }
    }
}