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
*命名空间：SAEA.MQTT.Server
*文件名： MqttClientSubscribeResult
*版本号： v26.4.23.1
*唯一标识：ea647a4b-b29f-4972-90fe-2baeaba90f37
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientSubscribeResult接口
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
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 一次 SUBSCRIBE 报文的处理结果，按过滤器顺序记录返回码与原因码
    /// </summary>
    public sealed class MqttClientSubscribeResult
    {
        /// <summary>
        /// MQTT 3.1.1 的 SUBACK 返回码，与订阅过滤器一一对应
        /// </summary>
        public List<MqttSubscribeReturnCode> ReturnCodes { get; } = new List<MqttSubscribeReturnCode>();

        /// <summary>
        /// MQTT 5.0 的 SUBACK 原因码，与订阅过滤器一一对应
        /// </summary>
        public List<MqttSubscribeReasonCode> ReasonCodes { get; } = new List<MqttSubscribeReasonCode>();

        /// <summary>
        /// 是否在发出 SUBACK 后关闭连接
        /// </summary>
        public bool CloseConnection { get; set; }

        /// <summary>
        /// 整包协议错误时的 DISCONNECT 原因码。设置后不应发送 SUBACK。
        /// </summary>
        public MqttDisconnectReasonCode? DisconnectReasonCode { get; set; }

        /// <summary>
        /// 订阅成功后需要补发 retained 消息的过滤器集合（已按 RetainHandling 过滤，且不含共享过滤器）。
        /// </summary>
        internal List<MqttTopicFilter> RetainedMessageFilters { get; } = new List<MqttTopicFilter>();
    }
}
