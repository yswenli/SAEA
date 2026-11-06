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
*文件名： MqttApplicationMessageInterceptorContext
*版本号： v26.4.23.1
*唯一标识：76f3e1cd-b258-4273-93da-ae6594d66870
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttApplicationMessageInterceptorContext接口
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
    /// 应用消息发布拦截的上下文，承载发布方标识、待发布消息与会话级共享数据
    /// </summary>
    public class MqttApplicationMessageInterceptorContext
    {
        /// <summary>
        /// 创建拦截上下文
        /// </summary>
        /// <param name="clientId">发布消息的客户端标识，服务端自身发布时为服务端 ClientId</param>
        /// <param name="sessionItems">发布方会话的键值集合，可为 null</param>
        /// <param name="applicationMessage">待发布的应用消息</param>
        public MqttApplicationMessageInterceptorContext(string clientId, IDictionary<object, object> sessionItems, MqttApplicationMessage applicationMessage)
        {
            ClientId = clientId;
            ApplicationMessage = applicationMessage;
            SessionItems = sessionItems;
        }

        /// <summary>
        /// 发布消息的客户端标识
        /// </summary>
        public string ClientId { get; }

        /// <summary>
        /// 待发布的应用消息，拦截器中可替换或修改其内容
        /// </summary>
        public MqttApplicationMessage ApplicationMessage { get; set; }

        /// <summary>
        /// 发布方会话的键值集合，用于在会话范围内共享数据
        /// </summary>
        public IDictionary<object, object> SessionItems { get; }

        /// <summary>
        /// 是否继续分发该消息，默认 true；置为 false 时仅丢弃本次发布
        /// </summary>
        public bool AcceptPublish { get; set; } = true;

        /// <summary>
        /// 是否在拦截后关闭发布方连接，默认 false
        /// </summary>
        public bool CloseConnection { get; set; }
    }
}
