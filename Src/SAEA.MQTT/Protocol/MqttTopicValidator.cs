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
*文件名： MqttTopicValidator
*版本号： v26.4.23.1
*唯一标识：ead5fe94-79f5-4349-ae68-9081241d38c2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttTopicValidator接口
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MqttTopicValidator接口
*
*****************************************************************************/
using System;
using SAEA.MQTT.Exceptions;

namespace SAEA.MQTT.Protocol
{
    public static class MqttTopicValidator
    {
        public static void ThrowIfInvalid(MqttApplicationMessage applicationMessage)
        {
            if (applicationMessage == null) throw new ArgumentNullException(nameof(applicationMessage));

            if (!applicationMessage.TopicAlias.HasValue)
            {
                ThrowIfInvalid(applicationMessage.Topic);
            }
            else
            {
                if (applicationMessage.TopicAlias.Value == 0)
                {
                    throw new MqttProtocolViolationException("The topic alias cannot be 0.");
                }
            }
        }

        public static void ThrowIfInvalid(string topic)
        {
            if (string.IsNullOrEmpty(topic))
            {
                throw new MqttProtocolViolationException("Topic should not be empty.");
            }

            foreach(var @char in topic)
            {
                if (@char == '+')
                {
                    throw new MqttProtocolViolationException("The character '+' is not allowed in topics.");
                }

                if (@char == '#')
                {
                    throw new MqttProtocolViolationException("The character '#' is not allowed in topics.");
                }
            }
        }
    }
}
