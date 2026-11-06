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
*唯一标识：963a3008-ffe8-481e-a71a-c4d9a9f4ab7e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttSubAckPacket数据包类
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
    /// SUBACK 报文，服务端对 SUBSCRIBE 的应答
    /// </summary>
    public sealed class MqttSubAckPacket : MqttBasePacket, IMqttPacketWithIdentifier
    {
        /// <summary>
        /// 被应答的 SUBSCRIBE 报文标识符
        /// </summary>
        public ushort PacketIdentifier { get; set; }

        /// <summary>
        /// 每个主题过滤器对应的订阅返回码，仅 MQTT 3.1.1 使用
        /// </summary>
        public List<MqttSubscribeReturnCode> ReturnCodes { get; set; } = new List<MqttSubscribeReturnCode>();

        #region Added in MQTTv5.0.0

        /// <summary>
        /// 每个主题过滤器对应的订阅原因码，仅 MQTT 5.0 使用，按订阅顺序排列
        /// </summary>
        public List<MqttSubscribeReasonCode> ReasonCodes { get; } = new List<MqttSubscribeReasonCode>();

        /// <summary>
        /// 应答属性，仅 MQTT 5.0 使用
        /// </summary>
        public MqttSubAckPacketProperties Properties { get; set; }

        #endregion

        /// <summary>
        /// 返回包含报文标识符、返回码与原因码的描述文本
        /// </summary>
        /// <returns>报文描述文本</returns>
        public override string ToString()
        {
            var returnCodesText = string.Join(",", ReturnCodes.Select(f => f.ToString()));
            var reasonCodesText = string.Join(",", ReasonCodes.Select(f => f.ToString()));

            return string.Concat("SubAck: [PacketIdentifier=", PacketIdentifier, "] [ReturnCodes=", returnCodesText, "] [ReasonCode=", reasonCodesText, "]");
        }
    }
}
