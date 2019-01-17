/****************************************************************************
*项目名称：SAEA.MQTT.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Model
*类 名 称：MqttServerOptions
*版 本 号： V3.6.2.2
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 16:23:08
*描述：
*=====================================================================
*修改时间：2019/1/15 16:23:08
*修 改 人： yswenli
*版 本 号： V3.6.2.2
*描    述：
*****************************************************************************/
using SAEA.MQTT.Interface;
using System;

namespace SAEA.MQTT.Model
{
    public class MqttServerOptions : IMqttServerOptions
    {
        public MqttServerTcpEndpointOptions DefaultEndpointOptions { get; } = new MqttServerTcpEndpointOptions();

        public MqttServerTlsTcpEndpointOptions TlsEndpointOptions { get; } = new MqttServerTlsTcpEndpointOptions();

        public bool EnablePersistentSessions { get; set; }

        public int MaxPendingMessagesPerClient { get; set; } = 250;
        public MqttPendingMessagesOverflowStrategy PendingMessagesOverflowStrategy { get; set; } = MqttPendingMessagesOverflowStrategy.DropOldestQueuedMessage;

        public TimeSpan DefaultCommunicationTimeout { get; set; } = TimeSpan.FromSeconds(15);

        public Action<MqttConnectionValidatorContext> ConnectionValidator { get; set; }

        public Action<MqttMessageInterceptorContext> ApplicationMessageInterceptor { get; set; }

        public Action<MqttClientMessageQueueInterceptorContext> ClientMessageQueueInterceptor { get; set; }

        public Action<MqttSubscriptionInterceptorContext> SubscriptionInterceptor { get; set; }

        public IMqttServerStorage Storage { get; set; }
    }
}
