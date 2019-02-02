/****************************************************************************
*项目名称：SAEA.MQTT.Core.Implementations
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Core.Implementations
*类 名 称：MqttChannelAdapter
*版 本 号： V4.1.2.2
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 15:24:37
*描述：
*=====================================================================
*修改时间：2019/1/15 15:24:37
*修 改 人： yswenli
*版 本 号： V4.1.2.2
*描    述：
*****************************************************************************/
using SAEA.MQTT.Common;
using SAEA.MQTT.Common.Log;
using SAEA.MQTT.Common.Serializer;
using SAEA.MQTT.Core.Packets;
using SAEA.MQTT.Exceptions;
using SAEA.MQTT.Interface;
using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.Core.Implementations
{
    public class MqttChannelAdapter : IMqttChannelAdapter
    {
        private const uint ErrorOperationAborted = 0x800703E3;
        private const int ReadBufferSize = 4096;  // TODO: Move buffer size to config

        private readonly SemaphoreSlim _writerSemaphore = new SemaphoreSlim(1, 1);

        private readonly IMqttNetChildLogger _logger;
        private readonly IMqttChannel _channel;

        private readonly byte[] _fixedHeaderBuffer = new byte[2];

        private readonly byte[] _singleByteBuffer = new byte[1];

        private bool _isDisposed;

        public MqttChannelAdapter(IMqttChannel channel, IMqttPacketSerializer serializer, IMqttNetChildLogger logger)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            PacketSerializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

            _logger = logger.CreateChildLogger(nameof(MqttChannelAdapter));
        }

        public string Endpoint => _channel.Endpoint;

        public IMqttPacketSerializer PacketSerializer { get; }

        public event EventHandler ReadingPacketStarted;
        public event EventHandler ReadingPacketCompleted;

        public async Task ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            try
            {
                _logger.Verbose("Connecting [Timeout={0}]", timeout);

                await Common.TaskExtensions
                    .TimeoutAfterAsync(ct => _channel.ConnectAsync(), timeout, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                if (IsWrappedException(exception))
                {
                    throw;
                }

                WrapException(exception);
            }
        }

        public async Task DisconnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            try
            {
                _logger.Verbose("Disconnecting [Timeout={0}]", timeout);

                await Common.TaskExtensions
                    .TimeoutAfterAsync(ct => _channel.DisconnectAsync(), timeout, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                if (IsWrappedException(exception))
                {
                    throw;
                }

                WrapException(exception);
            }
        }

        public async Task SendPacketAsync(MqttBasePacket packet, CancellationToken cancellationToken)
        {
            await _writerSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var packetData = PacketSerializer.Serialize(packet);
                await _channel.WriteAsync(packetData.Array, packetData.Offset, packetData.Count, cancellationToken).ConfigureAwait(false);
                PacketSerializer.FreeBuffer();

                _logger.Verbose("TX >>> {0}", packet);
            }
            catch (Exception exception)
            {
                if (IsWrappedException(exception))
                {
                    throw;
                }

                WrapException(exception);
            }
            finally
            {
                _writerSemaphore.Release();
            }
        }

        public async Task<MqttBasePacket> ReceivePacketAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            try
            {
                MqttReceivedPacket receivedMqttPacket;

                if (timeout > TimeSpan.Zero)
                {
                    receivedMqttPacket = await Common.TaskExtensions.TimeoutAfterAsync(ReceiveAsync, timeout, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    receivedMqttPacket = await ReceiveAsync(cancellationToken).ConfigureAwait(false);
                }

                if (receivedMqttPacket == null || cancellationToken.IsCancellationRequested)
                {
                    return null;
                }

                var packet = PacketSerializer.Deserialize(receivedMqttPacket);
                if (packet == null)
                {
                    throw new MqttProtocolViolationException("Received malformed packet.");
                }

                _logger.Verbose("RX <<< {0}", packet);

                return packet;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                if (IsWrappedException(exception))
                {
                    throw;
                }

                WrapException(exception);
            }

            return null;
        }

        private async Task<MqttReceivedPacket> ReceiveAsync(CancellationToken cancellationToken)
        {
            var fixedHeader = await MqttPacketReader.ReadFixedHeaderAsync(_channel, _fixedHeaderBuffer, _singleByteBuffer, cancellationToken).ConfigureAwait(false);

            try
            {
                ReadingPacketStarted?.Invoke(this, EventArgs.Empty);

                if (fixedHeader.RemainingLength == 0)
                {
                    return new MqttReceivedPacket(fixedHeader.Flags, null);
                }

                var body = new byte[fixedHeader.RemainingLength];
                var bodyOffset = 0;
                var chunkSize = Math.Min(ReadBufferSize, fixedHeader.RemainingLength);

                do
                {
                    var bytesLeft = body.Length - bodyOffset;
                    if (chunkSize > bytesLeft)
                    {
                        chunkSize = bytesLeft;
                    }


                    // async/await is not used to avoid the overhead of context switches. We assume that the reamining data
                    // has been sent from the sender directly after the initial bytes.
                    var readBytes = _channel.ReadAsync(body, bodyOffset, chunkSize, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();


                    cancellationToken.ThrowIfCancellationRequested();
                    ExceptionHelper.ThrowIfGracefulSocketClose(readBytes);

                    bodyOffset += readBytes;
                } while (bodyOffset < body.Length);

                return new MqttReceivedPacket(fixedHeader.Flags, new MqttPacketBodyReader(body, 0, body.Length));
            }
            finally
            {
                ReadingPacketCompleted?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            _isDisposed = true;

            _channel?.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(MqttChannelAdapter));
            }
        }

        private static bool IsWrappedException(Exception exception)
        {
            return exception is TaskCanceledException ||
                   exception is OperationCanceledException ||
                   exception is MqttCommunicationTimedOutException ||
                   exception is MqttCommunicationException;
        }

        private static void WrapException(Exception exception)
        {
            if (exception is IOException && exception.InnerException is SocketException socketException)
            {
                if (socketException.SocketErrorCode == SocketError.ConnectionAborted ||
                    socketException.SocketErrorCode == SocketError.OperationAborted)
                {
                    throw new OperationCanceledException();
                }
            }

            if (exception is COMException comException)
            {
                if ((uint)comException.HResult == ErrorOperationAborted)
                {
                    throw new OperationCanceledException();
                }
            }

            throw new MqttCommunicationException(exception);
        }
    }
}
