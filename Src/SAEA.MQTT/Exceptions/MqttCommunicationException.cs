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
*文件名： MqttCommunicationException
*版本号： v26.4.23.1
*唯一标识：28e16ffa-ef06-44f0-8397-68aa47da2254
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/01/16 15:31:19
*描述：MqttCommunicationException接口
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
    /// MQTT通信异常，连接与收发数据失败时抛出
    /// </summary>
    public class MqttCommunicationException : Exception
    {
        /// <summary>
        /// 供派生类使用，不指定异常消息
        /// </summary>
        protected MqttCommunicationException()
        {
        }

        /// <summary>
        /// 使用内部异常的消息初始化实例
        /// </summary>
        /// <param name="innerException">引起通信失败的内部异常</param>
        public MqttCommunicationException(Exception innerException)
            : base(innerException.Message, innerException)
        {
        }

        /// <summary>
        /// 使用指定消息初始化实例
        /// </summary>
        /// <param name="message">异常消息</param>
        public MqttCommunicationException(string message)
            : base(message)
        {
        }
    }
}
