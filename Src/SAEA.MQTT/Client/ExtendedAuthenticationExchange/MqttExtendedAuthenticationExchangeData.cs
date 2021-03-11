using System.Collections.Generic;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Client.ExtendedAuthenticationExchange
{
    public class MqttExtendedAuthenticationExchangeData
    {
        public MqttAuthenticateReasonCode ReasonCode { get; set; }

        public string ReasonString { get; set; }

        public byte[] AuthenticationData { get; set; }

        public List<MqttUserProperty> UserProperties { get; }
    }
}
