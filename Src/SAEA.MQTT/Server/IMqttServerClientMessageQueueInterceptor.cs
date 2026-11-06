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
*文件名： IMqttServerClientMessageQueueInterceptor
*版本号： v26.4.23.1
*唯一标识：40fb3e34-012d-44eb-b61c-e31bb80df956
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttServerClientMessageQueueInterceptor接口
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
    /// 客户端消息队列入队拦截器，可逐条决定是否把消息投递给指定接收方
    /// </summary>
    public interface IMqttServerClientMessageQueueInterceptor
    {
        /// <summary>
        /// 拦截消息入队，通过上下文的 AcceptEnqueue 决定是否放入接收方队列
        /// </summary>
        /// <param name="context">拦截上下文，包含收发双方标识、应用消息与授予的订阅 QoS</param>
        /// <returns>拦截完成的任务</returns>
        Task InterceptClientMessageQueueEnqueueAsync(MqttClientMessageQueueInterceptorContext context);
    }
}
