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
*命名空间：SAEA.MQTT.Adapter
*文件名： MqttConnectingFailedException
*版本号： v26.4.23.1
*唯一标识：aa9d6a57-02ed-40e1-a2eb-394dcbe0c118
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MQTT通道适配器类
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MQTT通道适配器类
*
*****************************************************************************/
using SAEA.MQTT.Client.Connecting;
using SAEA.MQTT.Exceptions;

namespace SAEA.MQTT.Adapter
{
    public class MqttConnectingFailedException : MqttCommunicationException
    {
        public MqttConnectingFailedException(MqttClientAuthenticateResult result)
            : base($"Connecting with MQTT server failed ({result.ResultCode.ToString()}).")
        {
            Result = result;
        }

        public MqttClientAuthenticateResult Result { get; }
        public MqttClientConnectResultCode ResultCode => Result.ResultCode;
    }
}
