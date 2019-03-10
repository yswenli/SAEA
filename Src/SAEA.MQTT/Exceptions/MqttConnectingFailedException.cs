/****************************************************************************
*项目名称：SAEA.MQTT.Exceptions
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Exceptions
*类 名 称：MqttConnectingFailedException
*版 本 号： v4.2.1.6
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 10:57:38
*描述：
*=====================================================================
*修改时间：2019/1/15 10:57:38
*修 改 人： yswenli
*版 本 号： v4.2.1.6
*描    述：
*****************************************************************************/
using SAEA.MQTT.Core.Protocol;

namespace SAEA.MQTT.Exceptions
{
    public class MqttConnectingFailedException : MqttCommunicationException
    {
        public MqttConnectingFailedException(MqttConnectReturnCode returnCode)
            : base($"Connecting with MQTT server failed ({returnCode}).")
        {
            ReturnCode = returnCode;
        }

        public MqttConnectReturnCode ReturnCode { get; }
    }
}
