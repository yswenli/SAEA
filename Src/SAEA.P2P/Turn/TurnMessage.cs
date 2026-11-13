/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.P2P.Turn
*文件名： TurnMessage
*版本号： v26.11.5.1
*唯一标识：e49602f0-62d9-435f-8480-3a64905218ff
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 22:30:00
*描述：TURN 方法判定、必选属性校验与消息构造读取助手
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 20:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 ADDITIONAL-ADDRESS-FAMILY 解析
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;

using SAEA.P2P.Stun;

namespace SAEA.P2P.Turn
{
    /// <summary>
    /// TURN 消息助手：判定 TURN 方法集、识别本实现明确不支持的必选属性、构造请求与指示、读取常用属性。
    /// TURN 报文本身仍是 STUN 报文，因此统一复用 <see cref="StunMessage"/> 与 <see cref="StunAttributeType"/>。
    /// </summary>
    public static class TurnMessage
    {
        /// <summary>
        /// REQUESTED-TRANSPORT 与地址族属性的值区固定长度
        /// </summary>
        private const int FixedValueLength = 4;

        /// <summary>
        /// TCP 协议号
        /// </summary>
        private const byte TcpProtocol = 6;

        /// <summary>
        /// UDP 协议号
        /// </summary>
        private const byte UdpProtocol = 17;

        /// <summary>
        /// 无未知属性时返回的空列表，避免每次分配
        /// </summary>
        private static readonly ushort[] EmptyTypes = new ushort[0];

