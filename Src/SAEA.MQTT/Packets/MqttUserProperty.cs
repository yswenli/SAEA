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
*文件名： MqttUserProperty
*版本号： v26.4.23.1
*唯一标识：59a542a7-f6da-495f-aed8-3ee08072a485
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttUserProperty类
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

namespace SAEA.MQTT.Packets
{
    /// <summary>
    /// 用户自定义属性，以名称/值对形式随报文传输，MQTT 5.0 新增
    /// </summary>
    public sealed class MqttUserProperty
    {
        /// <summary>
        /// 以名称与值构造用户属性
        /// </summary>
        /// <param name="name">属性名称，不能为 null</param>
        /// <param name="value">属性值，不能为 null</param>
        public MqttUserProperty(string name, string value)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// 属性名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 属性值
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// 返回名称与值的哈希值异或结果
        /// </summary>
        /// <returns>哈希值</returns>
        public override int GetHashCode()
        {
            return Name.GetHashCode() ^ Value.GetHashCode();
        }

        /// <summary>
        /// 与指定对象比较是否相等
        /// </summary>
        /// <param name="other">待比较对象，非 MqttUserProperty 时返回 false</param>
        /// <returns>名称与值均按序相等时为 true</returns>
        public override bool Equals(object other)
        {
            return Equals(other as MqttUserProperty);
        }

        /// <summary>
        /// 与另一个用户属性比较是否相等
        /// </summary>
        /// <param name="other">待比较的用户属性，null 时返回 false</param>
        /// <returns>名称与值均按序相等时为 true</returns>
        public bool Equals(MqttUserProperty other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(other, this))
            {
                return true;
            }

            return string.Equals(Name, other.Name, StringComparison.Ordinal) &&
                string.Equals(Value, other.Value, StringComparison.Ordinal);
        }
    }
}
