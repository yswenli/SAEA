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
*唯一标识：256bb722-02f5-4991-bcbe-d710ab42c6c1
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/01/16 15:31:19
*描述：
*
*=====================================================================
*修改标记
*修改时间：2019/01/16 15:31:19
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;

namespace SAEA.MQTT.Exceptions
{
    public class MqttProtocolViolationException : Exception
    {
        public MqttProtocolViolationException(string message)
            : base(message)
        {
        }
    }
}
