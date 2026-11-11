/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.P2P.Tests.Turn
*文件名： TurnCodecTests
*版本号： net10.0
*唯一标识：5d729e74-8f45-4108-bc4c-0070d7b99736
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 23:00:00
*描述：TURN 属性、ChannelData 与错误码编解码验收
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 23:00:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 TURN 编解码用例
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Stun;
using SAEA.P2P.Turn;

namespace SAEA.P2P.Tests.Turn
{
    /// <summary>
    /// TURN 编解码：ChannelData 在 TCP 上的补齐与非法通道号丢弃、EVEN-PORT 与 DONT-FRAGMENT 的不同处理、
    /// 错误码与常用属性逐字节回环。
    /// </summary>
    [TestClass]
    public class TurnCodecTests
    {
        /// <summary>
        /// TCP 上 ChannelData 必须补齐到 4 字节倍数，长度字段只计应用数据
        /// </summary>
        [TestMethod]
        public void ChannelData_TCP补齐_长度为4倍数()
        {
            var payload = new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55 };

            var padded = ChannelData.Encode(0x4001, payload, true);
            var raw = ChannelData.Encode(0x4001, payload, false);

            Assert.AreEqual(12, padded.Length, "5 字节应用数据补齐后总长应为 12");
            Assert.AreEqual(0, padded.Length % 4, "TCP 上的报文总长必须是 4 的倍数");
            Assert.AreEqual(9, raw.Length, "不补齐时总长应为头部 4 字节加应用数据 5 字节");

            var declared = (padded[2] << 8) | padded[3];
            Assert.AreEqual(payload.Length, declared, "长度字段只计应用数据，不含补齐字节");

            int totalLength;
            Assert.IsTrue(ChannelData.TryGetTotalLength(padded, true, out totalLength), "补齐语义下应能读出总长");
            Assert.AreEqual(12, totalLength, "补齐语义下的总长应含补齐字节");

            ushort channelNumber;
            ReadOnlySpan<byte> decoded;
            int consumed;
            Assert.IsTrue(ChannelData.TryDecode(padded, true, out channelNumber, out decoded, out consumed), "补齐后的报文应能解析");
            Assert.AreEqual((ushort)0x4001, channelNumber, "通道号应保持不变");
            Assert.AreEqual(12, consumed, "解析应消费含补齐字节在内的全部字节");
            CollectionAssert.AreEqual(payload, decoded.ToArray(), "应用数据应逐字节一致");
        }

        /// <summary>
        /// 0x5000 以上的通道号属于其它协议复用区间，解析时必须静默丢弃而不是抛异常
        /// </summary>
        [TestMethod]
        public void ChannelData_0x5000以上通道号_静默丢弃()
        {
            var reserved = new byte[] { 0x50, 0x00, 0x00, 0x00 };
            ushort channelNumber;
            ReadOnlySpan<byte> payload;
            Assert.IsFalse(ChannelData.TryDecode(reserved, out channelNumber, out payload), "0x5000 通道号应被丢弃");

            var belowRange = new byte[] { 0x3F, 0xFF, 0x00, 0x00 };
            Assert.IsFalse(ChannelData.TryDecode(belowRange, out channelNumber, out payload), "0x3FFF 通道号应被丢弃");

            int totalLength;
            Assert.IsFalse(ChannelData.TryGetTotalLength(reserved, false, out totalLength), "非法通道号不应给出报文总长");

            var valid = new byte[] { 0x40, 0x00, 0x00, 0x00 };
            Assert.IsTrue(ChannelData.TryDecode(valid, out channelNumber, out payload), "0x4000 是合法通道号");
            Assert.AreEqual((ushort)0x4000, channelNumber, "应保留原通道号");
            Assert.AreEqual(0, payload.Length, "空载荷应解析为零长度");
        }

        /// <summary>
        /// 携带 EVEN-PORT 或 RESERVATION-TOKEN 的请求必须回 420 并列出对应属性
        /// </summary>
        [TestMethod]
        public void TurnMessage_EVEN_PORT_返回420()
        {
            var request = new StunMessage(StunMessageType.Allocate, StunClass.Request);
            request.SetAttributes(new[]
            {
                new StunAttribute(StunAttributeType.EvenPort, new byte[] { 0x80 }),
                new StunAttribute(StunAttributeType.ReservationToken, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 })
            });

