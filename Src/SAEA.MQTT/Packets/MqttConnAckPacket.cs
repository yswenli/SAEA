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
*文件名： MqttConnAckPacket
*版本号： v26.4.23.1
*唯一标识：56538e5a-6a49-48d6-8068-4ca0c5867513
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttConnAckPacket数据包类
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
    /// CONNACK 报文，服务端对 CONNECT 的应答
    /// </summary>
    public sealed class MqttConnAckPacket : MqttBasePacket
    {
        /// <summary>
        /// CONNACK 返回码，仅 MQTT 3.1.1 及更早版本使用
        /// </summary>
        public MqttConnectReturnCode? ReturnCode { get; set; }

        #region Added in MQTTv3.1.1

        /// <summary>
        /// 服务端是否已存在该客户端标识对应的会话
        /// </summary>
        public bool IsSessionPresent { get; set; }

        #endregion

        #region Added in MQTTv5.0.0

        /// <summary>
        /// 连接原因码，仅 MQTT 5.0 使用
        /// </summary>
        public MqttConnectReasonCode? ReasonCode { get; set; }

        /// <summary>
        /// 连接属性，仅 MQTT 5.0 使用
        /// </summary>
        public MqttConnAckPacketProperties Properties { get; set; }

        #endregion

        /// <summary>
        /// 返回包含返回码、原因码与会话状态的描述文本
        /// </summary>
        /// <returns>报文描述文本</returns>
        public override string ToString()
        {
            return string.Concat("ConnAck: [ReturnCode=", ReturnCode, "] [ReasonCode=", ReasonCode, "] [IsSessionPresent=", IsSessionPresent, "]");
        }
    }
}
