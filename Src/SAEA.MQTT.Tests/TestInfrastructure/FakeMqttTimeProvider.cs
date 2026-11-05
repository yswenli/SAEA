using System;
using SAEA.MQTT.Internal;

namespace SAEA.MQTT.Tests.TestInfrastructure
{
    internal sealed class FakeMqttTimeProvider : IMqttTimeProvider
    {
        DateTimeOffset _utcNow;

        public FakeMqttTimeProvider(DateTimeOffset start)
        {
            _utcNow = start;
        }

        public DateTimeOffset UtcNow
        {
            get { return _utcNow; }
        }

        public void Advance(TimeSpan delta)
        {
            _utcNow = _utcNow.Add(delta);
        }

        public void Set(DateTimeOffset value)
        {
            _utcNow = value;
        }
    }
}