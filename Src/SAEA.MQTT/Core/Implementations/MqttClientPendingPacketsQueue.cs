/****************************************************************************
*项目名称：SAEA.MQTT.Core.Implementations
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Core.Implementations
*类 名 称：MqttClientPendingPacketsQueue
*版 本 号： v4.5.1.2
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 15:49:53
*描述：
*=====================================================================
*修改时间：2019/1/15 15:49:53
*修 改 人： yswenli
*版 本 号： v4.5.1.2
*描    述：
*****************************************************************************/
using SAEA.MQTT.Common;
using SAEA.MQTT.Common.Log;
using SAEA.MQTT.Core.Packets;
using SAEA.MQTT.Core.Protocol;
using SAEA.MQTT.Exceptions;
using SAEA.MQTT.Interface;
using SAEA.MQTT.Model;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.Core.Implementations
{
    public class MqttClientPendingPacketsQueue : IDisposable
    {
        private readonly Queue<MqttBasePacket> _queue = new Queue<MqttBasePacket>();
        private readonly AsyncAutoResetEvent _queueAutoResetEvent = new AsyncAutoResetEvent();

        private readonly IMqttServerOptions _options;
        private readonly MqttClientSession _clientSession;
        private readonly IMqttNetChildLogger _logger;

        public MqttClientPendingPacketsQueue(IMqttServerOptions options, MqttClientSession clientSession, IMqttNetChildLogger logger)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _clientSession = clientSession ?? throw new ArgumentNullException(nameof(clientSession));

            _logger = logger.CreateChildLogger(nameof(MqttClientPendingPacketsQueue));
        }

        public int Count
        {
            get
            {
                lock (_queue)
                {
                    return _queue.Count;
                }
            }
        }

        public void Start(IMqttChannelAdapter adapter, CancellationToken cancellationToken)
        {
            if (adapter == null) throw new ArgumentNullException(nameof(adapter));

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            Task.Run(() => SendQueuedPacketsAsync(adapter, cancellationToken), cancellationToken);
        }

        public void Enqueue(MqttBasePacket packet)
        {
            if (packet == null) throw new ArgumentNullException(nameof(packet));

            lock (_queue)
            {
                if (_queue.Count >= _options.MaxPendingMessagesPerClient)
                {
                    if (_options.PendingMessagesOverflowStrategy == MqttPendingMessagesOverflowStrategy.DropNewMessage)
                    {
                        return;
                    }

                    if (_options.PendingMessagesOverflowStrategy == MqttPendingMessagesOverflowStrategy.DropOldestQueuedMessage)
                    {
                        _queue.Dequeue();
                    }
                }

                _queue.Enqueue(packet);
            }

            _queueAutoResetEvent.Set();

            _logger.Verbose("Enqueued packet (ClientId: {0}).", _clientSession.ClientId);
        }

        public void Clear()
        {
            lock (_queue)
            {
                _queue.Clear();
            }
        }

        public void Dispose()
        {
        }

        private async Task SendQueuedPacketsAsync(IMqttChannelAdapter adapter, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await TrySendNextQueuedPacketAsync(adapter, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Unhandled exception while sending enqueued packet (ClientId: {0}).", _clientSession.ClientId);
            }
        }

        private async Task TrySendNextQueuedPacketAsync(IMqttChannelAdapter adapter, CancellationToken cancellationToken)
        {
            MqttBasePacket packet = null;
            try
            {
                lock (_queue)
                {
                    if (_queue.Count > 0)
                    {
                        packet = _queue.Dequeue();
                    }
                }

                if (packet == null)
                {
                    await _queueAutoResetEvent.WaitOneAsync(cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                adapter.SendPacketAsync(packet, cancellationToken).GetAwaiter().GetResult();

                _logger.Verbose("Enqueued packet sent (ClientId: {0}).", _clientSession.ClientId);
            }
            catch (Exception exception)
            {
                if (exception is MqttCommunicationTimedOutException)
                {
                    _logger.Warning(exception, "Sending publish packet failed: Timeout (ClientId: {0}).", _clientSession.ClientId);
                }
                else if (exception is MqttCommunicationException)
                {
                    _logger.Warning(exception, "Sending publish packet failed: Communication exception (ClientId: {0}).", _clientSession.ClientId);
                }
                else if (exception is OperationCanceledException)
                {
                }
                else
                {
                    _logger.Error(exception, "Sending publish packet failed (ClientId: {0}).", _clientSession.ClientId);
                }

                if (packet is MqttPublishPacket publishPacket)
                {
                    if (publishPacket.QualityOfServiceLevel > MqttQualityOfServiceLevel.AtMostOnce)
                    {
                        publishPacket.Dup = true;

                        Enqueue(publishPacket);
                    }
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    _clientSession.Stop(MqttClientDisconnectType.NotClean);
                }
            }
        }
    }
}
