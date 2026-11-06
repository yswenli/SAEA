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
*命名空间：SAEA.MQTT.Exceptions
*文件名： MqttConfigurationException
*版本号： v26.4.23.1
*唯一标识：4c4a86a5-05a8-4e3d-b1f9-a39efb58f856
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttConfigurationException接口
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

namespace SAEA.MQTT.Exceptions
{
    /// <summary>
    /// MQTT配置异常，参数校验失败时抛出
    /// </summary>
    public class MqttConfigurationException : Exception
    {
        /// <summary>
        /// 供派生类使用，不指定异常消息
        /// </summary>
        protected MqttConfigurationException()
        {
        }

        /// <summary>
        /// 使用内部异常的消息初始化实例
        /// </summary>
        /// <param name="innerException">引起配置失败的内部异常</param>
        public MqttConfigurationException(Exception innerException)
            : base(innerException.Message, innerException)
        {
        }

        /// <summary>
        /// 使用指定消息初始化实例
        /// </summary>
        /// <param name="message">异常消息</param>
        public MqttConfigurationException(string message)
            : base(message)
        {
        }
    }
}
