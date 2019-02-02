/****************************************************************************
*项目名称：SAEA.MQTT.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Model
*类 名 称：MqttClientOptions
*版 本 号： V4.1.2.2
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/14 19:13:31
*描述：
*=====================================================================
*修改时间：2019/1/14 19:13:31
*修 改 人： yswenli
*版 本 号： V4.1.2.2
*描    述：
*****************************************************************************/
using SAEA.MQTT.Core.Protocol;
using SAEA.MQTT.Interface;
using System;

namespace SAEA.MQTT.Model
{
    public class MqttClientOptions : IMqttClientOptions
    {
        public string ClientId { get; set; } = Guid.NewGuid().ToString("N");
        public bool CleanSession { get; set; } = true;
        public IMqttClientCredentials Credentials { get; set; } = new MqttClientCredentials();
        public MqttProtocolVersion ProtocolVersion { get; set; } = MqttProtocolVersion.V311;
        public IMqttClientChannelOptions ChannelOptions { get; set; }

        public TimeSpan CommunicationTimeout { get; set; } = TimeSpan.FromSeconds(10);
        public TimeSpan KeepAlivePeriod { get; set; } = TimeSpan.FromDays(365);
        public TimeSpan? KeepAliveSendInterval { get; set; } = TimeSpan.FromSeconds(15);

        public MqttMessage WillMessage { get; set; }
    }
}
