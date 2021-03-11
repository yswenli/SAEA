using System.Collections.Generic;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Server
{
    public sealed class MqttClientSubscribeResult
    {
        public List<MqttSubscribeReturnCode> ReturnCodes { get; } = new List<MqttSubscribeReturnCode>();

        public List<MqttSubscribeReasonCode> ReasonCodes { get; } = new List<MqttSubscribeReasonCode>();

        public bool CloseConnection { get; set; }
    }
}
