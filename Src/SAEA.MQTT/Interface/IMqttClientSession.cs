/****************************************************************************
*项目名称：SAEA.MQTT.Interface
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Interface
*类 名 称：IMqttClientSession
*版 本 号： v4.2.1.6
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 15:42:47
*描述：
*=====================================================================
*修改时间：2019/1/15 15:42:47
*修 改 人： yswenli
*版 本 号： v4.2.1.6
*描    述：
*****************************************************************************/
using SAEA.MQTT.Core.Implementations;
using SAEA.MQTT.Core.Packets;
using SAEA.MQTT.Core.Protocol;
using SAEA.MQTT.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAEA.MQTT.Interface
{
    public interface IMqttClientSession : IDisposable
    {
        string ClientId { get; }
        void FillStatus(MqttClientSessionStatus status);

        void EnqueueApplicationMessage(MqttClientSession senderClientSession, MqttPublishPacket publishPacket);
        void ClearPendingApplicationMessages();

        Task RunAsync(MqttConnectPacket connectPacket, IMqttChannelAdapter adapter);
        void Stop(MqttClientDisconnectType disconnectType);

        Task SubscribeAsync(IList<TopicFilter> topicFilters);
        Task UnsubscribeAsync(IList<string> topicFilters);
    }
}
