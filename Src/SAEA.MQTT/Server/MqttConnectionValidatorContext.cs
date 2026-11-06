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
*文件名： MqttConnectionValidatorContext
*版本号： v26.4.23.1
*唯一标识：b769b75f-bd20-427c-afac-4b4eeded37f1
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttConnectionValidatorContext接口
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
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using SAEA.MQTT.Adapter;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 连接校验上下文，暴露 CONNECT 报文的凭据、遗嘱与 MQTT 5.0 属性，并承载 CONNACK 的响应内容
    /// </summary>
    public class MqttConnectionValidatorContext
    {
        private readonly MqttConnectPacket _connectPacket;
        private readonly IMqttChannelAdapter _clientAdapter;

        /// <summary>
        /// 创建连接校验上下文
        /// </summary>
        /// <param name="connectPacket">客户端提交的 CONNECT 报文，可为 null 但相关属性随之返回 null</param>
        /// <param name="clientAdapter">客户端通道适配器，不可为 null</param>
        /// <param name="sessionItems">会话级键值集合，可为 null</param>
        public MqttConnectionValidatorContext(MqttConnectPacket connectPacket, IMqttChannelAdapter clientAdapter, IDictionary<object, object> sessionItems)
        {
            _connectPacket = connectPacket;
            _clientAdapter = clientAdapter ?? throw new ArgumentNullException(nameof(clientAdapter));
            SessionItems = sessionItems;
        }

        /// <summary>
        /// 客户端标识，服务端可改为分配值并通过 AssignedClientIdentifier 回传
        /// </summary>
        public string ClientId => _connectPacket.ClientId;

        /// <summary>
        /// 客户端连接的远端端点描述
        /// </summary>
        public string Endpoint => _clientAdapter.Endpoint;

        /// <summary>
        /// 该连接是否经 TLS 建立
        /// </summary>
        public bool IsSecureConnection => _clientAdapter.IsSecureConnection;

        /// <summary>
        /// 客户端证书，非双向 TLS 时为 null
        /// </summary>
        public X509Certificate2 ClientCertificate => _clientAdapter.ClientCertificate;

        /// <summary>
        /// 连接使用的 MQTT 协议版本
        /// </summary>
        public MqttProtocolVersion ProtocolVersion => _clientAdapter.PacketFormatterAdapter.ProtocolVersion;

        /// <summary>
        /// CONNECT 中的用户名，未提供时为 null
        /// </summary>
        public string Username => _connectPacket?.Username;

        /// <summary>
        /// CONNECT 中的原始密码字节，未提供时为 null
        /// </summary>
        public byte[] RawPassword => _connectPacket?.Password;

        /// <summary>
        /// 原始密码按 UTF-8 解码后的字符串，未提供密码时为空字符串
        /// </summary>
        public string Password => Encoding.UTF8.GetString(RawPassword ?? new byte[0]);

        /// <summary>
        /// CONNECT 中的遗嘱消息，未设置遗嘱时为 null
        /// </summary>
        public MqttApplicationMessage WillMessage => _connectPacket?.WillMessage;

        /// <summary>
        /// CONNECT 中的 CleanSession/CleanStart 标志
        /// </summary>
        public bool? CleanSession => _connectPacket?.CleanSession;

        /// <summary>
        /// 客户端声明的保活周期（秒）
        /// </summary>
        public ushort? KeepAlivePeriod => _connectPacket?.KeepAlivePeriod;

        /// <summary>
        /// CONNECT 中的 MQTT 5.0 用户属性
        /// </summary>
        public List<MqttUserProperty> UserProperties => _connectPacket?.Properties?.UserProperties;

        /// <summary>
        /// 增强认证的原始认证数据
        /// </summary>
        public byte[] AuthenticationData => _connectPacket?.Properties?.AuthenticationData;

        /// <summary>
        /// 增强认证的方法名
        /// </summary>
        public string AuthenticationMethod => _connectPacket?.Properties?.AuthenticationMethod;

        /// <summary>
        /// 客户端可接受的最大报文长度（字节）
        /// </summary>
        public uint? MaximumPacketSize => _connectPacket?.Properties?.MaximumPacketSize;

        /// <summary>
        /// 客户端可同时处理的 QoS 1/2 消息数量上限
        /// </summary>
        public ushort? ReceiveMaximum => _connectPacket?.Properties?.ReceiveMaximum;

        /// <summary>
        /// 客户端可接收的主题别名数量上限
        /// </summary>
        public ushort? TopicAliasMaximum => _connectPacket?.Properties?.TopicAliasMaximum;

        /// <summary>
        /// 客户端是否希望接收问题信息
        /// </summary>
        public bool? RequestProblemInformation => _connectPacket?.Properties?.RequestProblemInformation;

        /// <summary>
        /// 客户端是否希望接收响应信息
        /// </summary>
        public bool? RequestResponseInformation => _connectPacket?.Properties?.RequestResponseInformation;

        /// <summary>
        /// 客户端请求的会话过期间隔（秒）
        /// </summary>
        public uint? SessionExpiryInterval => _connectPacket?.Properties?.SessionExpiryInterval;

        /// <summary>
        /// 遗嘱延迟发布的间隔（秒）
        /// </summary>
        public uint? WillDelayInterval => _connectPacket?.Properties?.WillDelayInterval;

        /// <summary>
        /// CONNACK 的 Session Present 标志。CleanStart=1 时始终为 false。
        /// </summary>
        public bool IsSessionPresent { get; set; }

        /// <summary>
        /// 会话级键值集合，用于在会话范围内共享数据
        /// </summary>
        public IDictionary<object, object> SessionItems { get; }

        /// <summary>
        /// MQTT 3.1.1 的连接返回码，读写均转发到 ReasonCode 并做枚举转换
        /// </summary>
        [Obsolete("Use ReasonCode instead. It is MQTTv5 only but will be converted to a valid ReturnCode.")]
        public MqttConnectReturnCode ReturnCode
        {
            get => new MqttConnectReasonCodeConverter().ToConnectReturnCode(ReasonCode);
            set => ReasonCode = new MqttConnectReasonCodeConverter().ToConnectReasonCode(value);
        }

        /// <summary>
        /// 连接结果原因码，默认 Success；MQTT 3.1.1 客户端只能使用可转换为返回码的取值，否则连接失败
        /// </summary>
        public MqttConnectReasonCode ReasonCode { get; set; } = MqttConnectReasonCode.Success;

        /// <summary>
        /// 要写入 CONNACK 的 MQTT 5.0 用户属性
        /// </summary>
        public List<MqttUserProperty> ResponseUserProperties { get; set; }

        /// <summary>
        /// 要写入 CONNACK 的增强认证数据
        /// </summary>
        public byte[] ResponseAuthenticationData { get; set; }

        /// <summary>
        /// 服务端分配的客户端标识，仅在客户端留空且校验通过时使用
        /// </summary>
        public string AssignedClientIdentifier { get; set; }

        /// <summary>
        /// 要写入 CONNACK 的原因描述字符串
        /// </summary>
        public string ReasonString { get; set; }
    }
}
