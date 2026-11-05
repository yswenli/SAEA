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
 *命名空间：SAEA.MQTT.Internal
 *文件名： MqttMalformedPacketException
 *版本号： v26.4.23.1
 *唯一标识：658f8176-9dc3-4ef9-ab3c-15326e0cc136
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2021/03/11 16:46:45
 *描述：MqttMalformedPacketException类
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/05 00:00:00
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：MqttMalformedPacketException类
 *
 *****************************************************************************/
using SAEA.MQTT.Exceptions;

namespace SAEA.MQTT.Internal
{
    /// <summary>
    /// 报文解析失败（固定头或正文结构非法）时抛出，映射为 MQTT 5.0 的 0x81 Malformed Packet；
    /// 与“解析成功但违反协议”（0x82 Protocol Error）区分。
    /// </summary>
    internal sealed class MqttMalformedPacketException : MqttProtocolViolationException
    {
        public MqttMalformedPacketException(string message)
            : base(message)
        {
        }
    }
}