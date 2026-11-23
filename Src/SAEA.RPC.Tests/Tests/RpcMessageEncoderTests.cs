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
*命名空间：SAEA.RPC.Tests
*文件名： RpcMessageEncoderTests
*版本号： net10.0
*唯一标识：64b12095-3b57-448c-a19a-3b8c596fa1f1
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 17:05:00
*描述：RPC 出站编码用例：线格式布局、最小帧与 UTF-8 长度口径
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 17:05:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖 RPC 出站编码的线格式布局、最小帧与 UTF-8 长度口径
*
*****************************************************************************/
using System;
using System.Buffers.Binary;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.RPC.Model;
using SAEA.RPC.Net;
using SAEA.RPC.Tests.Tests.Support;

namespace SAEA.RPC.Tests
{
    /// <summary>
    /// RPC 编码器：把 <see cref="RSocketMsg"/> 写成 <c>[1B type][4B total][8B sequence][4B sLen][service][4B mLen][method][data]</c>。
    /// </summary>
    [TestClass]
    public class RpcMessageEncoderTests
    {
        /// <summary>
        /// 带服务名、方法名与数据段时，各字段的偏移与长度字段口径必须与线格式一致。
        /// </summary>
        [TestMethod]
        public void RpcMessageEncoder_RequestWithData_MatchesWireLayout()
        {
            var payload = RpcFrameBuilder.Payload(5);
            var msg = RpcFrameBuilder.Request(0x0102030405060708L, "HelloService", "SayHello", payload);

            var frame = RpcMessageEncoder.Encode(msg);

            var serviceBytes = Encoding.UTF8.GetBytes("HelloService");
            var methodBytes = Encoding.UTF8.GetBytes("SayHello");
            var total = 4 + 8 + 4 + serviceBytes.Length + 4 + methodBytes.Length + payload.Length;

            Assert.AreEqual(1 + total, frame.Length, "整帧长度应为 1 + 长度字段值。");
            Assert.AreEqual((byte)RSocketMsgType.Request, frame[0], "首字节应为消息类型。");
            Assert.AreEqual((uint)total, BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(1, 4)), "长度字段应覆盖类型字节之后的全部内容。");
            Assert.AreEqual(0x0102030405060708L, BinaryPrimitives.ReadInt64LittleEndian(frame.AsSpan(5, 8)), "序号字段应小端写入。");
            Assert.AreEqual((uint)serviceBytes.Length, BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(13, 4)), "服务名长度应按字节数写入。");

            var offset = 17;
            CollectionAssert.AreEqual(serviceBytes, Slice(frame, offset, serviceBytes.Length), "服务名应按 UTF-8 原样写入。");
            offset += serviceBytes.Length;

            Assert.AreEqual((uint)methodBytes.Length, BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(offset, 4)), "方法名长度应按字节数写入。");
            offset += 4;

            CollectionAssert.AreEqual(methodBytes, Slice(frame, offset, methodBytes.Length), "方法名应按 UTF-8 原样写入。");
            offset += methodBytes.Length;

            CollectionAssert.AreEqual(payload, Slice(frame, offset, payload.Length), "数据段应原样写入。");
            Assert.AreEqual(frame.Length, offset + payload.Length, "数据段之后不得有多余字节。");
        }

        /// <summary>
        /// 空服务名、空方法名与无数据段时，长度字段应恰为固定字段总长 20。
        /// </summary>
        [TestMethod]
        public void RpcMessageEncoder_EmptyFields_WritesMinimalTotal()
        {
            var frame = RpcMessageEncoder.Encode(RpcFrameBuilder.Ping());

            Assert.AreEqual(21, frame.Length, "最小帧整帧长度应为 21。");
            Assert.AreEqual(20u, BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(1, 4)), "最小帧长度字段应为 20。");
            Assert.AreEqual(0L, BinaryPrimitives.ReadInt64LittleEndian(frame.AsSpan(5, 8)), "未设置序号时应写 0。");
            Assert.AreEqual(0u, BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(13, 4)), "空服务名的长度字段应为 0。");
            Assert.AreEqual(0u, BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(17, 4)), "空方法名的长度字段应为 0。");
        }

        /// <summary>
        /// 中文服务名与方法名应按 UTF-8 字节数写长度，而不是字符数。
        /// </summary>
        [TestMethod]
        public void RpcMessageEncoder_Utf8Names_WritesByteLength()
        {
            var msg = RpcFrameBuilder.Request(1, "服务", "方法", null);

            var frame = RpcMessageEncoder.Encode(msg);

            var serviceBytes = Encoding.UTF8.GetBytes("服务");
            var methodBytes = Encoding.UTF8.GetBytes("方法");

            Assert.AreEqual((uint)serviceBytes.Length, BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(13, 4)), "服务名长度应按 UTF-8 字节数写入。");

            var methodLengthOffset = 17 + serviceBytes.Length;
            Assert.AreEqual((uint)methodBytes.Length, BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(methodLengthOffset, 4)), "方法名长度应按 UTF-8 字节数写入。");

            CollectionAssert.AreEqual(serviceBytes, Slice(frame, 17, serviceBytes.Length), "服务名应按 UTF-8 原样写入。");
            CollectionAssert.AreEqual(methodBytes, Slice(frame, methodLengthOffset + 4, methodBytes.Length), "方法名应按 UTF-8 原样写入。");
        }

        /// <summary>
        /// 兼容入口对 null 消息应直接拒绝。
        /// </summary>
        [TestMethod]
        public void RpcMessageEncoder_NullMessage_Throws()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => RpcMessageEncoder.Encode(null));
        }

        /// <summary>
        /// 兼容入口 <see cref="RpcCoder.Encode(RSocketMsg)"/> 应与编码器输出完全一致。
        /// </summary>
        [TestMethod]
        public void RpcCoder_Encode_DelegatesToEncoder()
        {
            var msg = RpcFrameBuilder.Request(5, "HelloService", "SayHello", RpcFrameBuilder.Payload(2));

            var byCoder = new RpcCoder().Encode(msg);
            var byEncoder = RpcMessageEncoder.Encode(msg);

            CollectionAssert.AreEqual(byEncoder, byCoder, "RpcCoder.Encode 应委托给 RpcMessageEncoder。");
        }

        private static byte[] Slice(byte[] source, int offset, int count)
        {
            var result = new byte[count];
            Buffer.BlockCopy(source, offset, result, 0, count);
            return result;
        }
    }
}