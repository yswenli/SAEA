/****************************************************************************
*项目名称：SAEA.MQTT.Common
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Common
*类 名 称：ManagedMqttClientExtensions
*版 本 号： V4.1.2.2
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/14 19:49:43
*描述：
*=====================================================================
*修改时间：2019/1/14 19:49:43
*修 改 人： yswenli
*版 本 号： V4.1.2.2
*描    述：
*****************************************************************************/
using SAEA.MQTT.Common.Log;
using SAEA.MQTT.Core;
using SAEA.MQTT.Core.Implementations;
using SAEA.MQTT.Core.Protocol;
using SAEA.MQTT.Interface;
using SAEA.MQTT.Model;
using System;
using System.Threading.Tasks;

namespace SAEA.MQTT.Common
{
    public static class ManagedMqttClientExtensions
    {
        public static IMqttManagedClient CreateManagedMqttClient(this IMqttClientFactory factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            return new MqttManagedClient(factory.CreateMqttClient(), new MqttNetLogger().CreateChildLogger());
        }

        public static IMqttManagedClient CreateManagedMqttClient(this IMqttClientFactory factory, IMqttNetLogger logger)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            return new MqttManagedClient(factory.CreateMqttClient(), logger.CreateChildLogger());
        }

        public static Task SubscribeAsync(this IMqttManagedClient managedClient, params TopicFilter[] topicFilters)
        {
            if (managedClient == null) throw new ArgumentNullException(nameof(managedClient));

            return managedClient.SubscribeAsync(topicFilters);
        }

        public static Task SubscribeAsync(this IMqttManagedClient managedClient, string topic, MqttQualityOfServiceLevel qualityOfServiceLevel)
        {
            if (managedClient == null) throw new ArgumentNullException(nameof(managedClient));
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            return managedClient.SubscribeAsync(new TopicFilterBuilder().WithTopic(topic).WithQualityOfServiceLevel(qualityOfServiceLevel).Build());
        }

        public static Task SubscribeAsync(this IMqttManagedClient managedClient, string topic)
        {
            if (managedClient == null) throw new ArgumentNullException(nameof(managedClient));
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            return managedClient.SubscribeAsync(new TopicFilterBuilder().WithTopic(topic).Build());
        }

        public static Task UnsubscribeAsync(this IMqttManagedClient managedClient, params string[] topicFilters)
        {
            if (managedClient == null) throw new ArgumentNullException(nameof(managedClient));

            return managedClient.UnsubscribeAsync(topicFilters);
        }
    }
}
