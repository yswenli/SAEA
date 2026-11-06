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
*文件名： MqttDisconnectPacketProperties
*版本号： v26.4.23.1
*唯一标识：7829ccae-7c4f-4b7d-bea9-5bffafed4c28
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttDisconnectPacketProperties接口
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
    /// DISCONNECT 报文的属性集合，均为 MQTT 5.0 新增
    /// </summary>
    public sealed class MqttDisconnectPacketProperties
    {
        /// <summary>
        /// 会话过期时间，单位秒，客户端断开发送时可覆盖 CONNECT 中的取值
        /// </summary>
        public uint? SessionExpiryInterval { get; set; }

        /// <summary>
        /// 可读的原因描述
        /// </summary>
        public string ReasonString { get; set; }

        /// <summary>
        /// 用户自定义属性
        /// </summary>
        public List<MqttUserProperty> UserProperties { get; set; }

        /// <summary>
        /// 服务端建议客户端改用或重连的服务器地址
        /// </summary>
        public string ServerReference { get; set; }
    }
}