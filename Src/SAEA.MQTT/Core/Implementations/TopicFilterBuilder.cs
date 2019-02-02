/****************************************************************************
*项目名称：SAEA.MQTT.Core
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Core.Implementations
*类 名 称：TopicFilterBuilder
*版 本 号： V4.1.2.2
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/14 19:48:13
*描述：
*=====================================================================
*修改时间：2019/1/14 19:48:13
*修 改 人： yswenli
*版 本 号： V4.1.2.2
*描    述：
*****************************************************************************/
using SAEA.MQTT.Core.Protocol;
using SAEA.MQTT.Exceptions;
using SAEA.MQTT.Model;

namespace SAEA.MQTT.Core.Implementations
{
    public class TopicFilterBuilder
    {
        private MqttQualityOfServiceLevel _qualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce;
        private string _topic;

        public TopicFilterBuilder WithTopic(string topic)
        {
            _topic = topic;
            return this;
        }

        public TopicFilterBuilder WithQualityOfServiceLevel(MqttQualityOfServiceLevel qualityOfServiceLevel)
        {
            _qualityOfServiceLevel = qualityOfServiceLevel;
            return this;
        }

        public TopicFilterBuilder WithAtLeastOnceQoS()
        {
            _qualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce;
            return this;
        }

        public TopicFilterBuilder WithAtMostOnceQoS()
        {
            _qualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce;
            return this;
        }

        public TopicFilterBuilder WithExactlyOnceQoS()
        {
            _qualityOfServiceLevel = MqttQualityOfServiceLevel.ExactlyOnce;
            return this;
        }

        public TopicFilter Build()
        {
            if (string.IsNullOrEmpty(_topic))
            {
                throw new MqttProtocolViolationException("Topic is not set.");
            }

            return new TopicFilter(_topic, _qualityOfServiceLevel);
        }
    }
}
