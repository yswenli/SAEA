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
*命名空间：SAEA.MQTT.Server
*文件名： MqttServerOptionsBuilder
*版本号： v26.4.23.1
*唯一标识：ddd2394d-b1e5-42f6-860d-99716d779863
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using SAEA.MQTT.Certificates;

#if !WINDOWS_UWP
using System.Security.Cryptography.X509Certificates;
#endif

namespace SAEA.MQTT.Server
{
    public class MqttServerOptionsBuilder
    {
        readonly MqttServerOptions _options = new MqttServerOptions();
        
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

        public MqttServerOptionsBuilder WithDefaultEndpointPort(int value)
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

        public MqttServerOptionsBuilder WithEncryptedEndpointBoundIPV6Address(IPAddress value)
        {
            _options.TlsEndpointOptions.BoundInterNetworkV6Address = value;
            return this;
        }

#if !WINDOWS_UWP
        public MqttServerOptionsBuilder WithEncryptionCertificate(byte[] value, IMqttServerCertificateCredentials credentials = null)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            _options.TlsEndpointOptions.CertificateProvider = new BlobCertificateProvider(value)
            {
                Password = credentials?.Password
            };

            return this;
        }

        public MqttServerOptionsBuilder WithEncryptionCertificate(X509Certificate2 certificate)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));

            _options.TlsEndpointOptions.CertificateProvider = new X509CertificateProvider(certificate);
            return this;
        }
#endif

        public MqttServerOptionsBuilder WithEncryptionSslProtocol(SslProtocols value)
        {
            _options.TlsEndpointOptions.SslProtocol = value;
            return this;
        }

#if !WINDOWS_UWP
        public MqttServerOptionsBuilder WithClientCertificate(RemoteCertificateValidationCallback validationCallback = null, bool checkCertificateRevocation = false)
        {
            _options.TlsEndpointOptions.ClientCertificateRequired = true;
            _options.TlsEndpointOptions.CheckCertificateRevocation = checkCertificateRevocation;
            _options.TlsEndpointOptions.RemoteCertificateValidationCallback = validationCallback;
            return this;
        }
#endif

        public MqttServerOptionsBuilder WithoutEncryptedEndpoint()
        {
            _options.TlsEndpointOptions.IsEnabled = false;
            return this;
        }

#if !WINDOWS_UWP
        public MqttServerOptionsBuilder WithRemoteCertificateValidationCallback(RemoteCertificateValidationCallback value)
        {
            _options.TlsEndpointOptions.RemoteCertificateValidationCallback = value;
            return this;
        }
#endif

        public MqttServerOptionsBuilder WithStorage(IMqttServerStorage value)
        {
            _options.Storage = value;
            return this;
        }

        public MqttServerOptionsBuilder WithRetainedMessagesManager(IMqttRetainedMessagesManager value)
        {
            _options.RetainedMessagesManager = value;
            return this;
        }

        public MqttServerOptionsBuilder WithConnectionValidator(IMqttServerConnectionValidator value)
        {
            _options.ConnectionValidator = value;
            return this;
        }

        public MqttServerOptionsBuilder WithConnectionValidator(Action<MqttConnectionValidatorContext> value)
        {
            _options.ConnectionValidator = new MqttServerConnectionValidatorDelegate(value);
            return this;
        }

        public MqttServerOptionsBuilder WithDisconnectedInterceptor(IMqttServerClientDisconnectedHandler value)
        {
            _options.ClientDisconnectedInterceptor = value;
            return this;
        }

        public MqttServerOptionsBuilder WithDisconnectedInterceptor(Action<MqttServerClientDisconnectedEventArgs> value)
        {
            _options.ClientDisconnectedInterceptor = new MqttServerClientDisconnectedHandlerDelegate(value);
            return this;
        }

        public MqttServerOptionsBuilder WithApplicationMessageInterceptor(IMqttServerApplicationMessageInterceptor value)
        {
            _options.ApplicationMessageInterceptor = value;
            return this;
        }

        public MqttServerOptionsBuilder WithApplicationMessageInterceptor(Action<MqttApplicationMessageInterceptorContext> value)
        {
            _options.ApplicationMessageInterceptor = new MqttServerApplicationMessageInterceptorDelegate(value);
            return this;
        }

        public MqttServerOptionsBuilder WithClientMessageQueueInterceptor(IMqttServerClientMessageQueueInterceptor value)
        {
            _options.ClientMessageQueueInterceptor = value;
            return this;
        }

        public MqttServerOptionsBuilder WithClientMessageQueueInterceptor(Action<MqttClientMessageQueueInterceptorContext> value)
        {
            _options.ClientMessageQueueInterceptor = new MqttClientMessageQueueInterceptorDelegate(value);
            return this;
        }

        public MqttServerOptionsBuilder WithSubscriptionInterceptor(IMqttServerSubscriptionInterceptor value)
        {
            _options.SubscriptionInterceptor = value;
            return this;
        }

        public MqttServerOptionsBuilder WithUnsubscriptionInterceptor(IMqttServerUnsubscriptionInterceptor value)
        {
            _options.UnsubscriptionInterceptor = value;
            return this;
        }

        public MqttServerOptionsBuilder WithSubscriptionInterceptor(Action<MqttSubscriptionInterceptorContext> value)
        {
            _options.SubscriptionInterceptor = new MqttServerSubscriptionInterceptorDelegate(value);
            return this;
        }

        public MqttServerOptionsBuilder WithDefaultEndpointReuseAddress()
        {
            _options.DefaultEndpointOptions.ReuseAddress = true;
            return this;
        }

        public MqttServerOptionsBuilder WithTlsEndpointReuseAddress()
        {
            _options.TlsEndpointOptions.ReuseAddress = true;
            return this;
        }

        public MqttServerOptionsBuilder WithPersistentSessions()
        {
            _options.EnablePersistentSessions = true;
            return this;
        }

        /// <summary>
        /// Gets or sets the client ID which is used when publishing messages from the server directly.
        /// </summary>
        public MqttServerOptionsBuilder WithClientId(string value)
        {
            _options.ClientId = value;
            return this;
        }

        public IMqttServerOptions Build()
        {
            return _options;
        }

        public MqttServerOptionsBuilder WithUndeliveredMessageInterceptor(Action<MqttApplicationMessageInterceptorContext> value)
        {
            _options.UndeliveredMessageInterceptor = new MqttServerApplicationMessageInterceptorDelegate(value);
            return this;
        }
    }
}
