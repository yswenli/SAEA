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
*文件名： MqttPubCompPacket
*版本号： v26.4.23.1
*唯一标识：0347e53e-f0c5-4c9f-b656-d87a7653b974
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttPubCompPacket数据包类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Packets
{
    /// <summary>
    /// PUBCOMP 报文，QoS 2 消息握手流程的最后一步
    /// </summary>
    public sealed class MqttPubCompPacket : MqttBasePacket, IMqttPacketWithIdentifier
    {
        /// <summary>
        /// 被确认的 PUBLISH 报文标识符
        /// </summary>
        public ushort PacketIdentifier { get; set; }

        #region Added in MQTTv5

        /// <summary>
        /// 确认原因码，仅 MQTT 5.0 使用
        /// </summary>
        public MqttPubCompReasonCode? ReasonCode { get; set; }

        /// <summary>
        /// 确认属性，仅 MQTT 5.0 使用
        /// </summary>
        public MqttPubCompPacketProperties Properties { get; set; }

        #endregion

        /// <summary>
        /// 返回包含报文标识符与原因码的描述文本
        /// </summary>
        /// <returns>报文描述文本</returns>
        public override string ToString()
        {
            return string.Concat("PubComp: [PacketIdentifier=", PacketIdentifier, "] [ReasonCode=", ReasonCode, "]");
        }
    }
}
