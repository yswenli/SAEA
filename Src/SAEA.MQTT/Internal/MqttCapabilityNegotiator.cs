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
 *命名空间：SAEA.MQTT.Internal
 *文件名： MqttCapabilityNegotiator
 *版本号： v26.4.23.1
 *唯一标识：ab7c4c93-8f2c-4b2a-9b57-6d1a2c3e1103
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2021/03/11 16:46:45
 *描述：MqttCapabilityNegotiator类
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/05 00:00:00
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：MqttCapabilityNegotiator类
 *
 *****************************************************************************/
using System;
using SAEA.MQTT.Exceptions;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Server;

namespace SAEA.MQTT.Internal
{
    /// <summary>
    /// 纯函数能力协商器：把服务端配置与客户端 CONNECT 属性合并为每连接能力快照。
    /// </summary>
    internal static class MqttCapabilityNegotiator
    {
        internal static MqttConnectionCapabilities Negotiate(
            MqttServerCapabilities serverCapabilities,
            MqttConnectPacket connectPacket,
            MqttProtocolVersion protocolVersion,
            string assignedClientIdentifier)
        {
            if (serverCapabilities == null) throw new ArgumentNullException(nameof(serverCapabilities));
            if (connectPacket == null) throw new ArgumentNullException(nameof(connectPacket));

            var capabilities = new MqttConnectionCapabilities
            {
                ProtocolVersion = protocolVersion,

                ReceiveMaximum = serverCapabilities.ReceiveMaximum,
                MaximumPacketSize = serverCapabilities.MaximumPacketSize,
                TopicAliasMaximum = serverCapabilities.TopicAliasMaximum,
                MaximumQoS = serverCapabilities.MaximumQoS,
                RetainAvailable = serverCapabilities.RetainAvailable,
                WildcardSubscriptionAvailable = serverCapabilities.WildcardSubscriptionAvailable,
                SubscriptionIdentifiersAvailable = serverCapabilities.SubscriptionIdentifiersAvailable,
                SharedSubscriptionAvailable = serverCapabilities.SharedSubscriptionAvailable,
                ServerKeepAlive = serverCapabilities.ServerKeepAlive ?? connectPacket.KeepAlivePeriod,

                MaximumSessionExpiryInterval = serverCapabilities.MaximumSessionExpiryInterval ?? uint.MaxValue,

                AssignedClientIdentifier = assignedClientIdentifier
            };

            if (protocolVersion != MqttProtocolVersion.V500)
            {
                // V310/V311 不读取 Properties，客户端能力保持默认值。
                capabilities.SessionExpiryInterval = 0;
                return capabilities;
            }

            var properties = connectPacket.Properties;

            var clientReceiveMaximum = properties?.ReceiveMaximum;
            if (clientReceiveMaximum.HasValue && clientReceiveMaximum.Value == 0)
            {
                throw new MqttProtocolViolationException("The CONNECT packet contains an invalid Receive Maximum of 0.");
            }

            var clientMaximumPacketSize = properties?.MaximumPacketSize;
            if (clientMaximumPacketSize.HasValue && clientMaximumPacketSize.Value == 0)
            {
                throw new MqttProtocolViolationException("The CONNECT packet contains an invalid Maximum Packet Size of 0.");
            }

            capabilities.ClientReceiveMaximum = clientReceiveMaximum ?? ushort.MaxValue;
            capabilities.ClientMaximumPacketSize = clientMaximumPacketSize ?? 268435455;
            capabilities.ClientTopicAliasMaximum = properties?.TopicAliasMaximum ?? 0;
            capabilities.RequestProblemInformation = properties?.RequestProblemInformation ?? true;

            var clientSessionExpiryInterval = properties?.SessionExpiryInterval ?? 0;
            capabilities.SessionExpiryInterval = Min(clientSessionExpiryInterval, capabilities.MaximumSessionExpiryInterval);

            return capabilities;
        }

        static uint Min(uint value1, uint value2)
        {
            return value1 < value2 ? value1 : value2;
        }
    }
}