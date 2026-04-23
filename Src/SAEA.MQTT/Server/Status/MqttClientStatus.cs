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
*命名空间：SAEA.MQTT.Server.Status
*文件名： MqttClientStatus
*版本号： v26.4.23.1
*唯一标识：7f7256e1-91b9-4ebd-a5df-0aa8139c4bfb
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
using SAEA.MQTT.Formatter;
using System;
using System.Threading.Tasks;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Server.Status
{
    public sealed class MqttClientStatus : IMqttClientStatus
    {
        readonly MqttClientConnection _connection;

        public MqttClientStatus(MqttClientConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public string ClientId { get; set; }

        public string Endpoint { get; set; }

        public MqttProtocolVersion ProtocolVersion { get; set; }

        public DateTime LastPacketReceivedTimestamp { get; set; }

        public DateTime ConnectedTimestamp { get; set; }

        public DateTime LastNonKeepAlivePacketReceivedTimestamp { get; set; }

        public long ReceivedApplicationMessagesCount { get; set; }

        public long SentApplicationMessagesCount { get; set; }

        public long ReceivedPacketsCount { get; set; }

        public long SentPacketsCount { get; set; }

        public IMqttSessionStatus Session { get; set; }

        public long BytesSent { get; set; }

        public long BytesReceived { get; set; }

        public Task DisconnectAsync()
        {
            return _connection.StopAsync(MqttDisconnectReasonCode.NormalDisconnection);
        }

        public void ResetStatistics()
        {
            _connection.ResetStatistics();
        }
    }
}
