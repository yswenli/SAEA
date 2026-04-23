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
*文件名： MqttPubRelPacket
*版本号： v26.4.23.1
*唯一标识：60ee830e-0c4e-4fd6-beef-91fee7e0b549
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttPubRelPacket数据包类
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MqttPubRelPacket数据包类
*
*****************************************************************************/
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Packets
{
    public sealed class MqttPubRelPacket : MqttBasePacket, IMqttPacketWithIdentifier
    {
        public ushort PacketIdentifier { get; set; }

        #region Added in MQTTv5

        public MqttPubRelReasonCode? ReasonCode { get; set; }

        public MqttPubRelPacketProperties Properties { get; set; }

        #endregion

        public override string ToString()
        {
            return string.Concat("PubRel: [PacketIdentifier=", PacketIdentifier, "] [ReasonCode=", ReasonCode, "]");
        }
    }
}
