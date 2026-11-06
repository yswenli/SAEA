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
*命名空间：SAEA.MQTT.Client.Receiving
*文件名： MqttApplicationMessageReceivedHandlerDelegate
*版本号： v26.4.23.1
*唯一标识：c18afd78-63cf-4e98-86bf-591ed697acdc
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttApplicationMessageReceivedHandlerDelegate接口
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

namespace SAEA.MQTT.Client.Receiving
{
    /// <summary>
    /// 用委托适配 IMqttApplicationMessageReceivedHandler 的处理器
    /// </summary>
    public class MqttApplicationMessageReceivedHandlerDelegate : IMqttApplicationMessageReceivedHandler
    {
        private readonly Func<MqttApplicationMessageReceivedEventArgs, Task> _handler;

        /// <summary>
        /// 用同步委托构造处理器，委托执行完后返回已完成的任务
        /// </summary>
        /// <param name="handler">同步处理委托，不能为 null</param>
        public MqttApplicationMessageReceivedHandlerDelegate(Action<MqttApplicationMessageReceivedEventArgs> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            _handler = context =>
            {
                handler(context);
                return Task.FromResult(0);
            };
        }

        /// <summary>
        /// 用异步委托构造处理器
        /// </summary>
        /// <param name="handler">异步处理委托，不能为 null</param>
        public MqttApplicationMessageReceivedHandlerDelegate(Func<MqttApplicationMessageReceivedEventArgs, Task> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>
        /// 调用被包装的委托处理收到的应用消息
        /// </summary>
        /// <param name="context">收到的消息与来源客户端标识</param>
        /// <returns>委托返回的任务</returns>
        public Task HandleApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs context)
        {
            return _handler(context);
        }
    }
}
