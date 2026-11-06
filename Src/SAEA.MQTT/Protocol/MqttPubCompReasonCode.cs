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
*命名空间：SAEA.MQTT.Protocol
*文件名： MqttPubCompReasonCode
*版本号： v26.4.23.1
*唯一标识：cd2d8988-4763-4e0e-981e-36b6fed9700d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttPubCompReasonCode代码枚举
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.MQTT.Protocol
{
    /// <summary>
    /// MQTT 5.0 QoS 2 发布完成（PUBCOMP）原因码
    /// </summary>
    public enum MqttPubCompReasonCode
    {
        /// <summary>
        /// 报文标识符对应的消息流程正常完成
        /// </summary>
        Success = 0,
        /// <summary>
        /// 未找到该报文标识符对应的流程
        /// </summary>
        PacketIdentifierNotFound = 146
    }
}
