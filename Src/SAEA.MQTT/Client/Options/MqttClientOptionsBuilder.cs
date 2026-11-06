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
*命名空间：SAEA.MQTT.Client.Options
*文件名： MqttClientOptionsBuilder
*版本号： v26.4.23.1
*唯一标识：52f9c77d-40dc-4ded-b556-55081c18d0e5
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientOptionsBuilder接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.MQTT.Client.ExtendedAuthenticationExchange;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SAEA.MQTT.Client.Options
{
    /// <summary>
    /// 客户端连接选项构建器，所有 With 方法均返回自身以支持链式调用
    /// </summary>
    public class MqttClientOptionsBuilder
    {
        readonly MqttClientOptions _options = new MqttClientOptions();

        MqttClientTcpOptions _tcpOptions;
        MqttClientWebSocketOptions _webSocketOptions;
        MqttClientOptionsBuilderTlsParameters _tlsParameters;
        MqttClientWebSocketProxyOptions _proxyOptions;

        /// <summary>
        /// 设置 MQTT 协议版本
        /// </summary>
        /// <param name="value">协议版本，不能为 Unknown</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithProtocolVersion(MqttProtocolVersion value)
        {
            if (value == MqttProtocolVersion.Unknown)
            {
                throw new ArgumentException("Protocol version is invalid.");
            }

            _options.ProtocolVersion = value;
            return this;
        }

        /// <summary>
        /// 设置单次网络读写的等待超时
        /// </summary>
        /// <param name="value">等待超时</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithCommunicationTimeout(TimeSpan value)
        {
            _options.CommunicationTimeout = value;
            return this;
        }

        /// <summary>
        /// 设置是否要求服务端丢弃已有会话重新开始
        /// </summary>
        /// <param name="value">true 表示清理会话，默认 true</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithCleanSession(bool value = true)
        {
            _options.CleanSession = value;
            return this;
        }

        /// <summary>
        /// 已废弃，不做任何设置，直接返回构建器自身
        /// </summary>
        /// <param name="value">保活发送间隔，已忽略</param>
        /// <returns>构建器自身，便于链式调用</returns>
        [Obsolete("This method is no longer supported. The client will send ping requests just before the keep alive interval is going to elapse. As per MQTT RFC the serve has to wait 1.5 times the interval so we don't need this anymore.")]
        public MqttClientOptionsBuilder WithKeepAliveSendInterval(TimeSpan value)
        {
            return this;
        }

        /// <summary>
        /// 关闭保活，等价于保活间隔设为 TimeSpan.Zero
        /// </summary>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithNoKeepAlive()
        {
            return WithKeepAlivePeriod(TimeSpan.Zero);
        }

        /// <summary>
        /// 设置保活间隔
        /// </summary>
        /// <param name="value">保活间隔，TimeSpan.Zero 表示不发送 PINGREQ</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithKeepAlivePeriod(TimeSpan value)
        {
            _options.KeepAlivePeriod = value;
            return this;
        }

        /// <summary>
        /// 设置客户端标识符
        /// </summary>
        /// <param name="value">客户端标识符</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithClientId(string value)
        {
            _options.ClientId = value;
            return this;
        }

        /// <summary>
        /// 设置遗嘱消息
        /// </summary>
        /// <param name="value">遗嘱消息，null 表示不设置遗嘱</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithWillMessage(MqttApplicationMessage value)
        {
            _options.WillMessage = value;
            return this;
        }

        /// <summary>
        /// 设置增强认证的方法名与初始数据
        /// </summary>
        /// <param name="method">认证方法名</param>
        /// <param name="data">认证初始数据</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithAuthentication(string method, byte[] data)
        {
            _options.AuthenticationMethod = method;
            _options.AuthenticationData = data;
            return this;
        }

        /// <summary>
        /// 设置遗嘱消息的延迟发布时长
        /// </summary>
        /// <param name="willDelayInterval">延迟时长（秒），null 表示不携带该属性</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithWillDelayInterval(uint? willDelayInterval)
        {
            _options.WillDelayInterval = willDelayInterval;
            return this;
        }

        /// <summary>
        /// 设置客户端可接受的主题别名最大编号
        /// </summary>
        /// <param name="topicAliasMaximum">最大编号，null 或 0 表示不接受入站别名</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithTopicAliasMaximum(ushort? topicAliasMaximum)
        {
            _options.TopicAliasMaximum = topicAliasMaximum;
            return this;
        }

        /// <summary>
        /// 设置客户端可接收的最大报文长度
        /// </summary>
        /// <param name="maximumPacketSize">最大长度（字节），null 表示使用协议上限</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithMaximumPacketSize(uint? maximumPacketSize)
        {
            _options.MaximumPacketSize = maximumPacketSize;
            return this;
        }

        /// <summary>
        /// 设置客户端允许的在途 QoS 大于 0 报文数
        /// </summary>
        /// <param name="receiveMaximum">在途报文数上限，null 表示使用协议上限</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithReceiveMaximum(ushort? receiveMaximum)
        {
            _options.ReceiveMaximum = receiveMaximum;
            return this;
        }

        /// <summary>
        /// 设置是否要求服务端返回原因字符串与用户属性
        /// </summary>
        /// <param name="requestProblemInformation">true 表示要求返回，默认 true，null 表示不携带该属性</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithRequestProblemInformation(bool? requestProblemInformation = true)
        {
            _options.RequestProblemInformation = requestProblemInformation;
            return this;
        }

        /// <summary>
        /// 设置是否向服务端请求响应信息
        /// </summary>
        /// <param name="requestResponseInformation">true 表示请求，默认 true，null 表示不携带该属性</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithRequestResponseInformation(bool? requestResponseInformation = true)
        {
            _options.RequestResponseInformation = requestResponseInformation;
            return this;
        }

        /// <summary>
        /// 设置会话过期时间
        /// </summary>
        /// <param name="sessionExpiryInterval">过期时间（秒），null 表示不携带该属性</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithSessionExpiryInterval(uint? sessionExpiryInterval)
        {
            _options.SessionExpiryInterval = sessionExpiryInterval;
            return this;
        }

        /// <summary>
        /// 追加一条 CONNECT 报文的用户自定义属性
        /// </summary>
        /// <param name="name">属性名，不能为 null</param>
        /// <param name="value">属性值，不能为 null</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithUserProperty(string name, string value)
        {
            if (name is null) throw new ArgumentNullException(nameof(name));
            if (value is null) throw new ArgumentNullException(nameof(value));

            if (_options.UserProperties == null)
            {
                _options.UserProperties = new List<MqttUserProperty>();
            }

            _options.UserProperties.Add(new MqttUserProperty(name, value));
            return this;
        }

        /// <summary>
        /// 用用户名和密码文本设置凭据，密码按 UTF-8 编码为字节
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码明文，null 表示不带密码</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithCredentials(string username, string password = null)
        {
            byte[] passwordBuffer = null;

            if (password != null)
            {
                passwordBuffer = Encoding.UTF8.GetBytes(password);
            }

            return WithCredentials(username, passwordBuffer);
        }

        /// <summary>
        /// 用用户名和密码字节设置凭据
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码字节，null 表示不带密码</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithCredentials(string username, byte[] password = null)
        {
            _options.Credentials = new MqttClientCredentials
            {
                Username = username,
                Password = password
            };

            return this;
        }

        /// <summary>
        /// 直接设置自定义凭据实现
        /// </summary>
        /// <param name="credentials">凭据实现，null 表示匿名连接</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithCredentials(IMqttClientCredentials credentials)
        {
            _options.Credentials = credentials;

            return this;
        }

        /// <summary>
        /// 设置增强认证交互处理器
        /// </summary>
        /// <param name="handler">AUTH 报文处理器，null 表示不处理</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithExtendedAuthenticationExchangeHandler(IMqttExtendedAuthenticationExchangeHandler handler)
        {
            _options.ExtendedAuthenticationExchangeHandler = handler;
            return this;
        }

        /// <summary>
        /// 用主机名和端口配置 TCP 通道，端口为 null 时按 TLS 是否启用在 1883 与 8883 之间选择
        /// </summary>
        /// <param name="server">服务端主机名或 IP</param>
        /// <param name="port">端口，null 表示按默认规则确定</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithTcpServer(string server, int? port = null)
        {
            _tcpOptions = new MqttClientTcpOptions
            {
                Server = server,
                Port = port
            };

            return this;
        }

        // TODO: Consider creating _MqttClientTcpOptionsBuilder_ as overload.
        /// <summary>
        /// 通过回调自定义 TCP 通道选项，会先重置已有的 TCP 选项
        /// </summary>
        /// <param name="optionsBuilder">TCP 选项配置回调，不能为 null</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithTcpServer(Action<MqttClientTcpOptions> optionsBuilder)
        {
            if (optionsBuilder == null) throw new ArgumentNullException(nameof(optionsBuilder));

            _tcpOptions = new MqttClientTcpOptions();
            optionsBuilder.Invoke(_tcpOptions);

            return this;
        }

        /// <summary>
        /// 用显式参数配置 WebSocket 代理，仅对 WebSocket 通道生效
        /// </summary>
        /// <param name="address">代理地址</param>
        /// <param name="username">代理认证用户名，null 表示不认证</param>
        /// <param name="password">代理认证密码，null 表示不认证</param>
        /// <param name="domain">代理认证域，null 表示不指定</param>
        /// <param name="bypassOnLocal">本地地址是否绕过代理，默认 false</param>
        /// <param name="bypassList">不经过代理的地址列表，null 表示不设置</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithProxy(string address, string username = null, string password = null, string domain = null, bool bypassOnLocal = false, string[] bypassList = null)
        {
            _proxyOptions = new MqttClientWebSocketProxyOptions
            {
                Address = address,
                Username = username,
                Password = password,
                Domain = domain,
                BypassOnLocal = bypassOnLocal,
                BypassList = bypassList
            };

            return this;
        }

        /// <summary>
        /// 通过回调自定义 WebSocket 代理选项，会先重置已有的代理选项
        /// </summary>
        /// <param name="optionsBuilder">代理选项配置回调，不能为 null</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithProxy(Action<MqttClientWebSocketProxyOptions> optionsBuilder)
        {
            if (optionsBuilder == null) throw new ArgumentNullException(nameof(optionsBuilder));

            _proxyOptions = new MqttClientWebSocketProxyOptions();
            optionsBuilder(_proxyOptions);
            return this;
        }

        /// <summary>
        /// 用地址配置 WebSocket 通道，并可选带入请求头与 Cookie
        /// </summary>
        /// <param name="uri">WebSocket 服务端地址</param>
        /// <param name="parameters">请求头与 Cookie 容器，null 表示都不设置</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithWebSocketServer(string uri, MqttClientOptionsBuilderWebSocketParameters parameters = null)
        {
            _webSocketOptions = new MqttClientWebSocketOptions
            {
                Uri = uri,
                RequestHeaders = parameters?.RequestHeaders,
                CookieContainer = parameters?.CookieContainer
            };

            return this;
        }

        /// <summary>
        /// 通过回调自定义 WebSocket 通道选项，会先重置已有的 WebSocket 选项
        /// </summary>
        /// <param name="optionsBuilder">WebSocket 选项配置回调，不能为 null</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithWebSocketServer(Action<MqttClientWebSocketOptions> optionsBuilder)
        {
            if (optionsBuilder == null) throw new ArgumentNullException(nameof(optionsBuilder));

            _webSocketOptions = new MqttClientWebSocketOptions();
            optionsBuilder.Invoke(_webSocketOptions);

            return this;
        }

        /// <summary>
        /// 指定 TLS 参数，仅在 UseTls 为 true 时于构建阶段写入通道选项
        /// </summary>
        /// <param name="parameters">TLS 参数</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithTls(MqttClientOptionsBuilderTlsParameters parameters)
        {
            _tlsParameters = parameters;
            return this;
        }

        /// <summary>
        /// 启用 TLS 并使用默认 TLS 参数
        /// </summary>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithTls()
        {
            return WithTls(new MqttClientOptionsBuilderTlsParameters { UseTls = true });
        }

        /// <summary>
        /// 通过回调自定义 TLS 参数，UseTls 需在回调内设为 true 才会生效
        /// </summary>
        /// <param name="optionsBuilder">TLS 参数配置回调，不能为 null</param>
        /// <returns>构建器自身，便于链式调用</returns>
        public MqttClientOptionsBuilder WithTls(Action<MqttClientOptionsBuilderTlsParameters> optionsBuilder)
        {
            if (optionsBuilder == null) throw new ArgumentNullException(nameof(optionsBuilder));

            _tlsParameters = new MqttClientOptionsBuilderTlsParameters();
            optionsBuilder(_tlsParameters);
            return this;
        }

        /// <summary>
        /// 生成连接选项，未配置 TCP 或 WebSocket 通道、或仅配置 TCP 通道却设置了代理时抛出异常
        /// </summary>
        /// <returns>生成的连接选项</returns>
        public IMqttClientOptions Build()
        {
            if (_tcpOptions == null && _webSocketOptions == null)
            {
                throw new InvalidOperationException("A channel must be set.");
            }

            if (_tlsParameters != null)
            {
                if (_tlsParameters?.UseTls == true)
                {
                    var tlsOptions = new MqttClientTlsOptions
                    {
                        UseTls = true,
                        SslProtocol = _tlsParameters.SslProtocol,
                        AllowUntrustedCertificates = _tlsParameters.AllowUntrustedCertificates,
#if WINDOWS_UWP
                        Certificates = _tlsParameters.Certificates?.Select(c => c.ToArray()).ToList(),
#else
                        Certificates = _tlsParameters.Certificates?.ToList(),
#endif
#pragma warning disable CS0618 // Type or member is obsolete
                        CertificateValidationCallback = _tlsParameters.CertificateValidationCallback,
#pragma warning restore CS0618 // Type or member is obsolete
#if NETCOREAPP3_1
                        ApplicationProtocols = _tlsParameters.ApplicationProtocols,
#endif
                        CertificateValidationHandler = _tlsParameters.CertificateValidationHandler,
                        IgnoreCertificateChainErrors = _tlsParameters.IgnoreCertificateChainErrors,
                        IgnoreCertificateRevocationErrors = _tlsParameters.IgnoreCertificateRevocationErrors
                    };

                    if (_tcpOptions != null)
                    {
                        _tcpOptions.TlsOptions = tlsOptions;
                    }
                    else if (_webSocketOptions != null)
                    {
                        _webSocketOptions.TlsOptions = tlsOptions;
                    }
                }
            }

            if (_proxyOptions != null)
            {
                if (_webSocketOptions == null)
                {
                    throw new InvalidOperationException("Proxies are only supported for WebSocket connections.");
                }

                _webSocketOptions.ProxyOptions = _proxyOptions;
            }

            _options.ChannelOptions = (IMqttClientChannelOptions)_tcpOptions ?? _webSocketOptions;

            return _options;
        }
    }
}
