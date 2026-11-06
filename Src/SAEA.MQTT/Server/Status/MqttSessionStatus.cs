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
*命名空间：SAEA.MQTT.Server.Status
*文件名： MqttSessionStatus
*版本号： v26.4.23.1
*唯一标识：b89695cf-f152-4326-abd9-d3ddec30dbd0
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttSessionStatus接口
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
using System.Threading.Tasks;

namespace SAEA.MQTT.Server.Status
{
    /// <summary>
    /// 客户端会话状态快照，删除与清空待发消息的操作委托给会话管理器
    /// </summary>
    public sealed class MqttSessionStatus : IMqttSessionStatus
    {
        readonly MqttClientSession _session;
        readonly MqttClientSessionsManager _sessionsManager;

        /// <summary>
        /// 创建会话状态对象
        /// </summary>
        /// <param name="session">被包装的会话，不能为 null，清空待发消息时直接操作它</param>
        /// <param name="sessionsManager">会话管理器，不能为 null，删除会话时使用</param>
        public MqttSessionStatus(MqttClientSession session, MqttClientSessionsManager sessionsManager)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _sessionsManager = sessionsManager ?? throw new ArgumentNullException(nameof(sessionsManager));
        }

        /// <summary>
        /// 会话所属的客户端标识
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// 会话中尚未确认投递的待发消息条数
        /// </summary>
        public long PendingApplicationMessagesCount { get; set; }

        /// <summary>
        /// 会话创建的时间
        /// </summary>
        public DateTime CreatedTimestamp { get; set; }

        /// <summary>
        /// 会话级自定义数据项，与真实会话共用同一字典实例
        /// </summary>
        public IDictionary<object, object> Items { get; set; }

        /// <summary>
        /// 按当前客户端标识删除该会话及其持久化数据
        /// </summary>
        /// <returns>删除完成的任务</returns>
        public Task DeleteAsync()
        {
            return _sessionsManager.DeleteSessionAsync(ClientId);
        }

        /// <summary>
        /// 清空会话的待发消息队列
        /// </summary>
        /// <returns>已完成的任务</returns>
        public Task ClearPendingApplicationMessagesAsync()
        {
            _session.ApplicationMessagesQueue.Clear();
            return Task.FromResult(0);
        }
    }
}
