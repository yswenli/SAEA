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
*命名空间：SAEA.MQTT.Client.ExtendedAuthenticationExchange
*文件名： MqttExtendedAuthenticationExchangeContext
*版本号： v26.4.23.1
*唯一标识：02e3098d-7af6-439c-997f-e6f4c2b31208
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
using System;
using System.Collections.Generic;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Client.ExtendedAuthenticationExchange
{
    public class MqttExtendedAuthenticationExchangeContext
    {
        public MqttExtendedAuthenticationExchangeContext(MqttAuthPacket authPacket, IMqttClient client)
        {
            if (authPacket == null) throw new ArgumentNullException(nameof(authPacket));

            ReasonCode = authPacket.ReasonCode;
            ReasonString = authPacket.Properties?.ReasonString;
            AuthenticationMethod = authPacket.Properties?.AuthenticationMethod;
            AuthenticationData = authPacket.Properties?.AuthenticationData;
            UserProperties = authPacket.Properties?.UserProperties;

            Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public MqttAuthenticateReasonCode ReasonCode { get; }

        public string ReasonString { get; }

        public string AuthenticationMethod { get; }

        public byte[] AuthenticationData { get; }

        public List<MqttUserProperty> UserProperties { get; }

        public IMqttClient Client { get; }
    }
}
