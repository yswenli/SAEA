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
*文件名： IMqttSessionStatus
*版本号： v26.4.23.1
*唯一标识：de365ca8-df11-4621-a280-ae5086342214
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttSessionStatus接口
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

namespace SAEA.MQTT.Server.Status
{
    /// <summary>
    /// 单个客户端会话的状态快照及会话级操作
    /// </summary>
    public interface IMqttSessionStatus
    {
        /// <summary>
        /// 会话所属的客户端标识
        /// </summary>
        string ClientId { get; }

        /// <summary>
        /// 会话中尚未确认投递的待发消息条数
        /// </summary>
        long PendingApplicationMessagesCount { get; }

        /// <summary>
        /// 会话级自定义数据项，拦截器可借此在多次回调间共享数据
        /// </summary>
        IDictionary<object, object> Items { get; }

        /// <summary>
        /// 清空会话的待发消息队列
        /// </summary>
        /// <returns>清空完成的任务</returns>
        Task ClearPendingApplicationMessagesAsync();

        /// <summary>
        /// 删除该会话及其持久化数据
        /// </summary>
        /// <returns>删除完成的任务</returns>
        Task DeleteAsync();
    }
}