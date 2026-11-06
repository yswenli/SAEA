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
*命名空间：SAEA.MQTT.Extensions.ManagedClient
*文件名： ManagedMqttClientOptions
*版本号： v26.4.23.1
*唯一标识：de0d81d2-b708-4e79-82b8-34d0e59fbdb4
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：ManagedMqttClientOptions接口
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
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Server;

namespace SAEA.MQTT.Extensions.ManagedClient
{
    /// <summary>
    /// 托管客户端选项的默认实现
    /// </summary>
    public class ManagedMqttClientOptions : IManagedMqttClientOptions
    {
        /// <summary>
        /// 内部MQTT客户端的连接选项
        /// </summary>
        public IMqttClientOptions ClientOptions { get; set; }

        /// <summary>
        /// 是否启用断线自动重连，默认启用
        /// </summary>
        public bool AutoReconnect { get; set; } = true;

        /// <summary>
        /// 重连失败后再次尝试的间隔，默认5秒
        /// </summary>
        public TimeSpan AutoReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 连接维护循环的检查间隔，默认1秒
        /// </summary>
        public TimeSpan ConnectionCheckInterval { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// 待发消息的持久化存储，为null表示不持久化
        /// </summary>
        public IManagedMqttClientStorage Storage { get; set; }

        /// <summary>
        /// 待发队列允许的最大消息数，默认不限制
        /// </summary>
        public int MaxPendingMessages { get; set; } = int.MaxValue;

        /// <summary>
        /// 待发队列达到上限时的溢出处理策略，默认丢弃新消息
        /// </summary>
        public MqttPendingMessagesOverflowStrategy PendingMessagesOverflowStrategy { get; set; } = MqttPendingMessagesOverflowStrategy.DropNewMessage;
    }
}
