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
 *文件名： MqttRetainedMessageEntry
 *版本号： v26.4.23.1
 *唯一标识：6f0f0f70-2a4b-4d4d-9a0e-2f0d7a0b1c07
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2021/03/11 16:46:45
 *描述：MqttRetainedMessageEntry类
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

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// Retained 消息及其存留时间元数据。绝对到期时间用于跨重启保持 Message Expiry 语义。
    /// </summary>
    public sealed class MqttRetainedMessageEntry
    {
        /// <summary>
        /// 供反序列化创建空实例
        /// </summary>
        public MqttRetainedMessageEntry()
        {
        }

        /// <summary>
        /// 用消息内容与存留时间元数据创建实例
        /// </summary>
        /// <param name="message">retained 消息本体</param>
        /// <param name="retainedAt">消息被设为 retained 的时间</param>
        /// <param name="expiresAt">绝对到期时间，null 表示永不过期</param>
        public MqttRetainedMessageEntry(MqttApplicationMessage message, DateTimeOffset retainedAt, DateTimeOffset? expiresAt)
        {
            Message = message;
            RetainedAt = retainedAt;
            ExpiresAt = expiresAt;
        }

        /// <summary>
        /// retained 消息本体
        /// </summary>
        public MqttApplicationMessage Message { get; set; }

        /// <summary>
        /// 消息被设为 retained 的时间
        /// </summary>
        public DateTimeOffset RetainedAt { get; set; }

        /// <summary>
        /// 绝对到期时间，null 表示永不过期
        /// </summary>
        public DateTimeOffset? ExpiresAt { get; set; }
    }
}