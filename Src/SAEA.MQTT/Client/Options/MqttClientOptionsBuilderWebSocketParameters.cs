using System.Collections.Generic;
using System.Net;

namespace SAEA.MQTT.Client.Options
{
    public class MqttClientOptionsBuilderWebSocketParameters
    {
        public IDictionary<string, string> RequestHeaders { get; set; }

        public CookieContainer CookieContainer { get; set; }
    }
}
