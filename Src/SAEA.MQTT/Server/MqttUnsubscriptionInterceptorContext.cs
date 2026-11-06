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
*文件名： MqttUnsubscriptionInterceptorContext
*版本号： v26.4.23.1
*唯一标识：bbb8fbcf-7d9a-49fd-b7f7-733f9fd6b666
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttUnsubscriptionInterceptorContext接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System.Collections.Generic;

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 取消订阅拦截的上下文，回调可修改主题、拒绝取消订阅或直接关闭连接
    /// </summary>
    public class MqttUnsubscriptionInterceptorContext
    {
        /// <summary>
        /// 创建取消订阅拦截上下文
        /// </summary>
        /// <param name="clientId">发起取消订阅的客户端标识</param>
        /// <param name="topic">客户端请求取消订阅的主题过滤器</param>
        /// <param name="sessionItems">会话范围内共享数据的键值集合</param>
        public MqttUnsubscriptionInterceptorContext(string clientId, string topic, IDictionary<object, object> sessionItems)
        {
            ClientId = clientId;
            Topic = topic;
            SessionItems = sessionItems;
        }

        /// <summary>
        /// 发起取消订阅的客户端标识
        /// </summary>
        public string ClientId { get; }

        /// <summary>
        /// 客户端请求取消订阅的主题过滤器，可在此拦截器中改写
        /// </summary>
        public string Topic { get; set; }

        /// <summary>
        /// 会话范围内共享数据的键值集合
        /// </summary>
        public IDictionary<object, object> SessionItems { get; }

        /// <summary>
        /// 是否接受本次取消订阅，默认 true；置为 false 时服务端返回失败应答
        /// </summary>
        public bool AcceptUnsubscription { get; set; } = true;

        /// <summary>
        /// 是否在拦截后直接关闭连接，默认 false
        /// </summary>
        public bool CloseConnection { get; set; }
    }
}
