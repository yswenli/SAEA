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
*命名空间：SAEA.MQTT.Client.Connecting
*文件名： MqttClientAuthenticateResult
*版本号： v26.4.23.1
*唯一标识：244fe14d-986e-4115-afd0-b7664f684f4a
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientAuthenticateResult接口
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MqttClientAuthenticateResult接口
*
*****************************************************************************/
using System.Collections.Generic;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Client.Connecting
{
    // TODO: Consider renaming this to _MqttClientConnectResult_
    public class MqttClientAuthenticateResult
    {
        public MqttClientConnectResultCode ResultCode { get; set; }

        public bool IsSessionPresent { get; set; }

        public bool? WildcardSubscriptionAvailable { get; set; }

        public bool? RetainAvailable { get; set; }

        public string AssignedClientIdentifier { get; set; }

        public string AuthenticationMethod { get; set; }

        public byte[] AuthenticationData { get; set; }

        public uint? MaximumPacketSize { get; set; }

        public string ReasonString { get; set; }

        public ushort? ReceiveMaximum { get; set; }
        
        public MqttQualityOfServiceLevel MaximumQoS { get; set; }

        public string ResponseInformation { get; set; }

        public ushort? TopicAliasMaximum { get; set; }

        public string ServerReference { get; set; }

        public ushort? ServerKeepAlive { get; set; }

        public uint? SessionExpiryInterval { get; set; }

        public bool? SubscriptionIdentifiersAvailable { get; set; }

        public bool? SharedSubscriptionAvailable { get; set; }

        public List<MqttUserProperty> UserProperties { get; set; }
    }
}
