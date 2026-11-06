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
*文件名： MqttEnqueuedApplicationMessage
*版本号： v26.4.23.1
*唯一标识：5f178a8f-3d32-456e-bcdf-976a64127e04
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttEnqueuedApplicationMessage接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 包裹应用消息与其发布方连接的待处理消息项，供服务端分发队列使用
    /// </summary>
    public class MqttEnqueuedApplicationMessage
    {
        /// <summary>
        /// 创建待处理消息项
        /// </summary>
        /// <param name="applicationMessage">应用消息</param>
        /// <param name="sender">发布方连接，服务端自身发布时为 null</param>
        public MqttEnqueuedApplicationMessage(MqttApplicationMessage applicationMessage, MqttClientConnection sender)
        {
            Sender = sender;
            ApplicationMessage = applicationMessage;
        }

        /// <summary>
        /// 发布方连接，服务端自身发布时为 null
        /// </summary>
        public MqttClientConnection Sender { get; }

        /// <summary>
        /// 待分发的应用消息
        /// </summary>
        public MqttApplicationMessage ApplicationMessage { get; }
    }
}