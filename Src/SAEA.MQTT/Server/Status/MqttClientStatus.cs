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
*唯一标识：f0e6390d-be13-47f9-9757-577460bce1e4
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientStatus接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.MQTT.Formatter;
using System;
using System.Threading.Tasks;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Server.Status
{
    /// <summary>
    /// 客户端连接状态快照，统计值由连接对象在取状态时填充
    /// </summary>
    public sealed class MqttClientStatus : IMqttClientStatus
    {
        readonly MqttClientConnection _connection;

        /// <summary>
        /// 创建客户端状态对象，用于包装指定连接
        /// </summary>
        /// <param name="connection">被包装的连接，不能为 null，断开与重置统计都委托给它</param>
        public MqttClientStatus(MqttClientConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        /// <summary>
        /// 客户端标识
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// 客户端远端地址，形如 IP:端口
        /// </summary>
        public string Endpoint { get; set; }

        /// <summary>
        /// 该连接协商使用的 MQTT 协议版本
        /// </summary>
        public MqttProtocolVersion ProtocolVersion { get; set; }

        /// <summary>
        /// 最近一次收到报文的时间
        /// </summary>
        public DateTime LastPacketReceivedTimestamp { get; set; }

        /// <summary>
        /// 连接建立的时间
        /// </summary>
        public DateTime ConnectedTimestamp { get; set; }

        /// <summary>
        /// 最近一次收到非保活报文的时间，用于保活超时判定
        /// </summary>
        public DateTime LastNonKeepAlivePacketReceivedTimestamp { get; set; }

        /// <summary>
        /// 累计收到的应用消息条数
        /// </summary>
        public long ReceivedApplicationMessagesCount { get; set; }

        /// <summary>
        /// 累计发出的应用消息条数
        /// </summary>
        public long SentApplicationMessagesCount { get; set; }

        /// <summary>
        /// 累计收到的报文条数
        /// </summary>
        public long ReceivedPacketsCount { get; set; }

        /// <summary>
        /// 累计发出的报文条数
        /// </summary>
        public long SentPacketsCount { get; set; }

        /// <summary>
        /// 该连接关联的会话状态
        /// </summary>
        public IMqttSessionStatus Session { get; set; }

        /// <summary>
        /// 累计发送的字节数
        /// </summary>
        public long BytesSent { get; set; }

        /// <summary>
        /// 累计接收的字节数
        /// </summary>
        public long BytesReceived { get; set; }

        /// <summary>
        /// 以正常断开原因码主动断开该客户端连接
        /// </summary>
        /// <returns>断开完成的任务</returns>
        public Task DisconnectAsync()
        {
            return _connection.StopAsync(MqttDisconnectReasonCode.NormalDisconnection);
        }

        /// <summary>
        /// 清零该连接的报文、应用消息与字节计数
        /// </summary>
        public void ResetStatistics()
        {
            _connection.ResetStatistics();
        }
    }
}
