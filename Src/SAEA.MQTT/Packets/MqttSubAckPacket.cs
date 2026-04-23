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
*命名空间：SAEA.MQTT.Packets
*文件名： MqttSubAckPacket
*版本号： v26.4.23.1
*唯一标识：085eaf33-509e-4625-a32f-2f02ce1f07aa
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MQTT协议数据包类
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MQTT协议数据包类
*
*****************************************************************************/
using System.Collections.Generic;
using System.Linq;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Packets
{
    public sealed class MqttSubAckPacket : MqttBasePacket, IMqttPacketWithIdentifier
    {
        public ushort PacketIdentifier { get; set; }

        public List<MqttSubscribeReturnCode> ReturnCodes { get; set; } = new List<MqttSubscribeReturnCode>();

        #region Added in MQTTv5.0.0

        public List<MqttSubscribeReasonCode> ReasonCodes { get; } = new List<MqttSubscribeReasonCode>();

        public MqttSubAckPacketProperties Properties { get; set; }

        #endregion

        public override string ToString()
        {
            var returnCodesText = string.Join(",", ReturnCodes.Select(f => f.ToString()));
            var reasonCodesText = string.Join(",", ReasonCodes.Select(f => f.ToString()));

            return string.Concat("SubAck: [PacketIdentifier=", PacketIdentifier, "] [ReturnCodes=", returnCodesText, "] [ReasonCode=", reasonCodesText, "]");
        }
    }
}
