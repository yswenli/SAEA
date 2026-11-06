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
*文件名： MqttCommunicationTimedOutException
*版本号： v26.4.23.1
*唯一标识：c963b127-e8e3-4ee7-9cf2-3305f058675a
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/01/16 15:31:19
*描述：MqttCommunicationTimedOutException接口
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
    /// MQTT通信超时异常，等待报文超过允许时限时抛出
    /// </summary>
    public class MqttCommunicationTimedOutException : MqttCommunicationException
    {
        /// <summary>
        /// 初始化实例，不指定额外信息
        /// </summary>
        public MqttCommunicationTimedOutException()
        {
        }

        /// <summary>
        /// 保留引起超时的内部异常
        /// </summary>
        /// <param name="innerException">引起超时的内部异常</param>
        public MqttCommunicationTimedOutException(Exception innerException) : base(innerException)
        {
        }
    }
}
