/****************************************************************************
*项目名称：SAEA.MQTT.Common
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Common
*类 名 称：ApplicationMessagePublisherExtensions
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/16 10:10:20
*描述：
*=====================================================================
*修改时间：2019/1/16 10:10:20
*修 改 人： yswenli
*版 本 号： V3.6.2.2
*描    述：
*****************************************************************************/
using SAEA.MQTT.Core.Implementations;
using SAEA.MQTT.Core.Protocol;
using SAEA.MQTT.Interface;
using SAEA.MQTT.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAEA.MQTT.Common
{
    public static class ApplicationMessagePublisherExtensions
    {
        public static async Task PublishAsync(this IApplicationMessagePublisher publisher, IEnumerable<MqttApplicationMessage> applicationMessages)
        {
            if (publisher == null) throw new ArgumentNullException(nameof(publisher));
            if (applicationMessages == null) throw new ArgumentNullException(nameof(applicationMessages));

            foreach (var applicationMessage in applicationMessages)
            {
                await publisher.PublishAsync(applicationMessage).ConfigureAwait(false);
            }
        }

        public static async Task PublishAsync(this IApplicationMessagePublisher publisher, params MqttApplicationMessage[] applicationMessages)
        {
            if (publisher == null) throw new ArgumentNullException(nameof(publisher));
            if (applicationMessages == null) throw new ArgumentNullException(nameof(applicationMessages));

            foreach (var applicationMessage in applicationMessages)
            {
                await publisher.PublishAsync(applicationMessage).ConfigureAwait(false);
            }
        }

        public static Task PublishAsync(this IApplicationMessagePublisher publisher, string topic)
        {
            if (publisher == null) throw new ArgumentNullException(nameof(publisher));
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            return publisher.PublishAsync(builder => builder
                .WithTopic(topic));
        }

        public static Task PublishAsync(this IApplicationMessagePublisher publisher, string topic, string payload)
        {
            if (publisher == null) throw new ArgumentNullException(nameof(publisher));
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            return publisher.PublishAsync(builder => builder
                .WithTopic(topic)
                .WithPayload(payload));
        }

        public static Task PublishAsync(this IApplicationMessagePublisher publisher, string topic, string payload, MqttQualityOfServiceLevel qualityOfServiceLevel)
        {
            if (publisher == null) throw new ArgumentNullException(nameof(publisher));
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            return publisher.PublishAsync(builder => builder
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(qualityOfServiceLevel));
        }

        public static Task PublishAsync(this IApplicationMessagePublisher publisher, string topic, string payload, MqttQualityOfServiceLevel qualityOfServiceLevel, bool retain)
        {
            if (publisher == null) throw new ArgumentNullException(nameof(publisher));
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            return publisher.PublishAsync(builder => builder
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(qualityOfServiceLevel)
                .WithRetainFlag(retain));
        }

        public static Task PublishAsync(this IApplicationMessagePublisher publisher, Func<MqttApplicationMessageBuilder, MqttApplicationMessageBuilder> builder)
        {
            var message = builder(new MqttApplicationMessageBuilder()).Build();
            return publisher.PublishAsync(message);
        }
    }
}
