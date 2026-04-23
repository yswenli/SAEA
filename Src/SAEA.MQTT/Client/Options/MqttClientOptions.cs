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
*命名空间：SAEA.MQTT.Client.Options
*文件名： MqttClientOptions
*版本号： v26.4.23.1
*唯一标识：65121ddf-053b-488d-b509-bc536b3294d4
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MQTT客户端类
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MQTT客户端类
*
*****************************************************************************/
using SAEA.MQTT.Client.ExtendedAuthenticationExchange;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Packets;
using System;
using System.Collections.Generic;

namespace SAEA.MQTT.Client.Options
{
    public class MqttClientOptions : IMqttClientOptions
    {
        public string ClientId { get; set; } = Guid.NewGuid().ToString("N");
        public bool CleanSession { get; set; } = true;
        public IMqttClientCredentials Credentials { get; set; }
        public IMqttExtendedAuthenticationExchangeHandler ExtendedAuthenticationExchangeHandler { get; set; }
        public MqttProtocolVersion ProtocolVersion { get; set; } = MqttProtocolVersion.V311;

        public IMqttClientChannelOptions ChannelOptions { get; set; }
        public TimeSpan CommunicationTimeout { get; set; } = TimeSpan.FromSeconds(10);
        public TimeSpan KeepAlivePeriod { get; set; } = TimeSpan.FromSeconds(15);

        public MqttApplicationMessage WillMessage { get; set; }
        public uint? WillDelayInterval { get; set; }

        public string AuthenticationMethod { get; set; }
        public byte[] AuthenticationData { get; set; }

        public uint? MaximumPacketSize { get; set; }
        public ushort? ReceiveMaximum { get; set; }
        public bool? RequestProblemInformation { get; set; }
        public bool? RequestResponseInformation { get; set; }
        public uint? SessionExpiryInterval { get; set; }
        public ushort? TopicAliasMaximum { get; set; }
        public List<MqttUserProperty> UserProperties { get; set; }
    }
}
