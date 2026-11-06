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
*唯一标识：775eca1c-701d-4e7a-abcc-7ad168ab61c1
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttServerOptionsBuilder接口
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
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using SAEA.MQTT.Certificates;
using SAEA.MQTT.Protocol;

#if !WINDOWS_UWP
using System.Security.Cryptography.X509Certificates;
#endif

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 以链式调用方式收集并构建服务端运行参数
    /// </summary>
    public class MqttServerOptionsBuilder
    {
        readonly MqttServerOptions _options = new MqttServerOptions();
        
        /// <summary>
        /// 同时设置明文与 TLS 端点的监听队列长度
        /// </summary>
        /// <param name="value">监听队列允许排队的待接受连接数</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithConnectionBacklog(int value)
        {
            _options.DefaultEndpointOptions.ConnectionBacklog = value;
            _options.TlsEndpointOptions.ConnectionBacklog = value;
            return this;
        }

        /// <summary>
        /// 设置每个客户端允许积压的待发消息条数上限
        /// </summary>
        /// <param name="value">待发消息条数上限，超出后按溢出策略丢弃</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithMaxPendingMessagesPerClient(int value)
        {
            _options.MaxPendingMessagesPerClient = value;
            return this;
        }

        /// <summary>
        /// 设置内部通信的默认超时时间
        /// </summary>
        /// <param name="value">超时时长</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithDefaultCommunicationTimeout(TimeSpan value)
        {
            _options.DefaultCommunicationTimeout = value;
            return this;
        }

        /// <summary>
        /// 启用明文 TCP 端点
        /// </summary>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithDefaultEndpoint()
        {
            _options.DefaultEndpointOptions.IsEnabled = true;
            return this;
        }

        /// <summary>
        /// 设置明文 TCP 端点的监听端口
        /// </summary>
        /// <param name="value">监听端口号</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithDefaultEndpointPort(int value)
        {
            _options.DefaultEndpointOptions.Port = value;
            return this;
        }

        /// <summary>
        /// 设置明文 TCP 端点绑定的 IPv4 地址
        /// </summary>
        /// <param name="value">绑定的地址，为 null 时绑定 IPAddress.Any</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithDefaultEndpointBoundIPAddress(IPAddress value)
        {
            _options.DefaultEndpointOptions.BoundInterNetworkAddress = value ?? IPAddress.Any;
            return this;
        }

        /// <summary>
        /// 设置明文 TCP 端点绑定的 IPv6 地址
        /// </summary>
        /// <param name="value">绑定的地址，为 null 时绑定 IPAddress.Any</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithDefaultEndpointBoundIPV6Address(IPAddress value)
        {
            _options.DefaultEndpointOptions.BoundInterNetworkV6Address = value ?? IPAddress.Any;
            return this;
        }

        /// <summary>
        /// 关闭明文 TCP 端点
        /// </summary>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithoutDefaultEndpoint()
        {
            _options.DefaultEndpointOptions.IsEnabled = false;
            return this;
        }

        /// <summary>
        /// 启用 TLS 端点
        /// </summary>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithEncryptedEndpoint()
        {
            _options.TlsEndpointOptions.IsEnabled = true;
            return this;
        }

        /// <summary>
        /// 设置 TLS 端点的监听端口
        /// </summary>
        /// <param name="value">监听端口号</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithEncryptedEndpointPort(int value)
        {
            _options.TlsEndpointOptions.Port = value;
            return this;
        }

        /// <summary>
        /// 设置 TLS 端点绑定的 IPv4 地址
        /// </summary>
        /// <param name="value">绑定的 IPv4 地址</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithEncryptedEndpointBoundIPAddress(IPAddress value)
        {
            _options.TlsEndpointOptions.BoundInterNetworkAddress = value;
            return this;
        }

        /// <summary>
        /// 设置 TLS 端点绑定的 IPv6 地址
        /// </summary>
        /// <param name="value">绑定的 IPv6 地址</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithEncryptedEndpointBoundIPV6Address(IPAddress value)
        {
            _options.TlsEndpointOptions.BoundInterNetworkV6Address = value;
            return this;
        }

#if !WINDOWS_UWP
        /// <summary>
        /// 用证书二进制内容配置 TLS 服务端证书
        /// </summary>
        /// <param name="value">证书文件内容（如 PFX 字节），不能为 null</param>
        /// <param name="credentials">证书私钥口令，为 null 表示无口令</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithEncryptionCertificate(byte[] value, IMqttServerCertificateCredentials credentials = null)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            _options.TlsEndpointOptions.CertificateProvider = new BlobCertificateProvider(value)
            {
                Password = credentials?.Password
            };

            return this;
        }

        /// <summary>
        /// 用已加载的证书对象配置 TLS 服务端证书
        /// </summary>
        /// <param name="certificate">服务端证书，不能为 null</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithEncryptionCertificate(X509Certificate2 certificate)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));

            _options.TlsEndpointOptions.CertificateProvider = new X509CertificateProvider(certificate);
            return this;
        }
