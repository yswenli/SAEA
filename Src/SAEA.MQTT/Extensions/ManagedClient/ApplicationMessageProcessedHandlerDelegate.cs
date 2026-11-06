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
*文件名： ApplicationMessageProcessedHandlerDelegate
*版本号： v26.4.23.1
*唯一标识：9d3feed5-d994-4273-b7af-ca8c4e651133
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：ApplicationMessageProcessedHandlerDelegate接口
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
using System.Threading.Tasks;

namespace SAEA.MQTT.Extensions.ManagedClient
{
    /// <summary>
    /// 将同步或异步回调包装为应用消息处理完成处理器
    /// </summary>
    public class ApplicationMessageProcessedHandlerDelegate : IApplicationMessageProcessedHandler
    {
        private readonly Func<ApplicationMessageProcessedEventArgs, Task> _handler;

        /// <summary>
        /// 用同步回调初始化实例
        /// </summary>
        /// <param name="handler">同步处理回调，不能为null；执行完立即返回已完成任务</param>
        public ApplicationMessageProcessedHandlerDelegate(Action<ApplicationMessageProcessedEventArgs> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            _handler = context =>
            {
                handler(context);
                return Task.FromResult(0);
            };
        }

        /// <summary>
        /// 用异步回调初始化实例
        /// </summary>
        /// <param name="handler">异步处理回调，不能为null</param>
        public ApplicationMessageProcessedHandlerDelegate(Func<ApplicationMessageProcessedEventArgs, Task> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>
        /// 触发应用消息处理完成处理
        /// </summary>
        /// <param name="eventArgs">处理结果事件参数</param>
        /// <returns>回调执行完成的异步任务</returns>
        public Task HandleApplicationMessageProcessedAsync(ApplicationMessageProcessedEventArgs eventArgs)
        {
            return _handler(eventArgs);
        }
    }
}
