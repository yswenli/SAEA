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
*文件名： MqttServerClientDisconnectedHandlerDelegate
*版本号： v26.4.23.1
*唯一标识：242d4a0c-741b-4292-889f-ac11db7eeed2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttServerClientDisconnectedHandlerDelegate接口
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
    /// 把同步或异步回调适配为客户端断开连接事件处理器
    /// </summary>
    public class MqttServerClientDisconnectedHandlerDelegate : IMqttServerClientDisconnectedHandler
    {
        private readonly Func<MqttServerClientDisconnectedEventArgs, Task> _handler;

        /// <summary>
        /// 用同步回调创建处理器，回调返回后视为已完成
        /// </summary>
        /// <param name="handler">断开连接回调，不能为 null</param>
        public MqttServerClientDisconnectedHandlerDelegate(Action<MqttServerClientDisconnectedEventArgs> handler)
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
        /// <param name="handler">断开连接回调，不能为 null</param>
        public MqttServerClientDisconnectedHandlerDelegate(Func<MqttServerClientDisconnectedEventArgs, Task> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>
        /// 调用注入的回调通知客户端已断开
        /// </summary>
        /// <param name="eventArgs">断开连接事件数据</param>
        /// <returns>回调返回的任务</returns>
        public Task HandleClientDisconnectedAsync(MqttServerClientDisconnectedEventArgs eventArgs)
        {
            return _handler(eventArgs);
        }
    }
}
