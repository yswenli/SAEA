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
*文件名： IManagedMqttClientOptions
*版本号： v26.4.23.1
*唯一标识：6817f549-b85e-4b47-8239-9d6ba3048074
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IManagedMqttClientOptions接口
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
    /// 托管客户端选项
    /// </summary>
    public interface IManagedMqttClientOptions
    {
        /// <summary>
        /// 内部MQTT客户端的连接选项
        /// </summary>
        IMqttClientOptions ClientOptions { get; }

        /// <summary>
        /// 是否启用断线自动重连
        /// </summary>
        bool AutoReconnect { get; }

        /// <summary>
        /// 重连失败后再次尝试的间隔
        /// </summary>
        TimeSpan AutoReconnectDelay { get; }

        /// <summary>
        /// 连接维护循环的检查间隔
        /// </summary>
        TimeSpan ConnectionCheckInterval { get; }

        /// <summary>
        /// 待发消息的持久化存储，为null表示不持久化
        /// </summary>
        IManagedMqttClientStorage Storage { get; }

        /// <summary>
        /// 待发队列允许的最大消息数
        /// </summary>
        int MaxPendingMessages { get; }

        /// <summary>
        /// 待发队列达到上限时的溢出处理策略
        /// </summary>
        MqttPendingMessagesOverflowStrategy PendingMessagesOverflowStrategy { get; }
    }
}