        /// <summary>
        /// 判断方法是否属于 TURN 方法集
        /// </summary>
        /// <param name="method">STUN 方法</param>
        /// <returns>属于 TURN 方法集时返回 true</returns>
        public static bool IsTurnMethod(StunMessageType method)
        {
            switch (method)
            {
                case StunMessageType.Allocate:
                case StunMessageType.Refresh:
                case StunMessageType.CreatePermission:
                case StunMessageType.ChannelBind:
                case StunMessageType.Send:
                case StunMessageType.Data:
                case StunMessageType.Connect:
                case StunMessageType.ConnectionBind:
                case StunMessageType.ConnectionAttempt:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 判断属性是否属于本实现明确不支持的必选属性。
        /// EVEN-PORT 与 RESERVATION-TOKEN 不实现端口保留机制，按 RFC 8489 回 420；
        /// DONT-FRAGMENT 不在此列，按 RFC 8656 在 UDP 中继时忽略。
        /// </summary>
        /// <param name="type">属性类型原值</param>
        /// <returns>不予支持时返回 true</returns>
        public static bool IsUnsupportedAttribute(ushort type)
        {
            return type == (ushort)StunAttributeType.EvenPort
                || type == (ushort)StunAttributeType.ReservationToken;
        }

        /// <summary>
        /// 汇总请求中应回 420 的必选属性：本实现不认识的必选属性与本实现明确不支持的必选属性
        /// </summary>
        /// <param name="request">请求</param>
        /// <returns>属性类型列表，按报文顺序去重；没有时为空列表</returns>
        /// <exception cref="ArgumentNullException">request 为 null</exception>
        public static IReadOnlyList<ushort> GetUnsupportedAttributes(StunMessage request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var result = new List<ushort>();
            var unknown = request.UnknownComprehensionRequiredTypes;
            for (var i = 0; i < unknown.Count; i++)
            {
                result.Add(unknown[i]);
            }

            var attributes = request.Attributes;
            for (var i = 0; i < attributes.Count; i++)
            {
                var attribute = attributes[i];
                if (attribute.IsComprehensionRequired && IsUnsupportedAttribute(attribute.Type) && !result.Contains(attribute.Type))
                {
                    result.Add(attribute.Type);
                }
            }

            return result;
        }

        /// <summary>
        /// 校验请求里是否存在应回 420 的必选属性；DONT-FRAGMENT 等被忽略的属性不影响结果
        /// </summary>
        /// <param name="request">请求</param>
        /// <param name="errorCode">失败时的错误码；成功时为 0</param>
        /// <param name="unknownAttributes">应写入 UNKNOWN-ATTRIBUTES 的类型列表；成功时为空</param>
        /// <returns>无需回 420 时返回 true</returns>
        /// <exception cref="ArgumentNullException">request 为 null</exception>
        public static bool TryValidateRequest(StunMessage request, out int errorCode, out IReadOnlyList<ushort> unknownAttributes)
        {
            var unsupported = GetUnsupportedAttributes(request);
            if (unsupported.Count > 0)
            {
                errorCode = TurnErrorCode.UnknownAttribute;
                unknownAttributes = unsupported;
                return false;
            }

            errorCode = 0;
            unknownAttributes = EmptyTypes;
            return true;
        }

        /// <summary>
        /// 构造 Allocate 请求
        /// </summary>
        /// <param name="requestedTransport">请求的中继传输协议号，只接受 6 与 17</param>
        /// <param name="lifetime">请求的生命周期，单位秒；为 null 时不携带 LIFETIME</param>
        /// <returns>Allocate 请求</returns>
        /// <exception cref="ArgumentOutOfRangeException">协议号不是 6 或 17</exception>
        public static StunMessage CreateAllocate(byte requestedTransport, uint? lifetime = null)
        {
            if (requestedTransport != TcpProtocol && requestedTransport != UdpProtocol)
            {
                throw new ArgumentOutOfRangeException(nameof(requestedTransport), "中继传输只支持 TCP(6) 与 UDP(17)");
            }

            var attributes = new List<StunAttribute>
            {
                CreateTransportAttribute(requestedTransport)
            };
            if (lifetime.HasValue)
            {
                attributes.Add(StunAttributeCodec.CreateUInt32(StunAttributeType.Lifetime, lifetime.Value));
            }

            return CreateMessage(StunMessageType.Allocate, StunClass.Request, attributes);
        }

        /// <summary>
        /// 构造 Refresh 请求
        /// </summary>
        /// <param name="lifetime">请求的生命周期，单位秒；0 表示立即删除分配</param>
        /// <returns>Refresh 请求</returns>
        public static StunMessage CreateRefresh(uint lifetime)
        {
            return CreateMessage(
                StunMessageType.Refresh,
                StunClass.Request,
                new[] { StunAttributeCodec.CreateUInt32(StunAttributeType.Lifetime, lifetime) });
        }

        /// <summary>
        /// 构造 CreatePermission 请求
        /// </summary>
        /// <param name="peerAddresses">对端地址列表，至少一个</param>
        /// <returns>CreatePermission 请求</returns>
        /// <exception cref="ArgumentNullException">peerAddresses 为 null</exception>
        /// <exception cref="ArgumentException">列表为空或含 null 元素</exception>
        public static StunMessage CreateCreatePermission(IEnumerable<IPEndPoint> peerAddresses)
        {
            if (peerAddresses == null)
            {
                throw new ArgumentNullException(nameof(peerAddresses));
            }

            var message = new StunMessage(StunMessageType.CreatePermission, StunClass.Request);
            var transactionId = message.TransactionId.ToArray();
            var attributes = new List<StunAttribute>();
            foreach (var peerAddress in peerAddresses)
            {
                if (peerAddress == null)
                {
                    throw new ArgumentException("对端地址不能为 null", nameof(peerAddresses));
                }
                attributes.Add(StunAddressCodec.EncodeAddress(StunAttributeType.XorPeerAddress, peerAddress, true, transactionId));
            }

            if (attributes.Count == 0)
            {
                throw new ArgumentException("至少需要一个对端地址", nameof(peerAddresses));
            }

            message.SetAttributes(attributes);
            return message;
        }

        /// <summary>
        /// 构造 ChannelBind 请求
        /// </summary>
        /// <param name="channelNumber">通道号，取值 0x4000 到 0x4FFF</param>
        /// <param name="peerAddress">对端地址</param>
        /// <returns>ChannelBind 请求</returns>
        /// <exception cref="ArgumentOutOfRangeException">通道号不合法</exception>
        /// <exception cref="ArgumentNullException">peerAddress 为 null</exception>
        public static StunMessage CreateChannelBind(ushort channelNumber, IPEndPoint peerAddress)
        {
            if (!ChannelData.IsValidChannelNumber(channelNumber))
            {
                throw new ArgumentOutOfRangeException(nameof(channelNumber), "通道号必须落在 0x4000 到 0x4FFF 之间");
            }
            if (peerAddress == null)
            {
                throw new ArgumentNullException(nameof(peerAddress));
            }

            var message = new StunMessage(StunMessageType.ChannelBind, StunClass.Request);
            var transactionId = message.TransactionId.ToArray();
            message.SetAttributes(new[]
            {
                CreateChannelNumberAttribute(channelNumber),
                StunAddressCodec.EncodeAddress(StunAttributeType.XorPeerAddress, peerAddress, true, transactionId)
            });
            return message;
        }

        /// <summary>
        /// 构造 Connect 请求，用于 TURN over TCP
        /// </summary>
        /// <param name="peerAddress">对端地址</param>
        /// <returns>Connect 请求</returns>
        /// <exception cref="ArgumentNullException">peerAddress 为 null</exception>
        public static StunMessage CreateConnect(IPEndPoint peerAddress)
        {
            if (peerAddress == null)
            {
                throw new ArgumentNullException(nameof(peerAddress));
            }

            var message = new StunMessage(StunMessageType.Connect, StunClass.Request);
            var transactionId = message.TransactionId.ToArray();
            message.SetAttributes(new[]
            {
                StunAddressCodec.EncodeAddress(StunAttributeType.XorPeerAddress, peerAddress, true, transactionId)
            });
            return message;
        }

        /// <summary>
        /// 构造 ConnectionBind 请求，用于 TURN over TCP
        /// </summary>
        /// <param name="connectionId">服务端下发的连接 ID</param>
        /// <returns>ConnectionBind 请求</returns>
        public static StunMessage CreateConnectionBind(uint connectionId)
        {
            return CreateMessage(
                StunMessageType.ConnectionBind,
                StunClass.Request,
                new[] { StunAttributeCodec.CreateUInt32(StunAttributeType.ConnectionId, connectionId) });
        }

        /// <summary>
        /// 构造 ConnectionAttempt 指示，用于对端主动连入中继端口时告知客户端连接 ID
        /// </summary>
        /// <param name="connectionId">服务端分配的连接 ID</param>
        /// <param name="peerAddress">发起连入的对端地址</param>
        /// <returns>ConnectionAttempt 指示</returns>
        /// <exception cref="ArgumentNullException">peerAddress 为 null</exception>
        public static StunMessage CreateConnectionAttempt(uint connectionId, IPEndPoint peerAddress)
        {
            if (peerAddress == null)
            {
                throw new ArgumentNullException(nameof(peerAddress));
            }

            // XOR-PEER-ADDRESS 的异或掩码来自事务 ID，属性必须在消息建好之后再编码
            var message = new StunMessage(StunMessageType.ConnectionAttempt, StunClass.Indication);
            var transactionId = message.TransactionId.ToArray();
            message.SetAttributes(new[]
            {
                StunAttributeCodec.CreateUInt32(StunAttributeType.ConnectionId, connectionId),
                StunAddressCodec.EncodeAddress(StunAttributeType.XorPeerAddress, peerAddress, true, transactionId)
            });
            return message;
        }

        /// <summary>
        /// 构造 Send 指示，用 DATA 属性承载应用数据
        /// </summary>
        /// <param name="peerAddress">对端地址</param>
        /// <param name="data">应用数据</param>
        /// <returns>Send 指示</returns>
        /// <exception cref="ArgumentNullException">peerAddress 或 data 为 null</exception>
        public static StunMessage CreateSend(IPEndPoint peerAddress, byte[] data)
        {
            return CreateDataCarrier(StunMessageType.Send, StunClass.Indication, peerAddress, data);
        }

        /// <summary>
        /// 构造 Data 指示，把来自对端的数据交给客户端
        /// </summary>
        /// <param name="peerAddress">对端地址</param>
        /// <param name="data">应用数据</param>
        /// <returns>Data 指示</returns>
        /// <exception cref="ArgumentNullException">peerAddress 或 data 为 null</exception>
        public static StunMessage CreateData(IPEndPoint peerAddress, byte[] data)
        {
            return CreateDataCarrier(StunMessageType.Data, StunClass.Indication, peerAddress, data);
        }

        /// <summary>
        /// 读取 LIFETIME 属性
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="lifetime">生命周期，单位秒</param>
        /// <returns>存在且长度正确时返回 true</returns>
        public static bool TryGetLifetime(StunMessage message, out uint lifetime)
        {
            lifetime = 0;
            if (message == null)
            {
                return false;
            }

            StunAttribute attribute;
            if (!message.TryGet(StunAttributeType.Lifetime, out attribute))
            {
                return false;
            }

            return StunAttributeCodec.TryGetUInt32(attribute, out lifetime);
        }

        /// <summary>
        /// 读取 REQUESTED-TRANSPORT 属性
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="protocol">传输协议号，UDP 为 17、TCP 为 6</param>
        /// <returns>存在且长度正确时返回 true</returns>
        public static bool TryGetRequestedTransport(StunMessage message, out byte protocol)
        {
            protocol = 0;
            if (message == null)
            {
                return false;
            }

            StunAttribute attribute;
            if (!message.TryGet(StunAttributeType.RequestedTransport, out attribute))
            {
                return false;
            }

            var value = attribute.Value.Span;
            if (value.Length != FixedValueLength)
            {
                return false;
            }

            protocol = value[0];
            return true;
        }

        /// <summary>
        /// 读取 CHANNEL-NUMBER 属性
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="channelNumber">通道号</param>
        /// <returns>存在、长度正确且通道号合法时返回 true</returns>
        public static bool TryGetChannelNumber(StunMessage message, out ushort channelNumber)
        {
            channelNumber = 0;
            if (message == null)
            {
                return false;
            }

            StunAttribute attribute;
            if (!message.TryGet(StunAttributeType.ChannelNumber, out attribute))
            {
                return false;
            }

            var value = attribute.Value.Span;
            if (value.Length != FixedValueLength)
            {
                return false;
            }

            var channel = (ushort)((value[0] << 8) | value[1]);
            if (!ChannelData.IsValidChannelNumber(channel))
            {
                return false;
            }

            channelNumber = channel;
            return true;
        }

        /// <summary>
        /// 读取 CONNECTION-ID 属性
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="connectionId">连接 ID</param>
        /// <returns>存在且长度正确时返回 true</returns>
        public static bool TryGetConnectionId(StunMessage message, out uint connectionId)
        {
            connectionId = 0;
            if (message == null)
            {
                return false;
            }

            StunAttribute attribute;
            if (!message.TryGet(StunAttributeType.ConnectionId, out attribute))
            {
                return false;
            }

            return StunAttributeCodec.TryGetUInt32(attribute, out connectionId);
        }

        /// <summary>
        /// 读取 REQUESTED-ADDRESS-FAMILY 属性
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="family">地址族</param>
        /// <returns>存在、长度正确且地址族已知时返回 true</returns>
        public static bool TryGetRequestedAddressFamily(StunMessage message, out TurnAddressFamily family)
        {
            family = TurnAddressFamily.IPv4;
            if (message == null)
            {
                return false;
            }

            StunAttribute attribute;
            if (!message.TryGet(StunAttributeType.RequestedAddressFamily, out attribute))
            {
                return false;
            }

            var value = attribute.Value.Span;
            if (value.Length != FixedValueLength)
            {
                return false;
            }

            if (value[0] == (byte)TurnAddressFamily.IPv4)
            {
                family = TurnAddressFamily.IPv4;
                return true;
            }
            if (value[0] == (byte)TurnAddressFamily.IPv6)
            {
                family = TurnAddressFamily.IPv6;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 读取 ADDITIONAL-ADDRESS-FAMILY 属性
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="family">附加地址族</param>
        /// <returns>存在、长度正确且地址族已知时返回 true</returns>
        public static bool TryGetAdditionalAddressFamily(StunMessage message, out TurnAddressFamily family)
        {
            family = TurnAddressFamily.IPv4;
            if (message == null)
            {
                return false;
            }

            StunAttribute attribute;
            if (!message.TryGet(StunAttributeType.AdditionalAddressFamily, out attribute))
            {
                return false;
            }

            var value = attribute.Value.Span;
            if (value.Length != FixedValueLength)
            {
                return false;
            }

            if (value[0] == (byte)TurnAddressFamily.IPv4)
            {
                family = TurnAddressFamily.IPv4;
                return true;
            }
            if (value[0] == (byte)TurnAddressFamily.IPv6)
            {
                family = TurnAddressFamily.IPv6;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 读取 XOR-PEER-ADDRESS 属性
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="peerAddress">对端地址</param>
        /// <returns>存在且能解码时返回 true</returns>
        public static bool TryGetPeerAddress(StunMessage message, out IPEndPoint peerAddress)
        {
            return TryGetAddress(message, StunAttributeType.XorPeerAddress, out peerAddress);
        }

        /// <summary>
        /// 读取 XOR-RELAYED-ADDRESS 属性
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="relayedAddress">中继地址</param>
        /// <returns>存在且能解码时返回 true</returns>
        public static bool TryGetRelayedAddress(StunMessage message, out IPEndPoint relayedAddress)
        {
            return TryGetAddress(message, StunAttributeType.XorRelayedAddress, out relayedAddress);
        }

        /// <summary>
        /// 读取 DATA 属性承载的应用数据
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="data">应用数据；不存在时为空</param>
        /// <returns>存在时返回 true</returns>
        public static bool TryGetData(StunMessage message, out ReadOnlyMemory<byte> data)
        {
            data = ReadOnlyMemory<byte>.Empty;
            if (message == null)
            {
                return false;
            }

            StunAttribute attribute;
            if (!message.TryGet(StunAttributeType.Data, out attribute))
            {
                return false;
            }

            data = attribute.Value;
            return true;
        }

        /// <summary>
        /// 读取指定类型的异或地址属性
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="type">属性类型</param>
        /// <param name="endPoint">解码出的端点</param>
        /// <returns>存在且能解码时返回 true</returns>
        private static bool TryGetAddress(StunMessage message, StunAttributeType type, out IPEndPoint endPoint)
        {
            endPoint = null;
            if (message == null)
            {
                return false;
            }

            StunAttribute attribute;
            if (!message.TryGet(type, out attribute))
            {
                return false;
            }

            return StunAddressCodec.TryDecodeAddress(attribute, message.TransactionId.Span, out endPoint);
        }

        /// <summary>
        /// 构造携带 DATA 与 XOR-PEER-ADDRESS 的指示
        /// </summary>
        /// <param name="method">方法，Send 或 Data</param>
        /// <param name="class">消息类别</param>
        /// <param name="peerAddress">对端地址</param>
        /// <param name="data">应用数据</param>
        /// <returns>指示消息</returns>
        private static StunMessage CreateDataCarrier(StunMessageType method, StunClass @class, IPEndPoint peerAddress, byte[] data)
        {
            if (peerAddress == null)
            {
                throw new ArgumentNullException(nameof(peerAddress));
            }
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var message = new StunMessage(method, @class);
            var transactionId = message.TransactionId.ToArray();
            message.SetAttributes(new[]
            {
                StunAddressCodec.EncodeAddress(StunAttributeType.XorPeerAddress, peerAddress, true, transactionId),
                new StunAttribute(StunAttributeType.Data, data)
            });
            return message;
        }

        /// <summary>
        /// 用给定方法、类别与属性构造消息
        /// </summary>
        /// <param name="method">方法</param>
        /// <param name="class">消息类别</param>
        /// <param name="attributes">属性列表</param>
        /// <returns>消息</returns>
        private static StunMessage CreateMessage(StunMessageType method, StunClass @class, IEnumerable<StunAttribute> attributes)
        {
            var message = new StunMessage(method, @class);
            message.SetAttributes(attributes);
            return message;
        }

        /// <summary>
        /// 构造 REQUESTED-TRANSPORT 属性；值区为 4 字节，协议号占首字节，其余置零
        /// </summary>
        /// <param name="protocol">传输协议号</param>
        /// <returns>属性</returns>
        private static StunAttribute CreateTransportAttribute(byte protocol)
        {
            return new StunAttribute(StunAttributeType.RequestedTransport, new byte[] { protocol, 0, 0, 0 });
        }

        /// <summary>
        /// 构造 CHANNEL-NUMBER 属性；值区为 4 字节，通道号占前两字节，后两字节置零
        /// </summary>
        /// <param name="channelNumber">通道号</param>
        /// <returns>属性</returns>
        private static StunAttribute CreateChannelNumberAttribute(ushort channelNumber)
        {
            return new StunAttribute(
                StunAttributeType.ChannelNumber,
                new byte[] { (byte)(channelNumber >> 8), (byte)channelNumber, 0, 0 });
        }
    }
}