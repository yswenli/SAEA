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
*文件名： MqttDisconnectPacket
*版本号： v26.4.23.1
*唯一标识：03d5c869-40c7-47fe-9c67-22808d57e3ea
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttDisconnectPacket接口
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
    /// DISCONNECT 报文，任一端主动断开连接时发送
    /// </summary>
    public sealed class MqttDisconnectPacket : MqttBasePacket
    {
        #region Added in MQTTv5

        /// <summary>
        /// 断开原因码，仅 MQTT 5.0 使用
        /// </summary>
        public MqttDisconnectReasonCode? ReasonCode { get; set; }

        /// <summary>
        /// 断开属性，仅 MQTT 5.0 使用
        /// </summary>
        public MqttDisconnectPacketProperties Properties { get; set; }

        #endregion

        /// <summary>
        /// 返回包含断开原因码的描述文本
        /// </summary>
        /// <returns>报文描述文本</returns>
        public override string ToString()
        {
            return string.Concat("Disconnect: [ReasonCode=", ReasonCode, "]");
        }
    }
}
