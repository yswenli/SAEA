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
*文件名： MqttServerClientUnsubscribedTopicHandlerDelegate
*版本号： v26.4.23.1
*唯一标识：575b2be1-9a75-449a-a06e-204e81d9c733
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttServerClientUnsubscribedTopicHandlerDelegate接口
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

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 把同步或异步回调适配为客户端取消订阅事件处理器
    /// </summary>
    public class MqttServerClientUnsubscribedTopicHandlerDelegate : IMqttServerClientUnsubscribedTopicHandler
    {
        private readonly Func<MqttServerClientUnsubscribedTopicEventArgs, Task> _handler;

        /// <summary>
        /// 用同步回调创建处理器，回调返回后视为已完成
        /// </summary>
        /// <param name="handler">取消订阅回调，不能为 null</param>
        public MqttServerClientUnsubscribedTopicHandlerDelegate(Action<MqttServerClientUnsubscribedTopicEventArgs> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            _handler = eventArgs =>
            {
                handler(eventArgs);
                return Task.FromResult(0);
            };
        }

        /// <summary>
        /// 用异步回调创建处理器
        /// </summary>
        /// <param name="handler">取消订阅回调，不能为 null</param>
        public MqttServerClientUnsubscribedTopicHandlerDelegate(Func<MqttServerClientUnsubscribedTopicEventArgs, Task> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>
        /// 调用注入的回调通知客户端已取消订阅
        /// </summary>
        /// <param name="eventArgs">取消订阅事件数据</param>
        /// <returns>回调返回的任务</returns>
        public Task HandleClientUnsubscribedTopicAsync(MqttServerClientUnsubscribedTopicEventArgs eventArgs)
        {
            return _handler(eventArgs);
        }
    }
}
