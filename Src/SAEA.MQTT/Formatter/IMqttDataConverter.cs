using System.Collections.Generic;
using SAEA.MQTT.Client.Connecting;
using SAEA.MQTT.Client.Disconnecting;
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Client.Publishing;
using SAEA.MQTT.Client.Subscribing;
using SAEA.MQTT.Client.Unsubscribing;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Server;
using MqttClientSubscribeResult = SAEA.MQTT.Client.Subscribing.MqttClientSubscribeResult;

namespace SAEA.MQTT.Formatter
{
    public interface IMqttDataConverter
    {
        MqttPublishPacket CreatePublishPacket(MqttApplicationMessage applicationMessage);

        MqttPubAckPacket CreatePubAckPacket(MqttPublishPacket publishPacket);

        MqttBasePacket CreatePubRecPacket(MqttPublishPacket publishPacket);

        MqttApplicationMessage CreateApplicationMessage(MqttPublishPacket publishPacket);

        MqttClientAuthenticateResult CreateClientConnectResult(MqttConnAckPacket connAckPacket);

        MqttConnectPacket CreateConnectPacket(MqttApplicationMessage willApplicationMessage, IMqttClientOptions options);

        MqttConnAckPacket CreateConnAckPacket(MqttConnectionValidatorContext connectionValidatorContext);

        MqttClientSubscribeResult CreateClientSubscribeResult(MqttSubscribePacket subscribePacket, MqttSubAckPacket subAckPacket);

        MqttClientUnsubscribeResult CreateClientUnsubscribeResult(MqttUnsubscribePacket unsubscribePacket, MqttUnsubAckPacket unsubAckPacket);

        MqttSubscribePacket CreateSubscribePacket(MqttClientSubscribeOptions options);

        MqttSubAckPacket CreateSubAckPacket(MqttSubscribePacket subscribePacket, Server.MqttClientSubscribeResult subscribeResult);

        MqttUnsubscribePacket CreateUnsubscribePacket(MqttClientUnsubscribeOptions options);

        MqttUnsubAckPacket CreateUnsubAckPacket(MqttUnsubscribePacket unsubscribePacket, List<MqttUnsubscribeReasonCode> reasonCodes);

        MqttDisconnectPacket CreateDisconnectPacket(MqttClientDisconnectOptions options);

        MqttClientPublishResult CreatePublishResult(MqttPubAckPacket pubAckPacket);

        MqttClientPublishResult CreatePublishResult(MqttPubRecPacket pubRecPacket, MqttPubCompPacket pubCompPacket);
    }
}
