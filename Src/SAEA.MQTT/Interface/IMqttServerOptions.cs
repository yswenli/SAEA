/****************************************************************************
*项目名称：SAEA.MQTT.Interface
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Interface
*类 名 称：IMqttServerOptions
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 10:21:30
*描述：
*=====================================================================
*修改时间：2019/1/15 10:21:30
*修 改 人： yswenli
*版 本 号： V3.6.2.2
*描    述：
*****************************************************************************/
using SAEA.MQTT.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.MQTT.Interface
{
    public interface IMqttServerOptions
    {
        bool EnablePersistentSessions { get; }

        int MaxPendingMessagesPerClient { get; }
        MqttPendingMessagesOverflowStrategy PendingMessagesOverflowStrategy { get; }

        TimeSpan DefaultCommunicationTimeout { get; }

        Action<MqttConnectionValidatorContext> ConnectionValidator { get; }
        Action<MqttSubscriptionInterceptorContext> SubscriptionInterceptor { get; }
        Action<MqttApplicationMessageInterceptorContext> ApplicationMessageInterceptor { get; }
        Action<MqttClientMessageQueueInterceptorContext> ClientMessageQueueInterceptor { get; }

        MqttServerTcpEndpointOptions DefaultEndpointOptions { get; }
        MqttServerTlsTcpEndpointOptions TlsEndpointOptions { get; }

        IMqttServerStorage Storage { get; }
    }
}
