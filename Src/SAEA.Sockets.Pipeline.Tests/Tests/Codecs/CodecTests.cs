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
*命名空间：SAEA.Sockets.Pipeline.Tests.Tests.Codecs
*文件名： CodecTests
*版本号： v26.11.5.1
*唯一标识：bf2421f5-a089-471d-8235-447ba3ef15d9
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：CodecTests
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：CodecTests
*
*****************************************************************************/
using System;
using System.Buffers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProtoBuf;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Codecs
{
    /// <summary>
    /// 两级编解码器：信封与实体往返、边界值、错误分支与帧内存不共享。
    /// </summary>
    [TestClass]
    public class CodecTests
    {
        private sealed class JsonPoco
        {
            public string Name { get; set; }
            public int Count { get; set; }
            public long Big { get; set; }
        }

        [ProtoContract]
        private sealed class ProtoPoco
        {
            [ProtoMember(1)]
            public string Name { get; set; }

            [ProtoMember(2)]
            public int Count { get; set; }

            [ProtoMember(3)]
            public long Big { get; set; }
        }

        /// <summary>
        /// 信封编码只写出负载内容，解码后类型码与负载原样还原
        /// </summary>
        [TestMethod]
        public void EnvelopeCodec_RoundTrip_PreservesTypeAndPayload()
        {
            var codec = new EnvelopeCodec();
            var payload = new byte[] { 0x10, 0x20, 0x30 };
            var writer = new ArrayBufferWriter<byte>();

            codec.Encode(new Envelope { Type = 7, Payload = payload }, writer);

            CollectionAssert.AreEqual(payload, writer.WrittenSpan.ToArray());

            var frame = new byte[1 + payload.Length];
            frame[0] = 7;
            payload.CopyTo(frame, 1);

            var envelope = (Envelope)codec.Decode(frame);

            Assert.AreEqual(7, envelope.Type);
            CollectionAssert.AreEqual(payload, envelope.Payload);
        }

        /// <summary>
        /// 类型码边界值 0 与 255 解码后仍保持原值且负载完整
        /// </summary>
        [TestMethod]
        public void EnvelopeCodec_TypeBoundaries_ZeroAnd255_RoundTrip()
        {
            var codec = new EnvelopeCodec();

            foreach (var type in new[] { 0, 255 })
            {
                var frame = new byte[] { (byte)type, 0xAA };
                var envelope = (Envelope)codec.Decode(frame);

                Assert.AreEqual(type, envelope.Type);
                CollectionAssert.AreEqual(new byte[] { 0xAA }, envelope.Payload);
            }
        }

        /// <summary>
        /// 只有类型字节的帧解码出长度为 0 的非 null 负载数组
        /// </summary>
        [TestMethod]
        public void EnvelopeCodec_Decode_EmptyPayload_ReturnsEmptyArray()
        {
            var codec = new EnvelopeCodec();

            var envelope = (Envelope)codec.Decode(new byte[] { 3 });

            Assert.AreEqual(3, envelope.Type);
            Assert.IsNotNull(envelope.Payload);
            Assert.AreEqual(0, envelope.Payload.Length);
        }

        /// <summary>
        /// 空帧解码抛 FrameProtocolException
        /// </summary>
        [TestMethod]
        public void EnvelopeCodec_Decode_EmptyFrame_ThrowsFrameProtocolException()
        {
            var codec = new EnvelopeCodec();

            Assert.ThrowsException<FrameProtocolException>(
                () => codec.Decode(ReadOnlyMemory<byte>.Empty));
        }

        /// <summary>
        /// 解码后的负载是独立副本，改写原始帧不影响它
        /// </summary>
        [TestMethod]
        public void EnvelopeCodec_Payload_DoesNotShareMemoryWithFrame()
        {
            var codec = new EnvelopeCodec();
            var frame = new byte[] { 1, 10, 20, 30 };

            var envelope = (Envelope)codec.Decode(frame);
            frame[1] = 0xFF;
            frame[2] = 0xFF;
            frame[3] = 0xFF;

            CollectionAssert.AreEqual(new byte[] { 10, 20, 30 }, envelope.Payload);
        }

        /// <summary>
        /// 编码非 Envelope 消息抛 InvalidOperationException
        /// </summary>
        [TestMethod]
        public void EnvelopeCodec_Encode_NonEnvelope_ThrowsInvalidOperationException()
        {
            var codec = new EnvelopeCodec();

            Assert.ThrowsException<InvalidOperationException>(
                () => codec.Encode(new JsonPoco(), new ArrayBufferWriter<byte>()));
        }

        /// <summary>
        /// JSON 编解码往返后字符串、int 与 long 字段值与原文一致
        /// </summary>
        [TestMethod]
        public void JsonMessageCodec_RoundTrip_RestoresEntity()
        {
            var codec = new JsonMessageCodec();
            var entity = new JsonPoco { Name = "管道", Count = 42, Big = 9000000000L };

            var payload = codec.Encode(entity);
            var restored = (JsonPoco)codec.Decode(payload, typeof(JsonPoco));

            Assert.AreEqual("管道", restored.Name);
            Assert.AreEqual(42, restored.Count);
            Assert.AreEqual(9000000000L, restored.Big);
        }

        /// <summary>
        /// 空负载解码返回字段为默认值的新实例而非 null
        /// </summary>
        [TestMethod]
        public void JsonMessageCodec_Decode_EmptyPayload_ReturnsNewInstance()
        {
            var codec = new JsonMessageCodec();

            var restored = (JsonPoco)codec.Decode(ReadOnlyMemory<byte>.Empty, typeof(JsonPoco));

            Assert.IsNotNull(restored);
            Assert.IsNull(restored.Name);
            Assert.AreEqual(0, restored.Count);
        }

        /// <summary>
        /// 目标类型传 null 时解码抛 ArgumentNullException
        /// </summary>
        [TestMethod]
        public void JsonMessageCodec_Decode_NullTargetType_ThrowsArgumentNullException()
        {
            var codec = new JsonMessageCodec();

            Assert.ThrowsException<ArgumentNullException>(
                () => codec.Decode(new byte[] { 0x7B, 0x7D }, null));
        }

        /// <summary>
        /// Protobuf 编解码往返后字符串、int 与 long 字段值与原文一致
        /// </summary>
        [TestMethod]
        public void ProtobufMessageCodec_RoundTrip_RestoresEntity()
        {
            var codec = new ProtobufMessageCodec();
            var entity = new ProtoPoco { Name = "管道", Count = 42, Big = 9000000000L };

            var payload = codec.Encode(entity);
            var restored = (ProtoPoco)codec.Decode(payload, typeof(ProtoPoco));

            Assert.AreEqual("管道", restored.Name);
            Assert.AreEqual(42, restored.Count);
            Assert.AreEqual(9000000000L, restored.Big);
        }

        /// <summary>
        /// 原始拷贝编解码保留类型码与负载，且负载为独立副本
        /// </summary>
        [TestMethod]
        public void RawCopyCodec_RoundTrip_PreservesTypeAndPayload()
        {
            var codec = new RawCopyCodec();
            var payload = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var writer = new ArrayBufferWriter<byte>();

            codec.Encode(new Envelope { Type = 200, Payload = payload }, writer);

            CollectionAssert.AreEqual(payload, writer.WrittenSpan.ToArray());

            var frame = new byte[1 + payload.Length];
            frame[0] = 200;
            payload.CopyTo(frame, 1);

            var envelope = (Envelope)codec.Decode(frame);

            Assert.AreEqual(200, envelope.Type);
            CollectionAssert.AreEqual(payload, envelope.Payload);

            frame[1] = 0xFF;
            CollectionAssert.AreEqual(payload, envelope.Payload);
        }

        /// <summary>
        /// 原始拷贝编解码遇到空帧抛 FrameProtocolException
        /// </summary>
        [TestMethod]
        public void RawCopyCodec_Decode_EmptyFrame_ThrowsFrameProtocolException()
        {
            var codec = new RawCopyCodec();

            Assert.ThrowsException<FrameProtocolException>(
                () => codec.Decode(ReadOnlyMemory<byte>.Empty));
        }
    }
}