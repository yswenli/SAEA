using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Common.EventBus
{
    public class EventSubscriber
    {
        public string Id { get; }
        public Func<EventMessage, CancellationToken, Task> Handler { get; }
        public int RetryCount { get; }
        public TimeSpan? RetryDelay { get; }

        public EventSubscriber(
            Func<EventMessage, CancellationToken, Task> handler,
            int retryCount = 3,
            TimeSpan? retryDelay = null)
        {
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
            RetryCount = retryCount;
            RetryDelay = retryDelay;
            Id = Guid.NewGuid().ToString("N");
        }
    }
}