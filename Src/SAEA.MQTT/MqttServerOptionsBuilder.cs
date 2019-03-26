/****************************************************************************
*项目名称：SAEA.MQTT
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT
*类 名 称：MqttServerOptionsBuilder
*版 本 号： v4.3.2.5
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 16:31:43
*描述：
*=====================================================================
*修改时间：2019/1/15 16:31:43
*修 改 人： yswenli
*版 本 号： v4.3.2.5
*描    述：
*****************************************************************************/
using SAEA.MQTT.Interface;
using SAEA.MQTT.Model;
using System;
using System.Net;
using System.Security.Authentication;

namespace SAEA.MQTT
{
    public class MqttServerOptionsBuilder
    {
        private readonly MqttServerOptions _options = new MqttServerOptions();

        public MqttServerOptionsBuilder WithConnectionBacklog(int value)
        {
            _options.DefaultEndpointOptions.ConnectionBacklog = value;
            _options.TlsEndpointOptions.ConnectionBacklog = value;
            return this;
        }

        public MqttServerOptionsBuilder WithMaxPendingMessagesPerClient(int value)
        {
            _options.MaxPendingMessagesPerClient = value;
            return this;
        }

        public MqttServerOptionsBuilder WithDefaultCommunicationTimeout(TimeSpan value)
        {
            _options.DefaultCommunicationTimeout = value;
            return this;
        }

        public MqttServerOptionsBuilder WithDefaultEndpoint()
        {
            _options.DefaultEndpointOptions.IsEnabled = true;
            return this;
        }

        public MqttServerOptionsBuilder WithDefaultEndpointPort(int value = 1883)
        {
            _options.DefaultEndpointOptions.Port = value;
            return this;
        }

        public MqttServerOptionsBuilder WithDefaultEndpointBoundIPAddress(IPAddress value)
        {
            _options.DefaultEndpointOptions.BoundInterNetworkAddress = value ?? IPAddress.Any;
            return this;
        }

        public MqttServerOptionsBuilder WithDefaultEndpointBoundIPV6Address(IPAddress value)
        {
            _options.DefaultEndpointOptions.BoundInterNetworkV6Address = value ?? IPAddress.Any;
            return this;
        }

        public MqttServerOptionsBuilder WithoutDefaultEndpoint()
        {
            _options.DefaultEndpointOptions.IsEnabled = false;
            return this;
        }

        public MqttServerOptionsBuilder WithEncryptedEndpoint()
        {
            _options.TlsEndpointOptions.IsEnabled = true;
            return this;
        }

        public MqttServerOptionsBuilder WithEncryptedEndpointPort(int value)
        {
            _options.TlsEndpointOptions.Port = value;
            return this;
        }

        public MqttServerOptionsBuilder WithEncryptedEndpointBoundIPAddress(IPAddress value)
        {
            _options.TlsEndpointOptions.BoundInterNetworkAddress = value;
            return this;
        }

        public MqttServerOptionsBuilder WithEncryptionCertificate(byte[] value)
        {
            _options.TlsEndpointOptions.Certificate = value;
            return this;
        }

        public MqttServerOptionsBuilder WithEncryptionSslProtocol(SslProtocols value)
        {
            _options.TlsEndpointOptions.SslProtocol = value;
            return this;
        }

        public MqttServerOptionsBuilder WithoutEncryptedEndpoint()
        {
            _options.TlsEndpointOptions.IsEnabled = false;
            return this;
        }

        public MqttServerOptionsBuilder WithStorage(IMqttServerStorage value)
        {
            _options.Storage = value;
            return this;
        }

        public MqttServerOptionsBuilder WithConnectionValidator(Action<MqttConnectionValidatorContext> value)
        {
            _options.ConnectionValidator = value;
            return this;
        }

        public MqttServerOptionsBuilder WithApplicationMessageInterceptor(Action<MqttMessageInterceptorContext> value)
        {
            _options.ApplicationMessageInterceptor = value;
            return this;
        }

        public MqttServerOptionsBuilder WithSubscriptionInterceptor(Action<MqttSubscriptionInterceptorContext> value)
        {
            _options.SubscriptionInterceptor = value;
            return this;
        }

        public MqttServerOptionsBuilder WithPersistentSessions()
        {
            _options.EnablePersistentSessions = true;
            return this;
        }

        public IMqttServerOptions Build()
        {
            return _options;
        }
    }
}
