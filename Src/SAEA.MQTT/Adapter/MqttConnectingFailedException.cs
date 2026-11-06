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
*唯一标识：282606e7-8330-44af-8bb0-1150c7415bf3
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttConnectingFailedException接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.MQTT.Client.Connecting;
using SAEA.MQTT.Exceptions;

namespace SAEA.MQTT.Adapter
{
    /// <summary>
    /// CONNECT 被服务端拒绝时抛出的异常，携带 CONNACK 的完整认证结果
    /// </summary>
    public class MqttConnectingFailedException : MqttCommunicationException
    {
        /// <summary>
        /// 以 CONNACK 认证结果构造异常
        /// </summary>
        /// <param name="result">服务端返回的认证结果</param>
        public MqttConnectingFailedException(MqttClientAuthenticateResult result)
            : base($"Connecting with MQTT server failed ({result.ResultCode.ToString()}).")
        {
            Result = result;
        }

        /// <summary>
        /// 服务端返回的认证结果
        /// </summary>
        public MqttClientAuthenticateResult Result { get; }
        /// <summary>
        /// 认证结果的返回码
        /// </summary>
        public MqttClientConnectResultCode ResultCode => Result.ResultCode;
    }
}
