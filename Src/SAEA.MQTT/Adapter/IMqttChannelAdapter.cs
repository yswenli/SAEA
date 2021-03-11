using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Packets;

namespace SAEA.MQTT.Adapter
{
    public interface IMqttChannelAdapter : IDisposable
    {
        string Endpoint { get; }

        bool IsSecureConnection { get; }

        X509Certificate2 ClientCertificate { get; }

        MqttPacketFormatterAdapter PacketFormatterAdapter { get; }

        long BytesSent { get; }

        long BytesReceived { get; }

        bool IsReadingPacket { get; }

        Task ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken);

        Task DisconnectAsync(TimeSpan timeout, CancellationToken cancellationToken);

        Task SendPacketAsync(MqttBasePacket packet, TimeSpan timeout, CancellationToken cancellationToken);

        Task<MqttBasePacket> ReceivePacketAsync(CancellationToken cancellationToken);

        void ResetStatistics();
    }
}
