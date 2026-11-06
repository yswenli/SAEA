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
*文件名： IMqttRetainedMessagesManager
*版本号： v26.4.23.1
*唯一标识：473c73d5-766e-44b2-a231-028b4b45ee20
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttRetainedMessagesManager接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.MQTT.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 保留消息管理器，按主题维护保留消息并负责与存储介质同步
    /// </summary>
    public interface IMqttRetainedMessagesManager
    {
        /// <summary>
        /// 绑定服务端配置与日志器，初始化管理器
        /// </summary>
        /// <param name="options">服务端配置，用于读取保留消息存储实现</param>
        /// <param name="logger">日志器，用于记录加载与保存异常</param>
        /// <returns>绑定的任务，当前实现同步完成</returns>
        Task Start(IMqttServerOptions options, IMqttNetLogger logger);

        /// <summary>
        /// 从已配置的存储加载保留消息，未配置存储时直接返回
        /// </summary>
        /// <returns>加载完成的任务</returns>
        Task LoadMessagesAsync();

        /// <summary>
        /// 清空内存中的保留消息并同步到存储
        /// </summary>
        /// <returns>清空完成的任务</returns>
        Task ClearMessagesAsync();

        /// <summary>
        /// 处理发布报文对保留消息的影响：载荷为空表示删除，否则覆盖；已过期的消息不存储
        /// </summary>
        /// <param name="clientId">发布消息的客户端标识，仅用于日志</param>
        /// <param name="applicationMessage">应用消息，其 Retain 标志已由调用方判定</param>
        /// <returns>处理与持久化完成的任务</returns>
        Task HandleMessageAsync(string clientId, MqttApplicationMessage applicationMessage);

        /// <summary>
        /// 获取全部未过期的保留消息
        /// </summary>
        /// <returns>保留消息列表，无消息时为空列表</returns>
        Task<IList<MqttApplicationMessage>> GetMessagesAsync();

        /// <summary>
        /// 获取与指定订阅过滤器集合匹配的保留消息
        /// </summary>
        /// <param name="topicFilters">订阅过滤器集合，任一过滤器命中即纳入结果</param>
        /// <returns>匹配的保留消息列表，同一主题只返回一次</returns>
        Task<IList<MqttApplicationMessage>> GetSubscribedMessagesAsync(ICollection<MqttTopicFilter> topicFilters);

        /// <summary>
        /// 获取与指定订阅过滤器集合匹配的保留消息条目，附带写入时间与到期时间
        /// </summary>
        /// <param name="topicFilters">订阅过滤器集合，任一过滤器命中即纳入结果</param>
        /// <returns>匹配的保留消息条目列表，同一主题只返回一次</returns>
        Task<IList<MqttRetainedMessageEntry>> GetSubscribedMessageEntriesAsync(ICollection<MqttTopicFilter> topicFilters);
    }
}
