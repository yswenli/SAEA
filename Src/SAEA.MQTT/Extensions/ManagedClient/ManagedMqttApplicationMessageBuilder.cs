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
*唯一标识：7435064f-1335-4685-ae1b-25daee023eb4
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：ManagedMqttApplicationMessageBuilder接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;

namespace SAEA.MQTT.Extensions.ManagedClient
{
    /// <summary>
    /// 托管应用消息的构造器
    /// </summary>
    public class ManagedMqttApplicationMessageBuilder
    {
        private Guid _id = Guid.NewGuid();
        private MqttApplicationMessage _applicationMessage;

        /// <summary>
        /// 指定消息唯一标识，默认使用新建的GUID
        /// </summary>
        /// <param name="id">消息唯一标识</param>
        /// <returns>当前构造器</returns>
        public ManagedMqttApplicationMessageBuilder WithId(Guid id)
        {
            _id = id;
            return this;
        }

        /// <summary>
        /// 直接指定要发送的应用消息
        /// </summary>
        /// <param name="applicationMessage">应用消息</param>
        /// <returns>当前构造器</returns>
        public ManagedMqttApplicationMessageBuilder WithApplicationMessage(MqttApplicationMessage applicationMessage)
        {
            _applicationMessage = applicationMessage;
            return this;
        }

        /// <summary>
        /// 通过回调配置应用消息
        /// </summary>
        /// <param name="builder">应用消息构造回调，不能为null</param>
        /// <returns>当前构造器</returns>
        public ManagedMqttApplicationMessageBuilder WithApplicationMessage(Action<MqttApplicationMessageBuilder> builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            var internalBuilder = new MqttApplicationMessageBuilder();
            builder(internalBuilder);

            _applicationMessage = internalBuilder.Build();
            return this;
        }

        /// <summary>
        /// 生成托管应用消息
        /// </summary>
        /// <returns>带当前标识与应用消息的实例</returns>
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
