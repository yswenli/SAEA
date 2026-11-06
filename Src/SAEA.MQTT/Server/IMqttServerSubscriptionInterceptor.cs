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
*文件名： IMqttServerSubscriptionInterceptor
*版本号： v26.4.23.1
*唯一标识：e62d45b2-65f2-4ba0-ad72-8e42b9dab58c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttServerSubscriptionInterceptor接口
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

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 订阅拦截器，可修改订阅过滤器或拒绝订阅请求
    /// </summary>
    public interface IMqttServerSubscriptionInterceptor
    {
        /// <summary>
        /// 拦截单个订阅过滤器，通过上下文的 AcceptSubscription 决定是否接受
        /// </summary>
        /// <param name="context">拦截上下文，包含客户端标识、订阅过滤器与会话级共享数据</param>
        /// <returns>拦截完成的任务</returns>
        Task InterceptSubscriptionAsync(MqttSubscriptionInterceptorContext context);
    }
}
