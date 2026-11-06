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
*文件名： MqttProtocolViolationException
*版本号： v26.4.23.1
*唯一标识：ea0c3528-e71c-4d1b-847c-7db44dfb9bac
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/01/16 15:31:19
*描述：MqttProtocolViolationException接口
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
    /// MQTT协议违规异常，报文不符合协议规范时抛出
    /// </summary>
    public class MqttProtocolViolationException : Exception
    {
        /// <summary>
        /// 使用指定消息初始化实例
        /// </summary>
        /// <param name="message">异常消息</param>
        public MqttProtocolViolationException(string message)
            : base(message)
        {
        }
    }
}
