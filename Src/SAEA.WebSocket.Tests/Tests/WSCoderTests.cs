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
*命名空间：SAEA.WebSocket.Tests
*文件名： WSCoderTests
*版本号： net10.0
*唯一标识：f8e81544-835f-4693-827c-b858f1b26342
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/16 10:40:00
*描述：WSCoder 的 byte[] 与 ReadOnlySequence 两条解码入口一致性用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/16 10:40:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，WSCoder 解码入口一致性用例
*
*****************************************************************************/
using System;
using System.Buffers;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.WebSocket.Model;
using SAEA.WebSocket.Tests.Tests.Support;
using SAEA.WebSocket.Type;

namespace SAEA.WebSocket.Tests
{
    /// <summary>
    /// WSCoder 解码入口：byte[] 重载必须与 ReadOnlySequence 重载解出相同的帧序列，且跨批次的半帧要留到补齐后再产出。
    /// </summary>
    [TestClass]
    public class WSCoderTests
    {
        /// <summary>
        /// 同一段字节分别走 byte[] 与 ReadOnlySequence 入口时，帧数、opcode 与帧体内容完全一致。
        /// </summary>
        [TestMethod]
        public void WSCoder_DecodeByteArray_MatchesSequenceInput()
        {
            var input = PipelineTestKit.Concat(
                WsFrameBuilder.Frame((byte)WSProtocalType.Text, Encoding.UTF8.GetBytes("hello")),
                WsFrameBuilder.Frame((byte)WSProtocalType.Binary, WsFrameBuilder.Payload(8)));

            var byteCoder = new WSCoder { RequireMask = true };
            var sequenceCoder = new WSCoder { RequireMask = true };

            var fromByteArray = byteCoder.Decode(input);
            var fromSequence = sequenceCoder.Decode(new ReadOnlySequence<byte>(input));

            try
            {
                Assert.AreEqual(2, fromByteArray.Count, "两帧的输入应解出两帧");
                Assert.AreEqual(fromSequence.Count, fromByteArray.Count, "两条入口解出的帧数必须一致");

                for (var i = 0; i < fromByteArray.Count; i++)
                {
                    var expected = (WSProtocal)fromSequence[i];
                    var actual = (WSProtocal)fromByteArray[i];

                    Assert.AreEqual(expected.Type, actual.Type, $"第 {i} 帧的 opcode 必须一致");
                    CollectionAssert.AreEqual(expected.Content.ToArray(), actual.Content.ToArray(),
                        $"第 {i} 帧的帧体必须一致");
                }
            }
            finally
            {
                fromByteArray.Dispose();
                fromSequence.Dispose();
            }
        }

        /// <summary>
        /// 一次只喂半帧时不产出，补齐剩余字节后才产出完整帧，说明字节切分不影响解码结果。
        /// </summary>
        [TestMethod]
        public void WSCoder_SplitFrameAcrossCalls_EmitsAfterCompletion()
        {
            var frame = WsFrameBuilder.Frame((byte)WSProtocalType.Text, Encoding.UTF8.GetBytes("split"));
            var head = new byte[3];
            var tail = new byte[frame.Length - 3];

            Array.Copy(frame, 0, head, 0, head.Length);
            Array.Copy(frame, head.Length, tail, 0, tail.Length);

            var coder = new WSCoder { RequireMask = true };

            var first = coder.Decode(head);
            DecodedFramesAssert.IsEmpty(first, "帧头未收全时不得产出帧");

            var second = coder.Decode(tail);
            try
            {
                Assert.AreEqual(1, second.Count, "补齐剩余字节后应产出一帧");

                var message = (WSProtocal)second[0];
                Assert.AreEqual((byte)WSProtocalType.Text, message.Type, "帧类型应与输入一致");
                Assert.AreEqual("split", Encoding.UTF8.GetString(message.Content.ToArray()), "帧体应与输入一致");
            }
            finally
            {
                second.Dispose();
            }
        }

        /// <summary>
        /// 空批次断言：批次存在但帧数为 0。
        /// </summary>
        static class DecodedFramesAssert
        {
            /// <summary>
            /// 断言批次中没有帧，并立即释放批次。
            /// </summary>
            /// <param name="frames">待检查的帧批次。</param>
            /// <param name="message">断言失败时的说明。</param>
            public static void IsEmpty(SAEA.Sockets.Base.DecodedFrames frames, string message)
            {
                if (frames == null)
                    throw new AssertFailedException(message + "（批次为 null）");

                try
                {
                    Assert.AreEqual(0, frames.Count, message);
                }
                finally
                {
                    frames.Dispose();
                }
            }
        }
    }
}