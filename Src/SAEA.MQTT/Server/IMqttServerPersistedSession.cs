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
*文件名： IMqttServerPersistedSession
*版本号： v26.4.23.1
*唯一标识：9d1086bf-8dcd-4524-9ac4-ac314b6c54ef
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttServerPersistedSession接口
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
using System.Collections.Generic;

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 可被持久化的服务端会话快照，供外部存储在服务端重启后还原会话状态
    /// </summary>
    public interface IMqttServerPersistedSession
    {
        /// <summary>
        /// 会话所属的客户端标识
        /// </summary>
        string ClientId { get; }

        /// <summary>
        /// 会话级键值集合，用于跨请求共享应用自定义数据
        /// </summary>
        IDictionary<object, object> Items { get; }

        /// <summary>
        /// 会话持有的订阅过滤器集合
        /// </summary>
        IList<MqttTopicFilter> Subscriptions { get; }

        /// <summary>
        /// 会话关联的遗嘱消息，未设置遗嘱时为 null
        /// </summary>
        MqttApplicationMessage WillMessage { get; }

        /// <summary>
        /// 遗嘱延迟发布的秒数，null 表示未设置并按 0 处理
        /// </summary>
        uint? WillDelayInterval { get; }

        /// <summary>
        /// 会话到期的绝对时间，null 表示会话不过期
        /// </summary>
        DateTime? SessionExpiryTimestamp { get; }

        /// <summary>
        /// 会话过期间隔秒数，0 表示连接断开后立即结束会话
        /// </summary>
        uint SessionExpiryInterval { get; }

        /// <summary>
        /// 客户端断开连接的时刻，null 表示当前仍处于连接状态
        /// </summary>
        DateTimeOffset? DisconnectedAt { get; }

        /// <summary>
        /// 尚未投递给客户端的排队消息，按入队顺序保存
        /// </summary>
        IList<MqttQueuedApplicationMessage> PendingApplicationMessages { get; }
    }
}
