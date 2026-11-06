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
*文件名： IMqttServer
*版本号： v26.4.23.1
*唯一标识：150c66f7-32b9-4f1b-8868-9def26235545
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttServer服务端类
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
using SAEA.MQTT.Server.Status;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// MQTT 服务端对外接口，提供启动停止、状态查询、保留消息管理及向指定客户端注入订阅的能力
    /// </summary>
    public interface IMqttServer : IApplicationMessageReceiver, IApplicationMessagePublisher, IDisposable
    {
        /// <summary>
        /// 服务端是否已启动
        /// </summary>
        bool IsStarted { get; }
        /// <summary>
        /// 服务端启动完成后的回调处理器，为 null 表示不回调
        /// </summary>
        IMqttServerStartedHandler StartedHandler { get; set; }
        /// <summary>
        /// 服务端停止完成后的回调处理器，为 null 表示不回调
        /// </summary>
        IMqttServerStoppedHandler StoppedHandler { get; set; }

        /// <summary>
        /// 客户端连接成功后的回调处理器，为 null 表示不回调
        /// </summary>
        IMqttServerClientConnectedHandler ClientConnectedHandler { get; set; }
        /// <summary>
        /// 客户端断开连接后的回调处理器，为 null 表示不回调
        /// </summary>
        IMqttServerClientDisconnectedHandler ClientDisconnectedHandler { get; set; }
        /// <summary>
        /// 客户端订阅主题成功后的回调处理器，为 null 表示不回调
        /// </summary>
        IMqttServerClientSubscribedTopicHandler ClientSubscribedTopicHandler { get; set; }
        /// <summary>
        /// 客户端取消订阅成功后的回调处理器，为 null 表示不回调
        /// </summary>
        IMqttServerClientUnsubscribedTopicHandler ClientUnsubscribedTopicHandler { get; set; }

        /// <summary>
        /// 当前生效的服务端配置
        /// </summary>
        IMqttServerOptions Options { get; }

        /// <summary>
        /// 获取所有已连接客户端的状态快照
        /// </summary>
        /// <returns>客户端状态列表，无连接时为空列表</returns>
        Task<IList<IMqttClientStatus>> GetClientStatusAsync();
        /// <summary>
        /// 获取所有会话的状态快照，包含已断开但未过期的持久会话
        /// </summary>
        /// <returns>会话状态列表，无会话时为空列表</returns>
        Task<IList<IMqttSessionStatus>> GetSessionStatusAsync();

        /// <summary>
        /// 获取当前全部保留消息
        /// </summary>
        /// <returns>保留消息列表，无保留消息时为空列表</returns>
        Task<IList<MqttApplicationMessage>> GetRetainedApplicationMessagesAsync();
        /// <summary>
        /// 清空全部保留消息并同步到存储
        /// </summary>
        /// <returns>清空完成的任务</returns>
        Task ClearRetainedApplicationMessagesAsync();

        /// <summary>
        /// 以服务端身份为指定客户端添加订阅，仍会经过订阅拦截器；服务端未启动或客户端不存在时抛出异常
        /// </summary>
        /// <param name="clientId">目标客户端标识</param>
        /// <param name="topicFilters">要添加的订阅过滤器集合</param>
        /// <returns>订阅完成的任务，含订阅后 retained 补发</returns>
        Task SubscribeAsync(string clientId, ICollection<MqttTopicFilter> topicFilters);
        /// <summary>
        /// 以服务端身份移除指定客户端的订阅，仍会经过取消订阅拦截器
        /// </summary>
        /// <param name="clientId">目标客户端标识</param>
        /// <param name="topicFilters">要移除的订阅主题集合</param>
        /// <returns>取消订阅完成的任务</returns>
        Task UnsubscribeAsync(string clientId, ICollection<string> topicFilters);

        /// <summary>
        /// 使用指定配置启动服务端，重复启动会因状态校验失败而抛出异常
        /// </summary>
        /// <param name="options">服务端配置，包含端点、TLS、存储与会话策略</param>
        /// <returns>启动完成的任务，所有端点开始监听后结束</returns>
        Task StartAsync(IMqttServerOptions options);
        /// <summary>
        /// 停止服务端并关闭所有客户端连接
        /// </summary>
        /// <returns>停止完成的任务</returns>
        Task StopAsync();
    }
}