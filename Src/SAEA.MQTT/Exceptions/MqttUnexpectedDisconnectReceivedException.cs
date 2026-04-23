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
*命名空间：SAEA.MQTT.Exceptions
*文件名： MqttUnexpectedDisconnectReceivedException
*版本号： v26.4.23.1
*唯一标识：60be9437-bd76-4332-8757-ebe3e4072375
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MQTT异常类类
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MQTT异常类类
*
*****************************************************************************/
using System.Collections.Generic;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Exceptions
{
    public class MqttUnexpectedDisconnectReceivedException : MqttCommunicationException
    {
        public MqttUnexpectedDisconnectReceivedException(MqttDisconnectPacket disconnectPacket) 
            : base($"Unexpected DISCONNECT (Reason code={disconnectPacket.ReasonCode}) received.")
        {
            ReasonCode = disconnectPacket.ReasonCode;
            SessionExpiryInterval = disconnectPacket.Properties?.SessionExpiryInterval;
            ReasonString = disconnectPacket.Properties?.ReasonString;
            ServerReference = disconnectPacket.Properties?.ServerReference;
            UserProperties = disconnectPacket.Properties?.UserProperties;
        }

        public MqttDisconnectReasonCode? ReasonCode { get; }

        public uint? SessionExpiryInterval { get; }

        public string ReasonString { get; }

        public List<MqttUserProperty> UserProperties { get; }

        public string ServerReference { get; }
    }
}
