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
*唯一标识：78dadecd-b6ba-4533-90c6-98faf3ec4371
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttExtendedAuthenticationExchangeContext接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Client.ExtendedAuthenticationExchange
{
    /// <summary>
    /// 收到服务端 AUTH 报文时的增强认证交换上下文
    /// </summary>
    public class MqttExtendedAuthenticationExchangeContext
    {
        /// <summary>
        /// 由服务端 AUTH 报文与当前客户端创建上下文
        /// </summary>
        /// <param name="authPacket">服务端下发的 AUTH 报文，不能为 null</param>
        /// <param name="client">发起认证交换的客户端实例，不能为 null</param>
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

        /// <summary>
        /// AUTH 报文的原因码
        /// </summary>
        public MqttAuthenticateReasonCode ReasonCode { get; }

        /// <summary>
        /// 服务端给出的说明，未下发时为 null
        /// </summary>
        public string ReasonString { get; }

        /// <summary>
        /// 认证方法名，未下发时为 null
        /// </summary>
        public string AuthenticationMethod { get; }

        /// <summary>
        /// 服务端下发的认证数据，未下发时为 null
        /// </summary>
        public byte[] AuthenticationData { get; }

        /// <summary>
        /// 服务端下发的用户自定义属性，未下发时为 null
        /// </summary>
        public List<MqttUserProperty> UserProperties { get; }

        /// <summary>
        /// 发起本次认证交换的客户端实例
        /// </summary>
        public IMqttClient Client { get; }
    }
}