            int errorCode;
            IReadOnlyList<ushort> unknownAttributes;
            Assert.IsFalse(TurnMessage.TryValidateRequest(request, out errorCode, out unknownAttributes), "端口保留机制不实现，校验应失败");
            Assert.AreEqual(TurnErrorCode.UnknownAttribute, errorCode, "不支持必选属性应回 420");
            CollectionAssert.Contains(
                new List<ushort>(unknownAttributes),
                (ushort)StunAttributeType.EvenPort,
                "UNKNOWN-ATTRIBUTES 应列出 EVEN-PORT");
            CollectionAssert.Contains(
                new List<ushort>(unknownAttributes),
                (ushort)StunAttributeType.ReservationToken,
                "UNKNOWN-ATTRIBUTES 应列出 RESERVATION-TOKEN");
            Assert.AreEqual(
                "Unknown Attribute",
                TurnErrorCode.GetReason(errorCode),
                "420 的默认原因短语应与 RFC 一致");
        }

        /// <summary>
        /// DONT-FRAGMENT 在 UDP 中继时必须忽略，不得按「不认识」回 420
        /// </summary>
        [TestMethod]
        public void TurnMessage_DONT_FRAGMENT_不返回420()
        {
            var request = TurnMessage.CreateAllocate(17);
            var attributes = new List<StunAttribute>();
            for (var i = 0; i < request.Attributes.Count; i++)
            {
                attributes.Add(request.Attributes[i]);
            }
            attributes.Add(StunAttributeCodec.CreateEmpty(StunAttributeType.DontFragment));
            request.SetAttributes(attributes);

            int errorCode;
            IReadOnlyList<ushort> unknownAttributes;
            Assert.IsTrue(TurnMessage.TryValidateRequest(request, out errorCode, out unknownAttributes), "DONT-FRAGMENT 应被忽略而不是判为不支持");
            Assert.AreEqual(0, errorCode, "通过校验时不应带错误码");
            Assert.AreEqual(0, unknownAttributes.Count, "通过校验时不应列出未知属性");
            Assert.IsFalse(TurnMessage.IsUnsupportedAttribute((ushort)StunAttributeType.DontFragment), "DONT-FRAGMENT 不属于不支持属性");
        }

        /// <summary>
        /// ERROR-CODE 的 class、number 与原因短语应能编码后原样解析回来
        /// </summary>
        [TestMethod]
        public void TurnMessage_ERROR_CODE_编码与解析回环()
        {
            var message = new StunMessage(StunMessageType.Allocate, StunClass.ErrorResponse);
            message.SetAttributes(new[] { TurnErrorCode.Create(TurnErrorCode.InsufficientCapacity, "容量不足") });

            var attribute = message.Attributes[0];
            var value = attribute.Value.ToArray();
            Assert.AreEqual(0, value[0], "ERROR-CODE 前两字节保留位应为 0");
            Assert.AreEqual(0, value[1], "ERROR-CODE 前两字节保留位应为 0");
            Assert.AreEqual(5, value[2], "错误类别应为 5");
            Assert.AreEqual(8, value[3], "错误号应为 8");

            StunMessage parsed;
            StunParseError error;
            Assert.IsTrue(StunMessage.TryParse(message.ToArray(), out parsed, out error), "错误应答应能重新解析");
            Assert.AreEqual(StunClass.ErrorResponse, parsed.Class, "解析后类别应保持错误应答");

            int code;
            string reason;
            Assert.IsTrue(TurnErrorCode.TryGet(parsed, out code, out reason), "应读出错误码");
            Assert.AreEqual(TurnErrorCode.InsufficientCapacity, code, "错误码应为 508");
            Assert.AreEqual("容量不足", reason, "原因短语应逐字还原");
        }

        /// <summary>
        /// ChannelBind 请求中的通道号与对端地址应能编码后原样解析回来
        /// </summary>
        [TestMethod]
        public void TurnMessage_ChannelBind回环_通道号与对端地址一致()
        {
            var peerAddress = new IPEndPoint(IPAddress.Parse("203.0.113.7"), 50000);
            var request = TurnMessage.CreateChannelBind(0x4ABC, peerAddress);

            StunMessage parsed;
            StunParseError error;
            Assert.IsTrue(StunMessage.TryParse(request.ToArray(), out parsed, out error), "ChannelBind 请求应能重新解析");
            Assert.AreEqual(StunMessageType.ChannelBind, parsed.Method, "方法应为 ChannelBind");
            Assert.AreEqual(StunClass.Request, parsed.Class, "类别应为请求");

            ushort channelNumber;
            Assert.IsTrue(TurnMessage.TryGetChannelNumber(parsed, out channelNumber), "应读出通道号");
            Assert.AreEqual((ushort)0x4ABC, channelNumber, "通道号应逐字节还原");

            IPEndPoint decodedPeer;
            Assert.IsTrue(TurnMessage.TryGetPeerAddress(parsed, out decodedPeer), "应读出对端地址");
            Assert.AreEqual(peerAddress, decodedPeer, "异或编码的对端地址应原样还原");
        }
    }
}