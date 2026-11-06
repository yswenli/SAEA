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
*命名空间：SAEA.MQTT.Protocol
*文件名： MqttRetainHandling
*版本号： v26.4.23.1
*唯一标识：4a9a5650-7c6a-4f99-96b2-5a8024310de4
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttRetainHandling接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.MQTT.Protocol
{
    /// <summary>
    /// MQTT 5.0 订阅时的保留消息发送策略
    /// </summary>
    public enum MqttRetainHandling
    {
        /// <summary>
        /// 每次成功订阅都发送保留消息
        /// </summary>
        SendAtSubscribe = 0,
        /// <summary>
        /// 仅当订阅此前不存在时才发送保留消息
        /// </summary>
        SendAtSubscribeIfNewSubscriptionOnly = 1,
        /// <summary>
        /// 订阅时不发送保留消息
        /// </summary>
        DoNotSendOnSubscribe = 2
    }
}
