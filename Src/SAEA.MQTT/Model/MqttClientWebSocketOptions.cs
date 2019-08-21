/****************************************************************************
*项目名称：SAEA.MQTT.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Model
*类 名 称：MqttClientWebSocketOptions
*版 本 号： v5.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/14 19:29:31
*描述：
*=====================================================================
*修改时间：2019/1/14 19:29:31
*修 改 人： yswenli
*版 本 号： v5.0.0.1
*描    述：
*****************************************************************************/
using SAEA.MQTT.Interface;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace SAEA.MQTT.Model
{
    public class MqttClientWebSocketOptions : IMqttClientChannelOptions
    {
        public string Uri { get; set; }

        public IDictionary<string, string> RequestHeaders { get; set; }

        public ICollection<string> SubProtocols { get; set; } = new List<string> { "mqtt" };

        public CookieContainer CookieContainer { get; set; }

        public MqttClientWebSocketProxyOptions ProxyOptions { get; set; }

        public MqttClientTlsOptions TlsOptions { get; set; } = new MqttClientTlsOptions();

        public override string ToString()
        {
            return Uri;
        }
    }
}
