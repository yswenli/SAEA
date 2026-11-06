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
*文件名： IManagedMqttClientStorage
*版本号： v26.4.23.1
*唯一标识：417ef104-7be5-40d7-8c43-288dc4e03f6e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IManagedMqttClientStorage接口
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
using System.Threading.Tasks;

namespace SAEA.MQTT.Extensions.ManagedClient
{
    /// <summary>
    /// 托管客户端的待发消息持久化存储
    /// </summary>
    public interface IManagedMqttClientStorage
    {
        /// <summary>
        /// 保存当前排队中的待发消息
        /// </summary>
        /// <param name="messages">待发消息集合</param>
        /// <returns>保存完成的异步任务</returns>
        Task SaveQueuedMessagesAsync(IList<ManagedMqttApplicationMessage> messages);

        /// <summary>
        /// 读取已保存的待发消息
        /// </summary>
        /// <returns>已保存的待发消息集合，无数据时返回空集合</returns>
        Task<IList<ManagedMqttApplicationMessage>> LoadQueuedMessagesAsync();
    }
}
