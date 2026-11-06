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
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
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
    /// <summary>
    /// 底层MQTT客户端实现，基于通道适配器直接收发报文
    /// </summary>
    public sealed class LowLevelMqttClient : ILowLevelMqttClient
    {
        readonly IMqttNetScopedLogger _logger;
        readonly IMqttClientAdapterFactory _clientAdapterFactory;

        IMqttChannelAdapter _adapter;
        IMqttClientOptions _options;

        /// <summary>
        /// 使用通道适配器工厂与日志器初始化实例
        /// </summary>
        /// <param name="clientAdapterFactory">通道适配器工厂，不能为null</param>
        /// <param name="logger">根日志器，不能为null；内部会创建独立作用域的日志器</param>
        public LowLevelMqttClient(IMqttClientAdapterFactory clientAdapterFactory, IMqttNetLogger logger)
        {
            _clientAdapterFactory = clientAdapterFactory ?? throw new ArgumentNullException(nameof(clientAdapterFactory));

            if (logger is null) throw new ArgumentNullException(nameof(logger));
            _logger = logger.CreateScopedLogger(nameof(LowLevelMqttClient));
        }

        bool IsConnected => _adapter != null;

        /// <summary>
        /// 建立连接并保存连接选项，已连接时抛出 InvalidOperationException
        /// </summary>
        /// <param name="options">连接选项，不能为null</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>连接完成的异步任务</returns>
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

        /// <summary>
        /// 断开连接并释放通道适配器，未连接时直接返回
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>断开完成的异步任务</returns>
        public async Task DisconnectAsync(CancellationToken cancellationToken)
        {
            if (_adapter == null)
            {
                return;
            }

            await SafeDisconnect(cancellationToken).ConfigureAwait(false);
            _adapter = null;
        }

        /// <summary>
        /// 发送一个MQTT报文，未连接时抛出异常，发送失败时自动断开
        /// </summary>
        /// <param name="packet">待发送报文，不能为null</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>发送完成的异步任务</returns>
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

        /// <summary>
        /// 接收一个MQTT报文，未连接时抛出异常，接收失败时自动断开
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>接收到的报文</returns>
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

        /// <summary>
        /// 释放当前通道适配器，未连接时不做任何操作
        /// </summary>
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
