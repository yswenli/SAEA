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
*文件名： IMqttServerStorage
*版本号： v26.4.23.1
*唯一标识：34570f4a-99fa-476c-8b6b-70db2304e221
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttServerStorage存储类
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

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 保留消息的持久化存储契约，仅保存消息本身，不含写入与到期时间
    /// </summary>
    public interface IMqttServerStorage
    {
        /// <summary>
        /// 全量保存当前保留消息，调用方保证列表为完整快照
        /// </summary>
        /// <param name="messages">保留消息列表</param>
        /// <returns>保存完成的任务</returns>
        Task SaveRetainedMessagesAsync(IList<MqttApplicationMessage> messages);

        /// <summary>
        /// 加载上次保存的保留消息，返回 null 或空列表表示没有可恢复的数据
        /// </summary>
        /// <returns>保留消息列表</returns>
        Task<IList<MqttApplicationMessage>> LoadRetainedMessagesAsync();
    }
}
