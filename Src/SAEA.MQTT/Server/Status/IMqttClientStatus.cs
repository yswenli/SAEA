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
*唯一标识：7feae04e-eabd-431d-beb2-1168b50673ba
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

namespace SAEA.MQTT.Server.Status
{
    public interface IMqttClientStatus
    {
        string ClientId { get; }

        string Endpoint { get; }

        MqttProtocolVersion ProtocolVersion { get; }

        DateTime LastPacketReceivedTimestamp { get; }

        DateTime LastNonKeepAlivePacketReceivedTimestamp { get; }

        long ReceivedApplicationMessagesCount { get; }

        long SentApplicationMessagesCount { get; }

        long ReceivedPacketsCount { get; }

        long SentPacketsCount { get; }

        IMqttSessionStatus Session { get; }

        long BytesSent { get; }

        long BytesReceived { get; }

        Task DisconnectAsync();

        void ResetStatistics();
    }
}
