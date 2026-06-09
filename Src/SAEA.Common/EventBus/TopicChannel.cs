using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SAEA.Common.EventBus
{
    public class TopicChannel
    {
        private readonly Channel<EventMessage> _channel;
        private readonly List<EventSubscriber> _subscribers;
        private readonly object _subscriberLock = new object();

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

        public async ValueTask PublishAsync(EventMessage message)
        {
            await _channel.Writer.WriteAsync(message);
        }

        public async ValueTask<EventMessage> ReadAsync(CancellationToken cancellationToken)
        {
            return await _channel.Reader.ReadAsync(cancellationToken);
        }

        public void AddSubscriber(EventSubscriber subscriber)
        {
            lock (_subscriberLock)
            {
                _subscribers.Add(subscriber);
            }
        }

        public void RemoveSubscriber(string subscriberId)
        {
            lock (_subscriberLock)
            {
                _subscribers.RemoveAll(s => s.Id == subscriberId);
            }
        }

        public IReadOnlyList<EventSubscriber> GetSubscribers()
        {
            lock (_subscriberLock)
            {
                return _subscribers.ToArray();
            }
        }

        public void Complete()
        {
            _channel.Writer.Complete();
        }
    }
}