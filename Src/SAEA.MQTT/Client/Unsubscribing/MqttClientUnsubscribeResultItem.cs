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
*命名空间：SAEA.MQTT.Client.Unsubscribing
*文件名： MqttClientUnsubscribeResultItem
*版本号： v26.4.23.1
*唯一标识：35e422ed-0f25-454a-bafe-d4c0b4d8777c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientUnsubscribeResultItem接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;

namespace SAEA.MQTT.Client.Unsubscribing
{
    /// <summary>
    /// 单个主题过滤器的取消订阅结果
    /// </summary>
    public class MqttClientUnsubscribeResultItem
    {
        /// <summary>
        /// 用主题过滤器与服务端返回的原因码构造取消订阅结果
        /// </summary>
        /// <param name="topicFilter">取消订阅时提交的主题过滤器，不能为 null</param>
        /// <param name="reasonCode">服务端返回的原因码</param>
        public MqttClientUnsubscribeResultItem(string topicFilter, MqttClientUnsubscribeResultCode reasonCode)
        {
            TopicFilter = topicFilter ?? throw new ArgumentNullException(nameof(topicFilter));
            ReasonCode = reasonCode;
        }

        /// <summary>
        /// 取消订阅时提交的主题过滤器
        /// </summary>
        public string TopicFilter { get; }

        /// <summary>
        /// 服务端返回的原因码
        /// </summary>
        public MqttClientUnsubscribeResultCode ReasonCode { get; }
    }
}