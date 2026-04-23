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
*命名空间：SAEA.MQTT.LowLevelClient
*文件名： LowLevelMqttClient
*版本号： v26.4.23.1
*唯一标识：e10cc3d6-454c-419d-83aa-bdf948ff87a0
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：LowLevelMqttClient接口
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：LowLevelMqttClient接口
*
*****************************************************************************/
using SAEA.MQTT.Adapter;
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Diagnostics;
using SAEA.MQTT.Packets;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.LowLevelClient
{
    public sealed class LowLevelMqttClient : ILowLevelMqttClient
    {
        readonly IMqttNetScopedLogger _logger;
        readonly IMqttClientAdapterFactory _clientAdapterFactory;

        IMqttChannelAdapter _adapter;
        IMqttClientOptions _options;

        public LowLevelMqttClient(IMqttClientAdapterFactory clientAdapterFactory, IMqttNetLogger logger)
        {
            _clientAdapterFactory = clientAdapterFactory ?? throw new ArgumentNullException(nameof(clientAdapterFactory));

            if (logger is null) throw new ArgumentNullException(nameof(logger));
            _logger = logger.CreateScopedLogger(nameof(LowLevelMqttClient));
        }

        bool IsConnected => _adapter != null;

        public async Task ConnectAsync(IMqttClientOptions options, CancellationToken cancellationToken)
        {
            if (options is null) throw new ArgumentNullException(nameof(options));

            if (_adapter != null)
            {
                throw new InvalidOperationException("Low level MQTT client is already connected. Disconnect first before connecting again.");
            }

            var newAdapter = _clientAdapterFactory.CreateClientAdapter(options);

            try
            {
                _logger.Verbose($"Trying to connect with server '{options.ChannelOptions}' (Timeout={options.CommunicationTimeout}).");
                await newAdapter.ConnectAsync(options.CommunicationTimeout, cancellationToken).ConfigureAwait(false);
                _logger.Verbose("Connection with server established.");

                _options = options;
            }
            catch (Exception)
            {
                _adapter?.Dispose();
                throw;
            }

            _adapter = newAdapter;
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken)
        {
            if (_adapter == null)
            {
                return;
            }

            await SafeDisconnect(cancellationToken).ConfigureAwait(false);
            _adapter = null;
        }

        public async Task SendAsync(MqttBasePacket packet, CancellationToken cancellationToken)
        {
            if (packet is null) throw new ArgumentNullException(nameof(packet));

            if (_adapter == null)
            {
                throw new InvalidOperationException("Low level MQTT client is not connected.");
            }

            try
            {
                await _adapter.SendPacketAsync(packet, _options.CommunicationTimeout, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                await SafeDisconnect(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        public async Task<MqttBasePacket> ReceiveAsync(CancellationToken cancellationToken)
        {
            if (_adapter == null)
            {
                throw new InvalidOperationException("Low level MQTT client is not connected.");
            }

            try
            {
                return await _adapter.ReceivePacketAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                await SafeDisconnect(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        public void Dispose()
        {
            _adapter?.Dispose();
        }

        async Task SafeDisconnect(CancellationToken cancellationToken)
        {
            try
            {
                await _adapter.DisconnectAsync(_options.CommunicationTimeout, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Error while disconnecting.");
            }
            finally
            {
                _adapter.Dispose();
            }
        }
    }
}
