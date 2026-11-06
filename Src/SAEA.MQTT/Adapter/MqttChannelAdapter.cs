/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MQTT.Adapter
*文件名： MqttChannelAdapter
*版本号： v26.4.23.1
*唯一标识：b7a189d6-b110-4905-b863-c393bd1edc0d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttChannelAdapter适配器类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Common.Caching;
using SAEA.MQTT.Channel;
using SAEA.MQTT.Diagnostics;
using SAEA.MQTT.Exceptions;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Internal;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;
using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.Adapter
{
    /// <summary>
    /// 基于 IMqttChannel 的信道适配器，负责报文编解码、超时包装与收发计数
    /// </summary>
    public sealed class MqttChannelAdapter : Disposable, IMqttChannelAdapter
    {
        const uint ErrorOperationAborted = 0x800703E3;
        const int ReadBufferSize = 4096;

        readonly IMqttNetScopedLogger _logger;
        readonly IMqttChannel _channel;
        readonly MqttPacketReader _packetReader;

        readonly byte[] _fixedHeaderBuffer = new byte[2];

        readonly AsyncLock _syncRoot = new AsyncLock();

        long _bytesReceived;
        long _bytesSent;

        /// <summary>
        /// 创建信道适配器并绑定报文读取器
        /// </summary>
        /// <param name="channel">底层信道</param>
        /// <param name="packetFormatterAdapter">报文编解码器</param>
        /// <param name="logger">日志记录器，用于创建本适配器的作用域日志</param>
        public MqttChannelAdapter(IMqttChannel channel, MqttPacketFormatterAdapter packetFormatterAdapter, IMqttNetLogger logger)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            _channel = channel ?? throw new ArgumentNullException(nameof(channel));

            PacketFormatterAdapter = packetFormatterAdapter ?? throw new ArgumentNullException(nameof(packetFormatterAdapter));

            _packetReader = new MqttPacketReader(_channel);

            _logger = logger.CreateScopedLogger(nameof(MqttChannelAdapter));
        }

        /// <summary>
        /// 对端地址
        /// </summary>
        public string Endpoint => _channel.Endpoint;

        /// <summary>
        /// 是否为加密连接
        /// </summary>
        public bool IsSecureConnection => _channel.IsSecureConnection;

        /// <summary>
        /// 客户端证书，非双向认证时为 null
        /// </summary>
        public X509Certificate2 ClientCertificate => _channel.ClientCertificate;

        /// <summary>
        /// 报文编解码器
        /// </summary>
        public MqttPacketFormatterAdapter PacketFormatterAdapter { get; }

        /// <summary>
        /// 出站上限，等于对端通告的 Maximum Packet Size。
        /// </summary>
        internal uint MaximumPacketSize { get; set; } = 268435455;

        /// <summary>
        /// 入站上限，等于本端通告的 Maximum Packet Size。
        /// </summary>
        internal uint MaximumInboundPacketSize { get; set; } = 268435455;

        /// <summary>
        /// 累计已发送字节数
        /// </summary>
        public long BytesSent => Interlocked.Read(ref _bytesSent);

        /// <summary>
        /// 累计已接收字节数
        /// </summary>
        public long BytesReceived => Interlocked.Read(ref _bytesReceived);

        /// <summary>
        /// 是否正在读取报文
        /// </summary>
        public bool IsReadingPacket { get; private set; }

        /// <summary>
        /// 建立连接
        /// </summary>
        /// <param name="timeout">超时时间，为 TimeSpan.Zero 时不限时</param>
        /// <param name="cancellationToken">取消标记</param>
        /// <returns>连接完成的异步任务</returns>
        public async Task ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            try
            {
                if (timeout == TimeSpan.Zero)
                {
                    await _channel.ConnectAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await MqttTaskTimeout.WaitAsync(t => _channel.ConnectAsync(t), timeout, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                if (IsWrappedException(exception))
                {
                    throw;
                }

                WrapAndThrowException(exception);
            }
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        /// <param name="timeout">超时时间，为 TimeSpan.Zero 时不限时</param>
        /// <param name="cancellationToken">取消标记</param>
        /// <returns>关闭完成的异步任务</returns>
        public async Task DisconnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            try
            {
                if (timeout == TimeSpan.Zero)
                {
                    await _channel.DisconnectAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await MqttTaskTimeout.WaitAsync(
                        t => _channel.DisconnectAsync(t), timeout, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                if (IsWrappedException(exception))
                {
                    throw;
                }

                WrapAndThrowException(exception);
            }
        }

        /// <summary>
        /// 编码并发送一个 MQTT 报文，同一时刻只允许一个发送流程
        /// </summary>
        /// <param name="packet">待发送报文</param>
        /// <param name="timeout">发送超时时间，为 TimeSpan.Zero 时不限时</param>
        /// <param name="cancellationToken">取消标记</param>
        /// <returns>发送完成的异步任务</returns>
        public async Task SendPacketAsync(MqttBasePacket packet, TimeSpan timeout, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            using (await _syncRoot.WaitAsync(cancellationToken).ConfigureAwait(false))
            {
                // Check for cancellation here again because "WaitAsync" might take some time.
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var packetData = PacketFormatterAdapter.Encode(packet);

                    if ((uint)packetData.Count > MaximumPacketSize)
                    {
                        throw new MqttMaximumPacketSizeExceededException(
                            MqttPacketSizeDirection.Outbound,
                            GetPacketType(packet),
                            MaximumPacketSize,
                            packetData.Count);
                    }

                    if (timeout == TimeSpan.Zero)
                    {
                        await _channel.WriteAsync(packetData.Array, packetData.Offset, packetData.Count, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await MqttTaskTimeout.WaitAsync(
                            t => _channel.WriteAsync(packetData.Array, packetData.Offset, packetData.Count, t), timeout, cancellationToken).ConfigureAwait(false);
                    }

                    Interlocked.Add(ref _bytesSent, packetData.Count);

                    _logger.Verbose("TX ({0} bytes) >>> {1}", packetData.Count, packet);
                }
                catch (Exception exception)
                {
                    if (IsWrappedException(exception))
                    {
                        throw;
                    }

                    WrapAndThrowException(exception);
                }
                finally
                {
                    PacketFormatterAdapter.FreeBuffer();
                }
            }
        }

        /// <summary>
        /// 读取并解码一个 MQTT 报文，首次读取时自动识别协议版本
        /// </summary>
        /// <param name="cancellationToken">取消标记</param>
        /// <returns>解码后的报文，连接关闭或读取被取消时返回 null；报文非法或调用前已释放时抛出异常</returns>
        public async Task<MqttBasePacket> ReceivePacketAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            try
            {
                var receivedPacket = await ReceiveAsync(cancellationToken).ConfigureAwait(false);
                if (receivedPacket == null || cancellationToken.IsCancellationRequested)
                {
                    return null;
                }

                Interlocked.Add(ref _bytesReceived, receivedPacket.TotalLength);

                if (PacketFormatterAdapter.ProtocolVersion == MqttProtocolVersion.Unknown)
                {
                    PacketFormatterAdapter.DetectProtocolVersion(receivedPacket);
                }

                MqttBasePacket packet;
                try
                {
                    packet = PacketFormatterAdapter.Decode(receivedPacket);
                }
                catch (MqttProtocolViolationException ex)
                {
                    // 解析阶段的结构性错误统一视为 Malformed Packet（0x81）。
                    throw new MqttMalformedPacketException(ex.Message);
                }
                catch (Exception ex) when (IsMalformedPacketException(ex))
                {
                    // 只把可预期的解析失败归类为 Malformed Packet；解码器自身的内部缺陷
                    // （空引用、越界、内存不足等）应原样抛出，避免被误报成对端报文非法。
                    throw new MqttMalformedPacketException("Received malformed packet: " + ex.Message);
                }

                if (packet == null)
                {
                    throw new MqttMalformedPacketException("Received malformed packet.");
                }

                _logger.Verbose("RX ({0} bytes) <<< {1}", receivedPacket.TotalLength, packet);

                return packet;
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception exception)
            {
                if (IsWrappedException(exception))
                {
                    throw;
                }

                WrapAndThrowException(exception);
            }

            return null;
        }

        /// <summary>
        /// 将收发字节计数清零
        /// </summary>
        public void ResetStatistics()
        {
            Interlocked.Exchange(ref _bytesReceived, 0L);
            Interlocked.Exchange(ref _bytesSent, 0L);
        }

        /// <summary>
        /// 释放底层信道与发送锁
        /// </summary>
        /// <param name="disposing">是否由显式释放触发，true 时释放托管资源</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _channel.Dispose();
                _syncRoot.Dispose();
            }

            base.Dispose(disposing);
        }

        async Task<ReceivedMqttPacket> ReceiveAsync(CancellationToken cancellationToken)
        {
            ReadFixedHeaderResult readFixedHeaderResult;
            try
            {
                readFixedHeaderResult = await _packetReader.ReadFixedHeaderAsync(_fixedHeaderBuffer, cancellationToken).ConfigureAwait(false);
            }
            catch (MqttProtocolViolationException ex)
            {
                // 固定头（如 Remaining Length）非法属于 Malformed Packet。
                throw new MqttMalformedPacketException(ex.Message);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            if (readFixedHeaderResult.ConnectionClosed)
            {
                return null;
            }

            try
            {
                IsReadingPacket = true;

                var fixedHeader = readFixedHeaderResult.FixedHeader;

                if ((uint)fixedHeader.TotalLength > MaximumInboundPacketSize)
                {
                    throw new MqttMaximumPacketSizeExceededException(
                        MqttPacketSizeDirection.Inbound,
                        (MqttControlPacketType)(fixedHeader.Flags >> 4),
                        MaximumInboundPacketSize,
                        fixedHeader.TotalLength);
                }

                if (fixedHeader.RemainingLength == 0)
                {
                    return new ReceivedMqttPacket(fixedHeader.Flags, null, 2);
                }

                var bodyLength = fixedHeader.RemainingLength;
                byte[] body = null;
                bool rentedBuffer = false;

                try
                {
                    // Use MemoryPoolManager for body buffer if size is reasonable
                    if (bodyLength <= MemoryPoolManager.MediumThreshold)
                    {
                        body = MemoryPoolManager.Rent(bodyLength);
                        rentedBuffer = true;
                    }
                    else
                    {
                        body = new byte[bodyLength];
                    }

                    var bodyOffset = 0;
                    var chunkSize = Math.Min(ReadBufferSize, bodyLength);

                    do
                    {
                        var bytesLeft = body.Length - bodyOffset;
                        if (chunkSize > bytesLeft)
                        {
                            chunkSize = bytesLeft;
                        }

                        var readBytes = await _channel.ReadAsync(body, bodyOffset, chunkSize, cancellationToken).ConfigureAwait(false);

                        if (cancellationToken.IsCancellationRequested)
                        {
                            return null;
                        }

                        if (readBytes == 0)
                        {
                            return null;
                        }

                        bodyOffset += readBytes;
                    } while (bodyOffset < bodyLength);

                    // Create a copy for the body reader since it needs to own the data
                    var bodyCopy = new byte[bodyLength];
                    Buffer.BlockCopy(body, 0, bodyCopy, 0, bodyLength);
                    var bodyReader = new MqttPacketBodyReader(bodyCopy, 0, bodyLength);
                    return new ReceivedMqttPacket(fixedHeader.Flags, bodyReader, fixedHeader.TotalLength);
                }
                finally
                {
                    if (rentedBuffer && body != null)
                    {
                        MemoryPoolManager.Return(body, bodyLength);
                    }
                }
            }
            finally
            {
                IsReadingPacket = false;
            }
        }

        static bool IsWrappedException(Exception exception)
        {
            return exception is OperationCanceledException ||
                   exception is MqttCommunicationTimedOutException ||
                   exception is MqttCommunicationException ||
                   exception is MqttMaximumPacketSizeExceededException ||
                   exception is MqttMalformedPacketException;
        }

        /// <summary>
        /// 判断异常是否属于“字节流本身不合法”这一可预期类别。
        /// 解码器的内部缺陷不在其列，交由上层按普通异常处理。
        /// </summary>
        static bool IsMalformedPacketException(Exception exception)
        {
            return exception is EndOfStreamException ||
                   exception is ArgumentException ||
                   exception is FormatException ||
                   exception is NotSupportedException;
        }

        static MqttControlPacketType? GetPacketType(MqttBasePacket packet)
        {
            if (packet is MqttConnectPacket) return MqttControlPacketType.Connect;
            if (packet is MqttConnAckPacket) return MqttControlPacketType.ConnAck;
            if (packet is MqttPublishPacket) return MqttControlPacketType.Publish;
            if (packet is MqttPubAckPacket) return MqttControlPacketType.PubAck;
            if (packet is MqttPubRecPacket) return MqttControlPacketType.PubRec;
            if (packet is MqttPubRelPacket) return MqttControlPacketType.PubRel;
            if (packet is MqttPubCompPacket) return MqttControlPacketType.PubComp;
            if (packet is MqttSubscribePacket) return MqttControlPacketType.Subscribe;
            if (packet is MqttSubAckPacket) return MqttControlPacketType.SubAck;
            if (packet is MqttUnsubscribePacket) return MqttControlPacketType.Unsubscribe;
            if (packet is MqttUnsubAckPacket) return MqttControlPacketType.UnsubAck;
            if (packet is MqttPingReqPacket) return MqttControlPacketType.PingReq;
            if (packet is MqttPingRespPacket) return MqttControlPacketType.PingResp;
            if (packet is MqttDisconnectPacket) return MqttControlPacketType.Disconnect;
            if (packet is MqttAuthPacket) return MqttControlPacketType.Auth;
            return null;
        }

        static void WrapAndThrowException(Exception exception)
        {
            if (exception is IOException && exception.InnerException is SocketException innerException)
            {
                exception = innerException;
            }

            if (exception is SocketException socketException)
            {
                if (socketException.SocketErrorCode == SocketError.OperationAborted)
                {
                    throw new OperationCanceledException();
                }
                
                if (socketException.SocketErrorCode == SocketError.ConnectionAborted)
                {
                    throw new MqttCommunicationException(socketException);
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
