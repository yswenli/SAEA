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
*命名空间：SAEA.MQTT.Formatter
*文件名： MqttPacketReader
*版本号： v26.4.23.1
*唯一标识：62918e05-dad0-4b0d-bdc1-e23d9dc30d40
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MQTT数据格式化类
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MQTT数据格式化类
*
*****************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;
using SAEA.MQTT.Channel;
using SAEA.MQTT.Exceptions;

namespace SAEA.MQTT.Formatter
{
    public sealed class MqttPacketReader
    {
        readonly byte[] _singleByteBuffer = new byte[1];

        readonly IMqttChannel _channel;

        public MqttPacketReader(IMqttChannel channel)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        }

        public async Task<ReadFixedHeaderResult> ReadFixedHeaderAsync(byte[] fixedHeaderBuffer, CancellationToken cancellationToken)
        {
            // The MQTT fixed header contains 1 byte of flags and at least 1 byte for the remaining data length.
            // So in all cases at least 2 bytes must be read for a complete MQTT packet.
            var buffer = fixedHeaderBuffer;
            var totalBytesRead = 0;

            while (totalBytesRead < buffer.Length)
            {
                var bytesRead = await _channel.ReadAsync(buffer, totalBytesRead, buffer.Length - totalBytesRead, cancellationToken).ConfigureAwait(false);

                if (cancellationToken.IsCancellationRequested)
                {
                    return null;
                }
                
                if (bytesRead == 0)
                {
                    return new ReadFixedHeaderResult
                    {
                        ConnectionClosed = true
                    };
                }
                
                totalBytesRead += bytesRead;
            }

            var hasRemainingLength = buffer[1] != 0;
            if (!hasRemainingLength)
            {
                return new ReadFixedHeaderResult
                {
                    FixedHeader = new MqttFixedHeader(buffer[0], 0, totalBytesRead)
                };
            }

            var bodyLength = await ReadBodyLengthAsync(buffer[1], cancellationToken).ConfigureAwait(false);

            if (!bodyLength.HasValue)
            {
                return new ReadFixedHeaderResult
                {
                    ConnectionClosed = true
                };
            }

            totalBytesRead += bodyLength.Value;
            return new ReadFixedHeaderResult
            {
                FixedHeader = new MqttFixedHeader(buffer[0], bodyLength.Value, totalBytesRead)
            };
        }

        async Task<int?> ReadBodyLengthAsync(byte initialEncodedByte, CancellationToken cancellationToken)
        {
            var offset = 0;
            var multiplier = 128;
            var value = initialEncodedByte & 127;
            int encodedByte = initialEncodedByte;

            while ((encodedByte & 128) != 0)
            {
                offset++;
                if (offset > 3)
                {
                    throw new MqttProtocolViolationException("Remaining length is invalid.");
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return null;
                }

                var readCount = await _channel.ReadAsync(_singleByteBuffer, 0, 1, cancellationToken).ConfigureAwait(false);

                if (cancellationToken.IsCancellationRequested)
                {
                    return null;
                }

                if (readCount == 0)
                {
                    return null;
                }

                encodedByte = _singleByteBuffer[0];

                value += (encodedByte & 127) * multiplier;
                multiplier *= 128;
            }

            return value;
        }
    }
}
