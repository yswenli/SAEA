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
*文件名： ISynchronizingSubscriptionsFailedHandler
*版本号： v26.4.23.1
*唯一标识：8214f892-b56d-4039-b9ba-748f1509319c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：ISynchronizingSubscriptionsFailedHandler接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System.Threading.Tasks;

namespace SAEA.MQTT.Extensions.ManagedClient
{
    /// <summary>
    /// 订阅同步失败处理器
    /// </summary>
    public interface ISynchronizingSubscriptionsFailedHandler
    {
        /// <summary>
        /// 处理订阅同步失败事件
        /// </summary>
        /// <param name="eventArgs">订阅同步失败的事件参数</param>
        /// <returns>处理完成的异步任务</returns>
        Task HandleSynchronizingSubscriptionsFailedAsync(ManagedProcessFailedEventArgs eventArgs);
    }
}
