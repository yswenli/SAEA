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
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using SAEA.MQTT.Exceptions;

namespace SAEA.MQTT.Protocol
{
    /// <summary>
    /// 应用消息主题与主题过滤器的合法性校验
    /// </summary>
    public static class MqttTopicValidator
    {
        /// <summary>
        /// 校验应用消息的主题，已设置主题别名时改为校验别名不为 0
        /// </summary>
        /// <param name="applicationMessage">待校验的应用消息，不能为 null</param>
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

        /// <summary>
        /// 校验主题名非空且不含通配符 '+' 与 '#'，不合法时抛出 MqttProtocolViolationException
        /// </summary>
        /// <param name="topic">待校验的主题名</param>
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
