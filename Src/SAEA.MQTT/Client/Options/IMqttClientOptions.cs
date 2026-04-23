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
*文件名： IMqttClientOptions
*版本号： v26.4.23.1
*唯一标识：b6ce3890-1a1f-44c4-972d-cde4785d27d8
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
using SAEA.MQTT.Client.ExtendedAuthenticationExchange;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Packets;
using System;
using System.Collections.Generic;

namespace SAEA.MQTT.Client.Options
{
    public interface IMqttClientOptions
    {
        string ClientId { get; }
        bool CleanSession { get; }
        IMqttClientCredentials Credentials { get; }
        IMqttExtendedAuthenticationExchangeHandler ExtendedAuthenticationExchangeHandler { get; }
        MqttProtocolVersion ProtocolVersion { get; }
        IMqttClientChannelOptions ChannelOptions { get; }

        TimeSpan CommunicationTimeout { get; }
        TimeSpan KeepAlivePeriod { get; }
        MqttApplicationMessage WillMessage { get; }
        uint? WillDelayInterval { get; }

        string AuthenticationMethod { get; }
        byte[] AuthenticationData { get; }
        uint? MaximumPacketSize { get; }
        ushort? ReceiveMaximum { get; }
        bool? RequestProblemInformation { get; }
        bool? RequestResponseInformation { get; }
        uint? SessionExpiryInterval { get; }
        ushort? TopicAliasMaximum { get; }
        List<MqttUserProperty> UserProperties { get; set; }
    }
}