/****************************************************************************
*项目名称：SAEA.MQTT.Core.Implementations
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Core.Implementations
*类 名 称：MqttClientSessionStatus
*版 本 号： v4.3.1.2
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 15:43:19
*描述：
*=====================================================================
*修改时间：2019/1/15 15:43:19
*修 改 人： yswenli
*版 本 号： v4.3.1.2
*描    述：
*****************************************************************************/
using SAEA.MQTT.Core.Protocol;
using SAEA.MQTT.Interface;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.MQTT.Core.Implementations
{
    public class MqttClientSessionStatus : IMqttClientSessionStatus
    {
        private readonly MqttClientSessionsManager _sessionsManager;
        private readonly MqttClientSession _session;

        public MqttClientSessionStatus(MqttClientSessionsManager sessionsManager, MqttClientSession session)
        {
            _sessionsManager = sessionsManager;
            _session = session;
        }

        public string ClientId { get; set; }
        public string Endpoint { get; set; }
        public bool IsConnected { get; set; }
        public MqttProtocolVersion? ProtocolVersion { get; set; }
        public TimeSpan LastPacketReceived { get; set; }
        public TimeSpan LastNonKeepAlivePacketReceived { get; set; }
        public int PendingApplicationMessagesCount { get; set; }

        public Task DisconnectAsync()
        {
            _session.Stop(MqttClientDisconnectType.NotClean);
            return Task.FromResult(0);
        }

        public Task DeleteSessionAsync()
        {
            try
            {
                _session.Stop(MqttClientDisconnectType.NotClean);
            }
            finally
            {
                _sessionsManager.DeleteSession(ClientId);
            }

            return Task.FromResult(0);
        }

        public Task ClearPendingApplicationMessagesAsync()
        {
            _session.ClearPendingApplicationMessages();

            return Task.FromResult(0);
        }
    }
}