#endif

        /// <summary>
        /// 设置 TLS 端点使用的 SSL/TLS 协议版本
        /// </summary>
        /// <param name="value">允许的协议版本组合</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithEncryptionSslProtocol(SslProtocols value)
        {
            _options.TlsEndpointOptions.SslProtocol = value;
            return this;
        }

#if !WINDOWS_UWP
        /// <summary>
        /// 要求客户端提供证书，并设置其校验方式
        /// </summary>
        /// <param name="validationCallback">客户端证书校验回调，为 null 时使用默认校验</param>
        /// <param name="checkCertificateRevocation">是否检查证书吊销状态</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithClientCertificate(RemoteCertificateValidationCallback validationCallback = null, bool checkCertificateRevocation = false)
        {
            _options.TlsEndpointOptions.ClientCertificateRequired = true;
            _options.TlsEndpointOptions.CheckCertificateRevocation = checkCertificateRevocation;
            _options.TlsEndpointOptions.RemoteCertificateValidationCallback = validationCallback;
            return this;
        }
#endif

        /// <summary>
        /// 关闭 TLS 端点
        /// </summary>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithoutEncryptedEndpoint()
        {
            _options.TlsEndpointOptions.IsEnabled = false;
            return this;
        }

#if !WINDOWS_UWP
        /// <summary>
        /// 设置 TLS 握手中对端证书的校验回调
        /// </summary>
        /// <param name="value">证书校验回调，为 null 时使用默认校验</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithRemoteCertificateValidationCallback(RemoteCertificateValidationCallback value)
        {
            _options.TlsEndpointOptions.RemoteCertificateValidationCallback = value;
            return this;
        }
