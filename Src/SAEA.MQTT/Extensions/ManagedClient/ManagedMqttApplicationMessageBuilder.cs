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
*命名空间：SAEA.MQTT.Extensions.ManagedClient
*文件名： ManagedMqttApplicationMessageBuilder
*版本号： v26.4.23.1
*唯一标识：f9437e92-7ae6-4e52-84a4-afb40f6b8c9e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MQTT扩展功能类
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MQTT扩展功能类
*
*****************************************************************************/
using System;

namespace SAEA.MQTT.Extensions.ManagedClient
{
    public class ManagedMqttApplicationMessageBuilder
    {
        private Guid _id = Guid.NewGuid();
        private MqttApplicationMessage _applicationMessage;

        public ManagedMqttApplicationMessageBuilder WithId(Guid id)
        {
            _id = id;
            return this;
        }

        public ManagedMqttApplicationMessageBuilder WithApplicationMessage(MqttApplicationMessage applicationMessage)
        {
            _applicationMessage = applicationMessage;
            return this;
        }

        public ManagedMqttApplicationMessageBuilder WithApplicationMessage(Action<MqttApplicationMessageBuilder> builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            var internalBuilder = new MqttApplicationMessageBuilder();
            builder(internalBuilder);

            _applicationMessage = internalBuilder.Build();
            return this;
        }

        public ManagedMqttApplicationMessage Build()
        {
            if (_applicationMessage == null)
            {
                throw new InvalidOperationException("The ApplicationMessage cannot be null.");
            }

            return new ManagedMqttApplicationMessage
            {
                Id = _id,
                ApplicationMessage = _applicationMessage
            };
        }
    }
}
