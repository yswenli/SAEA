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
*命名空间：SAEA.MQTT.Server
*文件名： MqttServerKeepAliveMonitor
*版本号： v26.4.23.1
*唯一标识：f83ff81d-e392-47fd-92bc-bc01d7f441c9
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using SAEA.MQTT.Diagnostics;
using SAEA.MQTT.Internal;
using System;
using System.Threading;
using System.Threading.Tasks;
using SAEA.MQTT.Implementations;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Server
{
    public sealed class MqttServerKeepAliveMonitor
    {
        readonly IMqttServerOptions _options;
        readonly MqttClientSessionsManager _sessionsManager;
        readonly IMqttNetScopedLogger _logger;

        public MqttServerKeepAliveMonitor(IMqttServerOptions options, MqttClientSessionsManager sessionsManager, IMqttNetLogger logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _sessionsManager = sessionsManager ?? throw new ArgumentNullException(nameof(sessionsManager));
            
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            _logger = logger.CreateScopedLogger(nameof(MqttServerKeepAliveMonitor));
        }

        public void Start(CancellationToken cancellationToken)
        {
            // The keep alive monitor spawns a real new thread (LongRunning) because it does not 
            // support async/await. Async etc. is avoided here because the thread will usually check
            // the connections every few milliseconds and thus the context changes (due to async) are 
            // only consuming resources. Also there is just 1 thread for the entire server which is fine at all!
            Task.Factory.StartNew(_ => DoWork(cancellationToken), cancellationToken, TaskCreationOptions.LongRunning).Forget(_logger);
        }

        void DoWork(CancellationToken cancellationToken)
        {
            try
            {
                _logger.Info("Starting keep alive monitor.");

                while (!cancellationToken.IsCancellationRequested)
                {
                    TryMaintainConnections();
                    PlatformAbstractionLayer.Sleep(_options.KeepAliveMonitorInterval);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Unhandled exception while checking keep alive timeouts.");
            }
            finally
            {
                _logger.Verbose("Stopped checking keep alive timeout.");
            }
        }

        void TryMaintainConnections()
        {
            var now = DateTime.UtcNow;
            foreach (var connection in _sessionsManager.GetConnections())
            {
                TryMaintainConnection(connection, now);
            }
        }

        void TryMaintainConnection(MqttClientConnection connection, DateTime now)
        {
            try
            {
                if (connection.Status != MqttClientConnectionStatus.Running)
                {
                    // The connection is already dead or just created so there is no need to check it.
                    return;
                }

                if (connection.ConnectPacket.KeepAlivePeriod == 0)
                {
                    // The keep alive feature is not used by the current connection.
                    return;
                }

                if (connection.IsReadingPacket)
                {
                    // The connection is currently reading a (large) packet. So it is obviously 
                    // doing something and thus "connected".
                    return;
                }

                // Values described here: [MQTT-3.1.2-24].
                // If the client sends 5 sec. the server will allow up to 7.5 seconds.
                // If the client sends 1 sec. the server will allow up to 1.5 seconds.
                var maxDurationWithoutPacket = connection.ConnectPacket.KeepAlivePeriod * 1.5D;

                var secondsWithoutPackage = (now - connection.LastPacketReceivedTimestamp).TotalSeconds;
                if (secondsWithoutPackage < maxDurationWithoutPacket)
                {
                    // A packet was received before the timeout is affected.
                    return;
                }

                _logger.Warning(null, "Client '{0}': Did not receive any packet or keep alive signal.", connection.ClientId);

                // Execute the disconnection in background so that the keep alive monitor can continue
                // with checking other connections.
                // We do not need to wait for the task so no await is needed.
                // Also the internal state of the connection must be swapped to "Finalizing" because the
                // next iteration of the keep alive timer happens.
                var _ = connection.StopAsync(MqttDisconnectReasonCode.KeepAliveTimeout);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Client {0}: Unhandled exception while checking keep alive timeouts.", connection.ClientId);
            }
        }
    }
}
