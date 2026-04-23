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
*命名空间：SAEA.MQTT.Formatter
*文件名： IMqttDataConverter
*版本号： v26.4.23.1
*唯一标识：55d2edd8-43b3-4741-93eb-92d630d20a19
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttDataConverter转换器类
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：IMqttDataConverter转换器类
*
*****************************************************************************/
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
