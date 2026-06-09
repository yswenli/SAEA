using System;

namespace SAEA.Common.EventBus
{
    public class EventMessage
    {
        public string Topic { get; }
        public byte[] Data { get; }
        public object Context { get; }
        public DateTime Timestamp { get; }

        public EventMessage(string topic, byte[] data, object context = null)
        {
            Topic = topic ?? throw new ArgumentNullException(nameof(topic));
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Context = context;
            Timestamp = DateTime.UtcNow;
        }
    }
}