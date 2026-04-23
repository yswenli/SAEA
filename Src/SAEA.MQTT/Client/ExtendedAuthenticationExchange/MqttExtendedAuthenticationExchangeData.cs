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
*文件名： MqttExtendedAuthenticationExchangeData
*版本号： v26.4.23.1
*唯一标识：f57f9228-33b8-4f68-86ce-22dde5f7a90a
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
using System.Collections.Generic;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Client.ExtendedAuthenticationExchange
{
    public class MqttExtendedAuthenticationExchangeData
    {
        public MqttAuthenticateReasonCode ReasonCode { get; set; }

        public string ReasonString { get; set; }

        public byte[] AuthenticationData { get; set; }

        public List<MqttUserProperty> UserProperties { get; }
    }
}
