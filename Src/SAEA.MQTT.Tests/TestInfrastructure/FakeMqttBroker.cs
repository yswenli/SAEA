using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SAEA.MQTT.Adapter;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Tests.TestInfrastructure
{
    /// <summary>
    /// 最小 MQTT 5.0 对端，用于验证客户端侧行为（发送配额、DISCONNECT 属性等）。
    /// 只实现测试所需的最小子集：CONNECT/CONNACK、PUBLISH/PUBACK、PUBREL/PUBCOMP、DISCONNECT。
    /// </summary>
    internal sealed class FakeMqttBroker : IAsyncDisposable
    {
        readonly TcpListener _listener;
        readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        readonly MqttPacketFormatterAdapter _formatter =
            new MqttPacketFormatterAdapter(MqttProtocolVersion.V500);
        readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        readonly ConcurrentQueue<ushort> _pendingPubAckIds = new ConcurrentQueue<ushort>();
        readonly TaskCompletionSource<MqttDisconnectPacket> _disconnectReceived =
            new TaskCompletionSource<MqttDisconnectPacket>(TaskCreationOptions.RunContinuationsAsynchronously);

        NetworkStream _stream;
        TcpClient _tcpClient;
        Task _acceptTask;
        int _receivedPublishCount;

        FakeMqttBroker(TcpListener listener, ushort receiveMaximum, bool autoAckQoS1)
        {
            _listener = listener;
            ReceiveMaximum = receiveMaximum;
            AutoAckQoS1 = autoAckQoS1;
        }

        public ushort ReceiveMaximum { get; }

        public bool AutoAckQoS1 { get; }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public int ReceivedPublishCount => Interlocked.CompareExchange(ref _receivedPublishCount, 0, 0);

        public Task<MqttDisconnectPacket> DisconnectReceived => _disconnectReceived.Task;

        public static Task<FakeMqttBroker> StartAsync(ushort receiveMaximum = ushort.MaxValue, bool autoAckQoS1 = true)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            var broker = new FakeMqttBroker(listener, receiveMaximum, autoAckQoS1);
            broker._acceptTask = Task.Run(() => broker.RunAsync(broker._cancellationTokenSource.Token));

            return Task.FromResult(broker);
        }

        public async Task<ushort> WaitForPendingPubAckAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                if (_pendingPubAckIds.TryPeek(out var packetIdentifier))
                {
                    return packetIdentifier;
                }

                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException("The fake broker did not receive a QoS 1 PUBLISH in time.");
        }

        public async Task<ushort> DequeuePendingPubAckAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                if (_pendingPubAckIds.TryDequeue(out var packetIdentifier))
                {
                    return packetIdentifier;
                }

                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException("The fake broker did not receive a QoS 1 PUBLISH in time.");
        }

        public Task SendPubAckAsync(ushort packetIdentifier)
        {
            return SendPacketAsync(new MqttPubAckPacket
            {
                PacketIdentifier = packetIdentifier,
                ReasonCode = MqttPubAckReasonCode.Success
            });
        }

        public async Task WaitForPublishCountAsync(int expected, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                if (ReceivedPublishCount >= expected)
                {
                    return;
                }

                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException(
                $"The fake broker expected {expected} PUBLISH packets but received {ReceivedPublishCount}.");
        }

        /// <summary>
        /// 模拟对端突然断开（不发送 DISCONNECT）。
        /// </summary>
        public void DropConnection()
        {
            try
            {
                _stream?.Dispose();
            }
            catch
            {
            }

            try
            {
                _tcpClient?.Dispose();
            }
            catch
            {
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cancellationTokenSource.Cancel();

            DropConnection();

            try
            {
                await _acceptTask.ConfigureAwait(false);
            }
            catch
            {
            }

            try
            {
                _listener.Stop();
            }
            catch
            {
            }

            _cancellationTokenSource.Dispose();
            _sendLock.Dispose();
        }

        async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                _tcpClient = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                _stream = _tcpClient.GetStream();

                while (!cancellationToken.IsCancellationRequested)
                {
                    var packet = await ReadPacketAsync(cancellationToken).ConfigureAwait(false);
                    if (packet == null)
                    {
                        return;
                    }

                    await HandlePacketAsync(packet).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (IOException)
            {
            }
            catch (SocketException)
            {
            }
        }

        async Task HandlePacketAsync(MqttBasePacket packet)
        {
            if (packet is MqttConnectPacket)
            {
                await SendPacketAsync(new MqttConnAckPacket
                {
                    ReasonCode = MqttConnectReasonCode.Success,
                    IsSessionPresent = false,
                    Properties = new MqttConnAckPacketProperties
                    {
                        ReceiveMaximum = ReceiveMaximum,
                        MaximumPacketSize = 268435455,
                        TopicAliasMaximum = 0
                    }
                }).ConfigureAwait(false);

                return;
            }

            if (packet is MqttPublishPacket publishPacket)
            {
                Interlocked.Increment(ref _receivedPublishCount);

                if (publishPacket.QualityOfServiceLevel == MqttQualityOfServiceLevel.AtLeastOnce)
                {
                    if (AutoAckQoS1)
                    {
                        await SendPacketAsync(new MqttPubAckPacket
                        {
                            PacketIdentifier = publishPacket.PacketIdentifier,
                            ReasonCode = MqttPubAckReasonCode.Success
                        }).ConfigureAwait(false);
                    }
                    else
                    {
                        _pendingPubAckIds.Enqueue(publishPacket.PacketIdentifier);
                    }
                }
                else if (publishPacket.QualityOfServiceLevel == MqttQualityOfServiceLevel.ExactlyOnce)
                {
                    await SendPacketAsync(new MqttPubRecPacket
                    {
                        PacketIdentifier = publishPacket.PacketIdentifier,
                        ReasonCode = MqttPubRecReasonCode.Success
                    }).ConfigureAwait(false);
                }

                return;
            }

            if (packet is MqttPubRelPacket pubRelPacket)
            {
                await SendPacketAsync(new MqttPubCompPacket
                {
                    PacketIdentifier = pubRelPacket.PacketIdentifier,
                    ReasonCode = MqttPubCompReasonCode.Success
                }).ConfigureAwait(false);

                return;
            }

            if (packet is MqttPingReqPacket)
            {
                await SendPacketAsync(MqttPingRespPacket.Instance).ConfigureAwait(false);
                return;
            }

            if (packet is MqttDisconnectPacket disconnectPacket)
            {
                _disconnectReceived.TrySetResult(disconnectPacket);
            }
        }

        async Task SendPacketAsync(MqttBasePacket packet)
        {
            await _sendLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_stream == null)
                {
                    return;
                }

                var buffer = _formatter.Encode(packet);
                await _stream.WriteAsync(buffer.Array, buffer.Offset, buffer.Count).ConfigureAwait(false);
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                _sendLock.Release();
            }
        }

        async Task<MqttBasePacket> ReadPacketAsync(CancellationToken cancellationToken)
        {
            var header = new byte[1];
            var read = await _stream.ReadAsync(header, 0, 1, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            var remainingLength = 0;
            var multiplier = 1;

            while (true)
            {
                var digit = new byte[1];
                read = await _stream.ReadAsync(digit, 0, 1, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return null;
                }

                remainingLength += (digit[0] & 0x7F) * multiplier;
                if ((digit[0] & 0x80) == 0)
                {
                    break;
                }

                multiplier *= 128;
            }

            var body = new byte[remainingLength];
            var offset = 0;
            while (offset < remainingLength)
            {
                var chunk = await _stream
                    .ReadAsync(body, offset, remainingLength - offset, cancellationToken)
                    .ConfigureAwait(false);

                if (chunk == 0)
                {
                    return null;
                }

                offset += chunk;
            }

            var bodyReader = new MqttPacketBodyReader(body, 0, body.Length);
            var receivedPacket = new ReceivedMqttPacket(header[0], bodyReader, remainingLength + 1);
            return _formatter.Decode(receivedPacket);
        }
    }
}