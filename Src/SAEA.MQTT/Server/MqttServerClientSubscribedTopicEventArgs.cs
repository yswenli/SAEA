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
*文件名： MqttServerClientSubscribedTopicEventArgs
*版本号： v26.4.23.1
*唯一标识：8fcc8873-53da-4947-ba9e-6d16b0443ed3
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttServerClientSubscribedTopicEventArgs接口
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
    /// 客户端订阅成功事件的数据，携带客户端标识与命中的主题过滤器
    /// </summary>
    public class MqttServerClientSubscribedTopicEventArgs : EventArgs
    {
        /// <summary>
        /// 创建订阅成功事件数据
        /// </summary>
        /// <param name="clientId">订阅方客户端标识，不能为 null</param>
        /// <param name="topicFilter">已建立的主题过滤器，不能为 null</param>
        public MqttServerClientSubscribedTopicEventArgs(string clientId, MqttTopicFilter topicFilter)
        {
            ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            TopicFilter = topicFilter ?? throw new ArgumentNullException(nameof(topicFilter));
        }

        /// <summary>
        /// 订阅方客户端标识
        /// </summary>
        public string ClientId { get; }

        /// <summary>
        /// 已建立的主题过滤器
        /// </summary>
        public MqttTopicFilter TopicFilter { get; }
    }
}
