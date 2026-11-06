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
*命名空间：SAEA.MQTT.Packets
*文件名： MqttConnectPacketProperties
*版本号： v26.4.23.1
*唯一标识：64ab3dc8-421f-476f-b1d1-8bc8bee4484d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttConnectPacketProperties接口
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

namespace SAEA.MQTT.Packets
{
    /// <summary>
    /// CONNECT 报文的属性集合，均为 MQTT 5.0 新增
    /// </summary>
    public sealed class MqttConnectPacketProperties
    {
        /// <summary>
        /// 断开连接后延迟多久发布遗嘱消息，单位秒
        /// </summary>
        public uint? WillDelayInterval { get; set; }

        /// <summary>
        /// 会话过期时间，单位秒，0 表示连接断开即销毁会话
        /// </summary>
        public uint? SessionExpiryInterval { get; set; }

        /// <summary>
        /// 认证方法名，用于增强认证
        /// </summary>
        public string AuthenticationMethod { get; set; }

        /// <summary>
        /// 认证数据，内容由认证方法定义
        /// </summary>
        public byte[] AuthenticationData { get; set; }

        /// <summary>
        /// 是否允许服务端在应答中返回原因描述与用户属性
        /// </summary>
        public bool? RequestProblemInformation { get; set; }

        /// <summary>
        /// 是否请求服务端在 CONNACK 中返回响应信息
        /// </summary>
        public bool? RequestResponseInformation { get; set; }

        /// <summary>
        /// 客户端允许同时处于未确认状态的 QoS 1/2 报文数量上限
        /// </summary>
        public ushort? ReceiveMaximum { get; set; }

        /// <summary>
        /// 客户端接受的入站主题别名数量上限
        /// </summary>
        public ushort? TopicAliasMaximum { get; set; }

        /// <summary>
        /// 客户端可接受的最大报文长度，单位字节
        /// </summary>
        public uint? MaximumPacketSize { get; set; }

        /// <summary>
        /// 用户自定义属性
        /// </summary>
        public List<MqttUserProperty> UserProperties { get; set; }
    }
}