#endif

        /// <summary>
        /// 设置服务端存储，用于持久化会话状态与 retained 消息
        /// </summary>
        /// <param name="value">存储实现，为 null 表示不持久化</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithStorage(IMqttServerStorage value)
        {
            _options.Storage = value;
            return this;
        }

        /// <summary>
        /// 设置 retained 消息管理器实现
        /// </summary>
        /// <param name="value">retained 消息管理器，为 null 时取消使用管理器</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithRetainedMessagesManager(IMqttRetainedMessagesManager value)
        {
            _options.RetainedMessagesManager = value;
            return this;
        }

        /// <summary>
        /// 设置连接校验器实现
        /// </summary>
        /// <param name="value">连接校验器，为 null 表示不做校验</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithConnectionValidator(IMqttServerConnectionValidator value)
        {
            _options.ConnectionValidator = value;
            return this;
        }

        /// <summary>
        /// 用同步回调设置连接校验器
        /// </summary>
        /// <param name="value">连接校验回调，不能为 null</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithConnectionValidator(Action<MqttConnectionValidatorContext> value)
        {
            _options.ConnectionValidator = new MqttServerConnectionValidatorDelegate(value);
            return this;
        }

        /// <summary>
        /// 设置客户端断开连接处理器
        /// </summary>
        /// <param name="value">断开连接处理器，为 null 表示不处理</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithDisconnectedInterceptor(IMqttServerClientDisconnectedHandler value)
        {
            _options.ClientDisconnectedInterceptor = value;
            return this;
        }

        /// <summary>
        /// 用同步回调设置客户端断开连接处理器
        /// </summary>
        /// <param name="value">断开连接回调，不能为 null</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithDisconnectedInterceptor(Action<MqttServerClientDisconnectedEventArgs> value)
        {
            _options.ClientDisconnectedInterceptor = new MqttServerClientDisconnectedHandlerDelegate(value);
            return this;
        }

        /// <summary>
        /// 设置应用消息发布拦截器实现
        /// </summary>
        /// <param name="value">发布拦截器，为 null 表示不拦截</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithApplicationMessageInterceptor(IMqttServerApplicationMessageInterceptor value)
        {
            _options.ApplicationMessageInterceptor = value;
            return this;
        }

        /// <summary>
        /// 用同步回调设置应用消息发布拦截器
        /// </summary>
        /// <param name="value">发布拦截回调，不能为 null</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithApplicationMessageInterceptor(Action<MqttApplicationMessageInterceptorContext> value)
        {
            _options.ApplicationMessageInterceptor = new MqttServerApplicationMessageInterceptorDelegate(value);
            return this;
        }

        /// <summary>
        /// 设置客户端消息队列拦截器实现
        /// </summary>
        /// <param name="value">队列拦截器，为 null 表示不拦截</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithClientMessageQueueInterceptor(IMqttServerClientMessageQueueInterceptor value)
        {
            _options.ClientMessageQueueInterceptor = value;
            return this;
        }

        /// <summary>
        /// 用同步回调设置客户端消息队列拦截器
        /// </summary>
        /// <param name="value">入队拦截回调，不能为 null</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithClientMessageQueueInterceptor(Action<MqttClientMessageQueueInterceptorContext> value)
        {
            _options.ClientMessageQueueInterceptor = new MqttClientMessageQueueInterceptorDelegate(value);
            return this;
        }

        /// <summary>
        /// 设置订阅拦截器实现
        /// </summary>
        /// <param name="value">订阅拦截器，为 null 表示不拦截</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithSubscriptionInterceptor(IMqttServerSubscriptionInterceptor value)
        {
            _options.SubscriptionInterceptor = value;
            return this;
        }

        /// <summary>
        /// 设置取消订阅拦截器实现
        /// </summary>
        /// <param name="value">取消订阅拦截器，为 null 表示不拦截</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithUnsubscriptionInterceptor(IMqttServerUnsubscriptionInterceptor value)
        {
            _options.UnsubscriptionInterceptor = value;
            return this;
        }

        /// <summary>
        /// 用同步回调设置订阅拦截器
        /// </summary>
        /// <param name="value">订阅拦截回调，不能为 null</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithSubscriptionInterceptor(Action<MqttSubscriptionInterceptorContext> value)
        {
            _options.SubscriptionInterceptor = new MqttServerSubscriptionInterceptorDelegate(value);
            return this;
        }

        /// <summary>
        /// 允许明文 TCP 端点复用本地地址
        /// </summary>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithDefaultEndpointReuseAddress()
        {
            _options.DefaultEndpointOptions.ReuseAddress = true;
            return this;
        }

        /// <summary>
        /// 允许 TLS 端点复用本地地址
        /// </summary>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithTlsEndpointReuseAddress()
        {
            _options.TlsEndpointOptions.ReuseAddress = true;
            return this;
        }

        /// <summary>
        /// 启用会话保持，客户端断开后其会话与待发消息继续保留
        /// </summary>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithPersistentSessions()
        {
            _options.EnablePersistentSessions = true;
            return this;
        }

        /// <summary>
        /// 设置服务端自身发布消息时使用的客户端标识
        /// </summary>
        /// <param name="value">服务端发布消息时使用的客户端标识</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithClientId(string value)
        {
            _options.ClientId = value;
            return this;
        }

        /// <summary>
        /// 返回构建完成的服务端选项
        /// </summary>
        /// <returns>构建器内部持有的同一选项实例，之后的设置调用仍会影响它</returns>
        public IMqttServerOptions Build()
        {
            return _options;
        }

        /// <summary>
        /// 用同步回调设置未投递消息拦截器，客户端离线丢弃消息前触发
        /// </summary>
        /// <param name="value">未投递消息拦截回调，不能为 null</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithUndeliveredMessageInterceptor(Action<MqttApplicationMessageInterceptorContext> value)
        {
            _options.UndeliveredMessageInterceptor = new MqttServerApplicationMessageInterceptorDelegate(value);
            return this;
        }

        /// <summary>
        /// 设置服务端接收窗口，即允许的并发未确认 QoS 1/2 报文数
        /// </summary>
        /// <param name="value">接收窗口大小，不能为 0，否则启动时抛出配置异常</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithReceiveMaximum(ushort value)
        {
            _options.Capabilities.ReceiveMaximum = value;
            return this;
        }

        /// <summary>
        /// 设置服务端可接受的最大报文长度
        /// </summary>
        /// <param name="value">最大报文字节数，启动时会被规范到 1 至 268435455 之间</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithMaximumPacketSize(uint value)
        {
            _options.Capabilities.MaximumPacketSize = value;
            return this;
        }

        /// <summary>
        /// 设置服务端支持的入站主题别名数量
        /// </summary>
        /// <param name="value">主题别名上限，0 表示不支持主题别名</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithTopicAliasMaximum(ushort value)
        {
            _options.Capabilities.TopicAliasMaximum = value;
            return this;
        }

        /// <summary>
        /// 设置是否允许客户端发布 retained 消息
        /// </summary>
        /// <param name="value">true 允许，false 时在 CONNACK 中通告不支持保留</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithRetainAvailable(bool value)
        {
            _options.Capabilities.RetainAvailable = value;
            return this;
        }

        /// <summary>
        /// 设置是否允许通配符订阅
        /// </summary>
        /// <param name="value">true 允许，false 时在 CONNACK 中通告不支持通配符订阅</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithWildcardSubscriptionAvailable(bool value)
        {
            _options.Capabilities.WildcardSubscriptionAvailable = value;
            return this;
        }

        /// <summary>
        /// 设置是否允许订阅携带订阅标识符
        /// </summary>
        /// <param name="value">true 允许，false 时在 CONNACK 中通告不支持订阅标识符</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithSubscriptionIdentifiersAvailable(bool value)
        {
            _options.Capabilities.SubscriptionIdentifiersAvailable = value;
            return this;
        }

        /// <summary>
        /// 设置是否允许共享订阅
        /// </summary>
        /// <param name="value">true 允许，false 时在 CONNACK 中通告不支持共享订阅</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithSharedSubscriptionAvailable(bool value)
        {
            _options.Capabilities.SharedSubscriptionAvailable = value;
            return this;
        }

        /// <summary>
        /// 设置服务端支持的最高服务质量等级
        /// </summary>
        /// <param name="value">最高 QoS，仅接受 AtMostOnce、AtLeastOnce、ExactlyOnce，其他值启动时抛出配置异常</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithMaximumQoS(MqttQualityOfServiceLevel value)
        {
            _options.Capabilities.MaximumQoS = value;
            return this;
        }

        /// <summary>
        /// 设置服务端强制客户端使用的保活秒数
        /// </summary>
        /// <param name="value">保活秒数，null 表示沿用客户端请求值，0 会在启动时抛出配置异常</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithServerKeepAlive(ushort? value)
        {
            _options.Capabilities.ServerKeepAlive = value;
            return this;
        }

        /// <summary>
        /// 设置允许的会话过期时间上限
        /// </summary>
        /// <param name="value">上限秒数，null 表示不限制并取 uint.MaxValue</param>
        /// <returns>同一个构建器实例，便于链式调用</returns>
        public MqttServerOptionsBuilder WithMaximumSessionExpiryInterval(uint? value)
        {
            _options.Capabilities.MaximumSessionExpiryInterval = value;
            return this;
        }
    }
}
