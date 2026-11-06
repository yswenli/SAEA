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
*文件名： MqttClientMessageQueueInterceptorDelegate
*版本号： v26.4.23.1
*唯一标识：1aed6aff-b781-496c-b3b6-dcd71e29c71b
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientMessageQueueInterceptorDelegate接口
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
    /// 以同步或异步委托实现的客户端消息队列入队拦截器
    /// </summary>
    public class MqttClientMessageQueueInterceptorDelegate : IMqttServerClientMessageQueueInterceptor
    {
        private readonly Func<MqttClientMessageQueueInterceptorContext, Task> _callback;

        /// <summary>
        /// 用同步委托创建拦截器，委托执行完毕后立即完成
        /// </summary>
        /// <param name="callback">同步拦截逻辑，不可为 null</param>
        public MqttClientMessageQueueInterceptorDelegate(Action<MqttClientMessageQueueInterceptorContext> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));

            _callback = context =>
            {
                callback(context);
                return Task.FromResult(0);
            };
        }

        /// <summary>
        /// 用异步委托创建拦截器
        /// </summary>
        /// <param name="callback">异步拦截逻辑，不可为 null</param>
        public MqttClientMessageQueueInterceptorDelegate(Func<MqttClientMessageQueueInterceptorContext, Task> callback)
        {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        /// <summary>
        /// 调用委托执行入队拦截
        /// </summary>
        /// <param name="context">拦截上下文</param>
        /// <returns>委托返回的任务</returns>
        public Task InterceptClientMessageQueueEnqueueAsync(MqttClientMessageQueueInterceptorContext context)
        {
            return _callback(context);
        }
    }
}