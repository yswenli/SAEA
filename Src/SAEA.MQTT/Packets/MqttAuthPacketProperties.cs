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
*文件名： MqttAuthPacketProperties
*版本号： v26.4.23.1
*唯一标识：64c04398-7163-4d9f-8028-e3c8205ae8b0
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttAuthPacketProperties接口
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
    /// AUTH 报文的属性集合
    /// </summary>
    public sealed class MqttAuthPacketProperties
    {
        /// <summary>
        /// 认证方法名，握手期间必须保持一致
        /// </summary>
        public string AuthenticationMethod { get; set; }

        /// <summary>
        /// 认证数据，内容由认证方法定义
        /// </summary>
        public byte[] AuthenticationData { get; set; }

        /// <summary>
        /// 可读的原因描述
        /// </summary>
        public string ReasonString { get; set; }

        /// <summary>
        /// 用户自定义属性
        /// </summary>
        public List<MqttUserProperty> UserProperties { get; set; }
    }
}
