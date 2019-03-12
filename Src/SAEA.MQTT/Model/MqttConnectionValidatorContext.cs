/****************************************************************************
*项目名称：SAEA.MQTT.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Model
*类 名 称：MqttConnectionValidatorContext
*版 本 号： v4.2.3.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 10:22:07
*描述：
*=====================================================================
*修改时间：2019/1/15 10:22:07
*修 改 人： yswenli
*版 本 号： v4.2.3.1
*描    述：
*****************************************************************************/
using SAEA.MQTT.Core.Protocol;

namespace SAEA.MQTT.Model
{
    public class MqttConnectionValidatorContext
    {
        public MqttConnectionValidatorContext(string clientId, string username, string password, MqttMessage willMessage, string endpoint)
        {
            ClientId = clientId;
            Username = username;
            Password = password;
            WillMessage = willMessage;
            Endpoint = endpoint;
        }

        public string ClientId { get; }

        public string Username { get; }

        public string Password { get; }

        public MqttMessage WillMessage { get; }

        public string Endpoint { get; }

        public MqttConnectReturnCode ReturnCode { get; set; } = MqttConnectReturnCode.ConnectionAccepted;
    }
}
