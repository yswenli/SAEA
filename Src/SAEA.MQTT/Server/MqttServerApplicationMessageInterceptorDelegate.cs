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
*文件名： MqttServerApplicationMessageInterceptorDelegate
*版本号： v26.4.23.1
*唯一标识：7248cff3-cc77-42fb-8421-ac6f7f0c55a5
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttServerApplicationMessageInterceptorDelegate接口
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
    /// 把同步或异步回调适配为应用消息发布拦截器
    /// </summary>
    public class MqttServerApplicationMessageInterceptorDelegate : IMqttServerApplicationMessageInterceptor
    {
        private readonly Func<MqttApplicationMessageInterceptorContext, Task> _callback;

        /// <summary>
        /// 用同步回调创建拦截器，回调返回后视为已完成
        /// </summary>
        /// <param name="callback">发布拦截回调，不能为 null</param>
        public MqttServerApplicationMessageInterceptorDelegate(Action<MqttApplicationMessageInterceptorContext> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));

            _callback = context =>
            {
                callback(context);
                return Task.FromResult(0);
            };
        }

        /// <summary>
        /// 用异步回调创建拦截器
        /// </summary>
        /// <param name="callback">发布拦截回调，不能为 null</param>
        public MqttServerApplicationMessageInterceptorDelegate(Func<MqttApplicationMessageInterceptorContext, Task> callback)
        {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        /// <summary>
        /// 调用注入的回调执行发布拦截
        /// </summary>
        /// <param name="context">发布拦截上下文，回调可修改其中的消息内容</param>
        /// <returns>回调返回的任务</returns>
        public Task InterceptApplicationMessagePublishAsync(MqttApplicationMessageInterceptorContext context)
        {
            return _callback(context);
        }
    }
}
