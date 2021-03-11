using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Packets;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.LowLevelClient
{
    public interface ILowLevelMqttClient : IDisposable
    {
        Task ConnectAsync(IMqttClientOptions options, CancellationToken cancellationToken);

        Task DisconnectAsync(CancellationToken cancellationToken);

        Task SendAsync(MqttBasePacket packet, CancellationToken cancellationToken);

        Task<MqttBasePacket> ReceiveAsync(CancellationToken cancellationToken);
    }
}
