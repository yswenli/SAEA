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
*命名空间：SAEA.MQTT
*文件名： MqttApplicationMessageReceivedEventArgs
*版本号： v26.4.23.1
*唯一标识：410bcc4f-5807-4406-af98-60dc80516cee
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttApplicationMessageReceivedEventArgs接口
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

namespace SAEA.MQTT
{
    /// <summary>
    /// 应用消息接收事件参数，承载消息来源客户端与消息本体
    /// </summary>
    public class MqttApplicationMessageReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// 创建应用消息接收事件参数
        /// </summary>
        /// <param name="clientId">来源客户端标识，客户端侧为本地配置的标识</param>
        /// <param name="applicationMessage">收到的应用消息，不能为null</param>
        public MqttApplicationMessageReceivedEventArgs(string clientId, MqttApplicationMessage applicationMessage)
        {
            ClientId = clientId;
            ApplicationMessage = applicationMessage ?? throw new ArgumentNullException(nameof(applicationMessage));
        }

        /// <summary>
        /// 消息来源客户端的标识
        /// </summary>
        public string ClientId { get; }

        /// <summary>
        /// 收到的应用消息
        /// </summary>
        public MqttApplicationMessage ApplicationMessage { get; }

        /// <summary>
        /// 处理器是否处理失败，QoS 1/2 下置为 true 时客户端不回送 PUBACK/PUBREC
        /// </summary>
        public bool ProcessingFailed { get; set; }
    }
}
