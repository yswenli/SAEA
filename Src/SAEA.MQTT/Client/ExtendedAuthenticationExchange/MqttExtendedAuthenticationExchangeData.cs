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
*唯一标识：45cc4b50-7aa4-4a0c-9ab2-f884592a7082
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttExtendedAuthenticationExchangeData接口
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MqttExtendedAuthenticationExchangeData接口
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
