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
*文件名： MqttPublishPacket
*版本号： v26.4.23.1
*唯一标识：6816f607-020a-4774-8292-5d4a5ecbc97b
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttPublishPacket接口
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
    /// PUBLISH 报文，承载应用消息的主题与载荷
    /// </summary>
    public sealed class MqttPublishPacket : MqttBasePacket, IMqttPacketWithIdentifier
    {
        /// <summary>
        /// 报文标识符，仅 QoS 1/2 使用，QoS 0 时为 0
        /// </summary>
        public ushort PacketIdentifier { get; set; }

        /// <summary>
        /// 是否作为保留消息发布
        /// </summary>
        public bool Retain { get; set; }

        /// <summary>
        /// 消息的服务质量等级
        /// </summary>
        public MqttQualityOfServiceLevel QualityOfServiceLevel { get; set; }

        /// <summary>
        /// 是否为重复投递的报文
        /// </summary>
        public bool Dup { get; set; }

        /// <summary>
        /// 主题名，可用主题别名替代
        /// </summary>
        public string Topic { get; set; }

        /// <summary>
        /// 消息载荷，可为 null 表示空载荷
        /// </summary>
        public byte[] Payload { get; set; }

        #region Added in MQTTv5

        /// <summary>
        /// 发布属性，仅 MQTT 5.0 使用
        /// </summary>
        public MqttPublishPacketProperties Properties { get; set; }

        #endregion

        /// <summary>
        /// 返回包含主题、载荷长度、QoS 等信息的描述文本
        /// </summary>
        /// <returns>报文描述文本</returns>
        public override string ToString()
        {
            return string.Concat("Publish: [Topic=", Topic, "] [Payload.Length=", Payload?.Length, "] [QoSLevel=", QualityOfServiceLevel, "] [Dup=", Dup, "] [Retain=", Retain, "] [PacketIdentifier=", PacketIdentifier, "]");
        }
    }
}
