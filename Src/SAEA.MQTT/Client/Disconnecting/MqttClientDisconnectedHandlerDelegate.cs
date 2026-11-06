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
*命名空间：SAEA.MQTT.Client.Disconnecting
*文件名： MqttClientDisconnectedHandlerDelegate
*版本号： v26.4.23.1
*唯一标识：36b2bec6-e8e4-4b18-80a3-4ece72e32a37
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientDisconnectedHandlerDelegate接口
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

namespace SAEA.MQTT.Client.Disconnecting
{
    /// <summary>
    /// 将同步或异步委托适配为 <see cref="IMqttClientDisconnectedHandler"/>
    /// </summary>
    public class MqttClientDisconnectedHandlerDelegate : IMqttClientDisconnectedHandler
    {
        private readonly Func<MqttClientDisconnectedEventArgs, Task> _handler;

        /// <summary>
        /// 用同步委托创建处理器，回调返回后即视为完成
        /// </summary>
        /// <param name="handler">断开连接时执行的同步委托，不能为 null</param>
        public MqttClientDisconnectedHandlerDelegate(Action<MqttClientDisconnectedEventArgs> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            _handler = context =>
            {
                handler(context);
                return Task.FromResult(0);
            };
        }

        /// <summary>
        /// 用异步委托创建处理器
        /// </summary>
        /// <param name="handler">断开连接时执行的异步委托，不能为 null</param>
        public MqttClientDisconnectedHandlerDelegate(Func<MqttClientDisconnectedEventArgs, Task> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>
        /// 调用被包装的委托
        /// </summary>
        /// <param name="eventArgs">断开连接事件参数</param>
        /// <returns>被包装委托返回的任务</returns>
        public Task HandleDisconnectedAsync(MqttClientDisconnectedEventArgs eventArgs)
        {
            return _handler(eventArgs);
        }
    }
}
