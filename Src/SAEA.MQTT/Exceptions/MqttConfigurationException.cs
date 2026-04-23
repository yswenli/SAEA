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
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MqttConfigurationException接口
*
*****************************************************************************/
using System;

namespace SAEA.MQTT.Exceptions
{
    public class MqttConfigurationException : Exception
    {
        protected MqttConfigurationException()
        {
        }

        public MqttConfigurationException(Exception innerException)
            : base(innerException.Message, innerException)
        {
        }

        public MqttConfigurationException(string message)
            : base(message)
        {
        }
    }
}
