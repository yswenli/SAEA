/****************************************************************************
*项目名称：SAEA.MQTT.Event
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Event
*类 名 称：MqttClientDisconnectedEventArgs
*版 本 号： v4.2.1.6
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/14 19:56:10
*描述：
*=====================================================================
*修改时间：2019/1/14 19:56:10
*修 改 人： yswenli
*版 本 号： v4.2.1.6
*描    述：
*****************************************************************************/
using System;

namespace SAEA.MQTT.Event
{
    public class MqttClientDisconnectedEventArgs : EventArgs
    {
        public MqttClientDisconnectedEventArgs(bool clientWasConnected, Exception exception)
        {
            ClientWasConnected = clientWasConnected;
            Exception = exception;
        }

        public MqttClientDisconnectedEventArgs(string clientID, bool clientWasConnected, Exception exception = null)
        {
            ClientID = clientID;
            ClientWasConnected = clientWasConnected;
            Exception = exception;
        }

        public string ClientID { get; set; }

        public bool ClientWasConnected { get; }

        public Exception Exception { get; }
    }
}
