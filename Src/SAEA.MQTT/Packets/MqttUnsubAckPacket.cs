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
*文件名： MqttUnsubAckPacket
*版本号： v26.4.23.1
*唯一标识：3bd51153-b51b-49b4-addb-84102e6db3e0
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttUnsubAckPacket数据包类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System.Collections.Generic;
using System.Linq;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Packets
{
    /// <summary>
    /// UNSUBACK 报文，服务端对 UNSUBSCRIBE 的应答
    /// </summary>
    public sealed class MqttUnsubAckPacket : MqttBasePacket, IMqttPacketWithIdentifier
    {
        /// <summary>
        /// 被应答的 UNSUBSCRIBE 报文标识符
        /// </summary>
        public ushort PacketIdentifier { get; set; }

        #region Added in MQTTv5

        /// <summary>
        /// 应答属性，仅 MQTT 5.0 使用
        /// </summary>
        public MqttUnsubAckPacketProperties Properties { get; set; }

        /// <summary>
        /// 每个主题过滤器对应的取消订阅原因码，仅 MQTT 5.0 使用，按请求顺序排列
        /// </summary>
        public List<MqttUnsubscribeReasonCode> ReasonCodes { get; set; } = new List<MqttUnsubscribeReasonCode>();

        #endregion

        /// <summary>
        /// 返回包含报文标识符与各原因码的描述文本
        /// </summary>
        /// <returns>报文描述文本</returns>
        public override string ToString()
        {
            var reasonCodesText = string.Join(",", ReasonCodes.Select(f => f.ToString()));

            return string.Concat("UnsubAck: [PacketIdentifier=", PacketIdentifier, "] [ReasonCodes=", reasonCodesText, "]");
        }
    }
}
