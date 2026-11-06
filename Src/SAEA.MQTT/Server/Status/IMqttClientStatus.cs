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
*文件名： IMqttClientStatus
*版本号： v26.4.23.1
*唯一标识：334d5ed7-23cb-465d-883a-0f37886bf50f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttClientStatus接口
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

namespace SAEA.MQTT.Server.Status
{
    /// <summary>
    /// 单个客户端连接的运行时状态快照与流量统计
    /// </summary>
    public interface IMqttClientStatus
    {
        /// <summary>
        /// 客户端标识
        /// </summary>
        string ClientId { get; }

        /// <summary>
        /// 客户端远端地址，形如 IP:端口
        /// </summary>
        string Endpoint { get; }

        /// <summary>
        /// 该连接协商使用的 MQTT 协议版本
        /// </summary>
        MqttProtocolVersion ProtocolVersion { get; }

        /// <summary>
        /// 最近一次收到报文的时间
        /// </summary>
        DateTime LastPacketReceivedTimestamp { get; }

        /// <summary>
        /// 最近一次收到非保活报文的时间，用于保活超时判定
        /// </summary>
        DateTime LastNonKeepAlivePacketReceivedTimestamp { get; }

        /// <summary>
        /// 累计收到的应用消息条数
        /// </summary>
        long ReceivedApplicationMessagesCount { get; }

        /// <summary>
        /// 累计发出的应用消息条数
        /// </summary>
        long SentApplicationMessagesCount { get; }

        /// <summary>
        /// 累计收到的报文条数
        /// </summary>
        long ReceivedPacketsCount { get; }

        /// <summary>
        /// 累计发出的报文条数
        /// </summary>
        long SentPacketsCount { get; }

        /// <summary>
        /// 该连接关联的会话状态
        /// </summary>
        IMqttSessionStatus Session { get; }

        /// <summary>
        /// 累计发送的字节数
        /// </summary>
        long BytesSent { get; }

        /// <summary>
        /// 累计接收的字节数
        /// </summary>
        long BytesReceived { get; }

        /// <summary>
        /// 以正常断开原因码主动断开该客户端连接
        /// </summary>
        /// <returns>断开完成的任务</returns>
        Task DisconnectAsync();

        /// <summary>
        /// 清零该连接的报文、应用消息与字节计数
        /// </summary>
        void ResetStatistics();
    }
}
