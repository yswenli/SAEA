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
*唯一标识：e3b703dc-2d76-4769-8642-bd2fecce95f0
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MQTT服务端类
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MQTT服务端类
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
    public class MqttConnectionValidatorContext
    {
        private readonly MqttConnectPacket _connectPacket;
        private readonly IMqttChannelAdapter _clientAdapter;

        public MqttConnectionValidatorContext(MqttConnectPacket connectPacket, IMqttChannelAdapter clientAdapter, IDictionary<object, object> sessionItems)
        {
            _connectPacket = connectPacket;
            _clientAdapter = clientAdapter ?? throw new ArgumentNullException(nameof(clientAdapter));
            SessionItems = sessionItems;
        }

        public string ClientId => _connectPacket.ClientId;

        public string Endpoint => _clientAdapter.Endpoint;

        public bool IsSecureConnection => _clientAdapter.IsSecureConnection;

        public X509Certificate2 ClientCertificate => _clientAdapter.ClientCertificate;

        public MqttProtocolVersion ProtocolVersion => _clientAdapter.PacketFormatterAdapter.ProtocolVersion;

        public string Username => _connectPacket?.Username;

        public byte[] RawPassword => _connectPacket?.Password;

        public string Password => Encoding.UTF8.GetString(RawPassword ?? new byte[0]);

        public MqttApplicationMessage WillMessage => _connectPacket?.WillMessage;

        public bool? CleanSession => _connectPacket?.CleanSession;

        public ushort? KeepAlivePeriod => _connectPacket?.KeepAlivePeriod;

        public List<MqttUserProperty> UserProperties => _connectPacket?.Properties?.UserProperties;

        public byte[] AuthenticationData => _connectPacket?.Properties?.AuthenticationData;

        public string AuthenticationMethod => _connectPacket?.Properties?.AuthenticationMethod;

        public uint? MaximumPacketSize => _connectPacket?.Properties?.MaximumPacketSize;

        public ushort? ReceiveMaximum => _connectPacket?.Properties?.ReceiveMaximum;

        public ushort? TopicAliasMaximum => _connectPacket?.Properties?.TopicAliasMaximum;

        public bool? RequestProblemInformation => _connectPacket?.Properties?.RequestProblemInformation;

        public bool? RequestResponseInformation => _connectPacket?.Properties?.RequestResponseInformation;

        public uint? SessionExpiryInterval => _connectPacket?.Properties?.SessionExpiryInterval;

        public uint? WillDelayInterval => _connectPacket?.Properties?.WillDelayInterval;

        /// <summary>
        /// Gets or sets a key/value collection that can be used to share data within the scope of this session.
        /// </summary>
        public IDictionary<object, object> SessionItems { get; }

        /// <summary>
        /// This is used for MQTTv3 only.
        /// </summary>
        [Obsolete("Use ReasonCode instead. It is MQTTv5 only but will be converted to a valid ReturnCode.")]
        public MqttConnectReturnCode ReturnCode
        {
            get => new MqttConnectReasonCodeConverter().ToConnectReturnCode(ReasonCode);
            set => ReasonCode = new MqttConnectReasonCodeConverter().ToConnectReasonCode(value);
        }

        /// <summary>
        /// This is used for MQTTv5 only. When a MQTTv3 client connects the enum value must be one which is
        /// also supported in MQTTv3. Otherwise the connection attempt will fail because not all codes can be
        /// converted properly.
        /// </summary>
        public MqttConnectReasonCode ReasonCode { get; set; } = MqttConnectReasonCode.Success;

        public List<MqttUserProperty> ResponseUserProperties { get; set; }

        public byte[] ResponseAuthenticationData { get; set; }

        public string AssignedClientIdentifier { get; set; }

        public string ReasonString { get; set; }
    }
}
