using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SAEA.MQTT.Adapter;
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Tests.TestInfrastructure
{
    /// <summary>
    /// 最小 MQTT 5.0 原始 TCP 客户端，用于在报文层验证 Topic Alias 行为。
    /// </summary>
    internal sealed class RawMqttTestClient : IAsyncDisposable
    {
        readonly TcpClient _tcpClient;
        readonly NetworkStream _stream;
        readonly MqttPacketFormatterAdapter _formatter = new MqttPacketFormatterAdapter(MqttProtocolVersion.V500);

        private RawMqttTestClient(TcpClient tcpClient)
        {
            _tcpClient = tcpClient;
            _stream = tcpClient.GetStream();
        }

        public static async Task<RawMqttTestClient> ConnectAsync(
            int port,
            string clientId,
            ushort? topicAliasMaximum,
            CancellationToken cancellationToken,
            ushort? receiveMaximum = null,
            uint? maximumPacketSize = null)
        {
            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync("127.0.0.1", port).ConfigureAwait(false);

            var client = new RawMqttTestClient(tcpClient);

            var options = new MqttClientOptions
            {
                ClientId = clientId,
                CleanSession = true,
                KeepAlivePeriod = TimeSpan.FromSeconds(30),
                ProtocolVersion = MqttProtocolVersion.V500,
                TopicAliasMaximum = topicAliasMaximum,
                ReceiveMaximum = receiveMaximum,
                MaximumPacketSize = maximumPacketSize
            };

            var connectPacket = client._formatter.DataConverter.CreateConnectPacket(null, options);
            await client.SendPacketAsync(connectPacket, cancellationToken).ConfigureAwait(false);

            var connAck = await client.ReadPacketAsync(cancellationToken).ConfigureAwait(false) as MqttConnAckPacket;
            if (connAck == null || connAck.ReasonCode != MqttConnectReasonCode.Success)
            {
                throw new InvalidOperationException("The raw test client could not establish a connection.");
            }

            return client;
        }

        public async Task SubscribeAsync(string topic, CancellationToken cancellationToken)
        {
            var packet = new MqttSubscribePacket
            {
                PacketIdentifier = 1,
                Properties = new MqttSubscribePacketProperties()
            };

            packet.TopicFilters.Add(new MqttTopicFilter
            {
                Topic = topic,
                QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce
            });

            await SendPacketAsync(packet, cancellationToken).ConfigureAwait(false);

            var subAck = await ReadPacketAsync(cancellationToken).ConfigureAwait(false) as MqttSubAckPacket;
            if (subAck == null)
            {
                throw new InvalidOperationException("The raw test client did not receive a SUBACK packet.");
            }
        }

        public Task SendPublishAsync(string topic, string payload, ushort? topicAlias, CancellationToken cancellationToken)
        {
            var packet = BuildPublishPacket(topic, Encoding.UTF8.GetBytes(payload ?? string.Empty), topicAlias);
            return SendRawAsync(packet, cancellationToken);
        }

        public Task SendPublishAsync(
            string topic,
            byte[] payload,
            MqttQualityOfServiceLevel qualityOfServiceLevel,
            bool retain,
            ushort? packetIdentifier,
            CancellationToken cancellationToken)
        {
            var fixedHeader = (byte)(0x30 | (retain ? 0x01 : 0x00) | ((int)qualityOfServiceLevel << 1));
            var packet = BuildPublishPacketCore(fixedHeader, topic, payload, packetIdentifier, null);
            return SendRawAsync(packet, cancellationToken);
        }

        public async Task<MqttSubAckPacket> SubscribeAsync(
            string topic,
            MqttQualityOfServiceLevel qualityOfServiceLevel,
            uint? subscriptionIdentifier,
            CancellationToken cancellationToken)
        {
            var packet = new MqttSubscribePacket
            {
                PacketIdentifier = 1,
                Properties = new MqttSubscribePacketProperties
                {
                    SubscriptionIdentifier = subscriptionIdentifier
                }
            };

            packet.TopicFilters.Add(new MqttTopicFilter
            {
                Topic = topic,
                QualityOfServiceLevel = qualityOfServiceLevel
            });

            await SendPacketAsync(packet, cancellationToken).ConfigureAwait(false);

            return await ReadPacketAsync(cancellationToken).ConfigureAwait(false) as MqttSubAckPacket;
        }

        public Task SendQoS1PublishAsync(string topic, string payload, ushort packetId, CancellationToken cancellationToken)
        {
            var packet = BuildPublishPacketCore(
                0x32,
                topic,
                Encoding.UTF8.GetBytes(payload ?? string.Empty),
                packetId,
                null);
            return SendRawAsync(packet, cancellationToken);
        }

        public Task SendQoS2PublishAsync(string topic, string payload, ushort packetId, CancellationToken cancellationToken)
        {
            var packet = BuildPublishPacketCore(
                0x34,
                topic,
                Encoding.UTF8.GetBytes(payload ?? string.Empty),
                packetId,
                null);
            return SendRawAsync(packet, cancellationToken);
        }

        public Task SendRawBytesAsync(byte[] data, CancellationToken cancellationToken)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            return SendRawAsync(data, cancellationToken);
        }

        public Task SendPacketAsync(MqttBasePacket packet, CancellationToken cancellationToken)
        {
            return SendRawAsync(_formatter.Encode(packet), cancellationToken);
        }

        public async Task<MqttBasePacket> ReadPacketAsync(CancellationToken cancellationToken)
        {
            var header = new byte[1];
            await ReadExactAsync(header, 0, 1, cancellationToken).ConfigureAwait(false);

            var remainingLength = 0;
            var multiplier = 1;

            while (true)
            {
                var digit = new byte[1];
                await ReadExactAsync(digit, 0, 1, cancellationToken).ConfigureAwait(false);

                remainingLength += (digit[0] & 0x7F) * multiplier;
                if ((digit[0] & 0x80) == 0)
                {
                    break;
                }

                multiplier *= 128;
            }

            var body = new byte[remainingLength];
            if (remainingLength > 0)
            {
                await ReadExactAsync(body, 0, remainingLength, cancellationToken).ConfigureAwait(false);
            }

            var bodyReader = new MqttPacketBodyReader(body, 0, body.Length);
            var receivedPacket = new ReceivedMqttPacket(header[0], bodyReader, body.Length + 1);
            return _formatter.Decode(receivedPacket);
        }

        public async Task<MqttDisconnectPacket> WaitForDisconnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            using (var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeoutSource.CancelAfter(timeout);

                while (true)
                {
                    var packet = await ReadPacketAsync(timeoutSource.Token).ConfigureAwait(false);
                    var disconnectPacket = packet as MqttDisconnectPacket;
                    if (disconnectPacket != null)
                    {
                        return disconnectPacket;
                    }
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                _stream.Dispose();
            }
            catch
            {
                // Best effort during test cleanup.
            }

            _tcpClient.Dispose();

            await Task.CompletedTask.ConfigureAwait(false);
        }

        public static byte[] BuildPublishPacket(string topic, byte[] payload, ushort? topicAlias)
        {
            return BuildPublishPacketCore(0x30, topic, payload, null, topicAlias);
        }

        static byte[] BuildPublishPacketCore(byte fixedHeader, string topic, byte[] payload, ushort? packetIdentifier, ushort? topicAlias)
        {
            var body = new List<byte>();
            WriteString(body, topic);

            if (packetIdentifier.HasValue)
            {
                body.Add((byte)(packetIdentifier.Value >> 8));
                body.Add((byte)(packetIdentifier.Value & 0xFF));
            }

            var properties = new List<byte>();
            if (topicAlias.HasValue)
            {
                properties.Add((byte)MqttPropertyId.TopicAlias);
                properties.Add((byte)(topicAlias.Value >> 8));
                properties.Add((byte)(topicAlias.Value & 0xFF));
            }

            WriteVariableLengthInteger(body, properties.Count);
            body.AddRange(properties);

            if (payload != null && payload.Length > 0)
            {
                body.AddRange(payload);
            }

            var packet = new List<byte> { fixedHeader };
            WriteVariableLengthInteger(packet, body.Count);
            packet.AddRange(body);
            return packet.ToArray();
        }

        async Task SendRawAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            await _stream
                .WriteAsync(buffer.Array, buffer.Offset, buffer.Count, cancellationToken)
                .ConfigureAwait(false);
        }

        async Task SendRawAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            await _stream
                .WriteAsync(buffer, 0, buffer.Length, cancellationToken)
                .ConfigureAwait(false);
        }

        async Task ReadExactAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var read = 0;
            while (read < count)
            {
                var chunk = await _stream
                    .ReadAsync(buffer, offset + read, count - read, cancellationToken)
                    .ConfigureAwait(false);

                if (chunk == 0)
                {
                    throw new IOException("The connection was closed before a full packet was received.");
                }

                read += chunk;
            }
        }

        static void WriteString(List<byte> buffer, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                buffer.Add(0);
                buffer.Add(0);
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(value);
            buffer.Add((byte)(bytes.Length >> 8));
            buffer.Add((byte)(bytes.Length & 0xFF));
            buffer.AddRange(bytes);
        }

        static void WriteVariableLengthInteger(List<byte> buffer, int value)
        {
            do
            {
                var encoded = (byte)(value % 128);
                value /= 128;

                if (value > 0)
                {
                    encoded |= 0x80;
                }

                buffer.Add(encoded);
            }
            while (value > 0);
        }
    }
}