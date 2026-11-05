using System;
using SAEA.MQTT.Internal;

namespace SAEA.MQTT.Tests.TestInfrastructure
{
    /// <summary>
    /// 在作用域内替换 <see cref="MqttTime.Provider"/>，释放时恢复进入前的值。
    /// </summary>
    internal sealed class TestClockScope : IDisposable
    {
        readonly IMqttTimeProvider _previousProvider;
        bool _disposed;

        public TestClockScope(IMqttTimeProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            _previousProvider = MqttTime.Provider;
            MqttTime.Provider = provider;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            MqttTime.Provider = _previousProvider;
        }
    }
}