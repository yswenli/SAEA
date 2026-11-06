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
*文件名： MqttControlPacketType
*版本号： v26.4.23.1
*唯一标识：5b6bce12-db5a-4158-810f-29013d6ee219
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttControlPacketType类型枚举
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
    /// MQTT 控制报文类型，取值对应固定报头首字节的高 4 位
    /// </summary>
    public enum MqttControlPacketType
    {
        /// <summary>
        /// 客户端请求建立连接
        /// </summary>
        Connect = 1,
        /// <summary>
        /// 服务端确认连接
        /// </summary>
        ConnAck = 2,
        /// <summary>
        /// 发布应用消息
        /// </summary>
        Publish = 3,
        /// <summary>
        /// QoS 1 发布消息的确认
        /// </summary>
        PubAck = 4,
        /// <summary>
        /// QoS 2 发布消息的接收响应
        /// </summary>
        PubRec = 5,
        /// <summary>
        /// QoS 2 发布消息的释放
        /// </summary>
        PubRel = 6,
        /// <summary>
        /// QoS 2 发布消息的释放完成
        /// </summary>
        PubComp = 7,
        /// <summary>
        /// 客户端订阅主题
        /// </summary>
        Subscribe = 8,
        /// <summary>
        /// 服务端确认订阅
        /// </summary>
        SubAck = 9,
        /// <summary>
        /// 客户端取消订阅
        /// </summary>
        Unsubscribe = 10,
        /// <summary>
        /// 服务端确认取消订阅
        /// </summary>
        UnsubAck = 11,
        /// <summary>
        /// 客户端心跳请求
        /// </summary>
        PingReq = 12,
        /// <summary>
        /// 服务端心跳响应
        /// </summary>
        PingResp = 13,
        /// <summary>
        /// 断开连接
        /// </summary>
        Disconnect = 14,
        /// <summary>
        /// MQTT 5.0 增强认证
        /// </summary>
        Auth = 15
    }
}
