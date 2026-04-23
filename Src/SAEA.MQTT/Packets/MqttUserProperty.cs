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
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MqttUserProperty类
*
*****************************************************************************/
using System;

namespace SAEA.MQTT.Packets
{
    public sealed class MqttUserProperty
    {
        public MqttUserProperty(string name, string value)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public string Name { get; }

        public string Value { get; }

        public override int GetHashCode()
        {
            return Name.GetHashCode() ^ Value.GetHashCode();
        }

        public override bool Equals(object other)
        {
            return Equals(other as MqttUserProperty);
        }

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
