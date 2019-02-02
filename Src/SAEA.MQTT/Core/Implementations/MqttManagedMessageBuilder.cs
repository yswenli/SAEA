/****************************************************************************
*项目名称：SAEA.MQTT
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Core.Implementations
*类 名 称：MqttManagedMessageBuilder
*版 本 号： V4.1.2.2
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/14 19:07:44
*描述：
*=====================================================================
*修改时间：2019/1/14 19:07:44
*修 改 人： yswenli
*版 本 号： V4.1.2.2
*描    述：
*****************************************************************************/
using SAEA.MQTT.Model;
using System;

namespace SAEA.MQTT.Core.Implementations
{
    public class MqttManagedMessageBuilder
    {
        private Guid _id = Guid.NewGuid();
        private MqttMessage _applicationMessage;

        public MqttManagedMessageBuilder WithId(Guid id)
        {
            _id = id;
            return this;
        }

        public MqttManagedMessageBuilder WithApplicationMessage(MqttMessage applicationMessage)
        {
            _applicationMessage = applicationMessage;
            return this;
        }

        public MqttManagedMessageBuilder WithApplicationMessage(Action<MqttMessageBuilder> builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            var internalBuilder = new MqttMessageBuilder();
            builder(internalBuilder);

            _applicationMessage = internalBuilder.Build();
            return this;
        }

        public MqttManagedMessage Build()
        {
            if (_applicationMessage == null)
            {
                throw new InvalidOperationException("The ApplicationMessage cannot be null.");
            }

            return new MqttManagedMessage
            {
                Id = _id,
                ApplicationMessage = _applicationMessage
            };
        }
    }
}
