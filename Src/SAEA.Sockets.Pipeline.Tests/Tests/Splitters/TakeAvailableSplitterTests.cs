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
*命名空间：SAEA.Sockets.Pipeline.Tests.Tests.Splitters
*文件名： TakeAvailableSplitterTests
*版本号： net10.0
*唯一标识：4f2e024b-9d6b-456a-91e7-1e10c0e3f94f
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 18:26:56
*描述：无边界分帧器的切块、整段与空缓冲用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 18:26:56
*修改人： yswenli
*版本号： net10.0
*描述：新增无边界分帧器超过单帧上限时切块的用例
*
*****************************************************************************/
using System;
using System.Buffers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Splitters
{
    /// <summary>
    /// 无边界分帧器的切块行为：超过单帧上限按上限切块、未超上限整段一帧、空缓冲不产帧。
    /// </summary>
    [TestClass]
    public class TakeAvailableSplitterTests
    {
        private const int Limit = 64 * 1024;

        /// <summary>
        /// 缓冲超过单帧上限时按上限逐块切出而不抛异常，累计消费量等于总长且不丢字节
        /// </summary>
        [TestMethod]
        public void TrySplit_缓冲超过单帧上限_按上限切块且不抛异常()
        {
            var total = Limit * 3 + 100;
            var payload = NewPayload(total);
            var splitter = new TakeAvailableSplitter(Limit);
            var sequence = new ReadOnlySequence<byte>(payload);
            var expectedLengths = new[] { Limit, Limit, Limit, 100 };

            var offset = 0L;
            for (var i = 0; i < expectedLengths.Length; i++)
            {
                long consumed;
                ReadOnlySequence<byte> frame;

                Assert.IsTrue(splitter.TrySplit(sequence.Slice(offset), out consumed, out frame),
                    "第 " + (i + 1) + " 块应切出帧，缓冲超限不得抛异常。");
                Assert.AreEqual(expectedLengths[i], consumed, "第 " + (i + 1) + " 块的消费量不符合预期。");
                Assert.AreEqual(expectedLengths[i], (int)frame.Length, "第 " + (i + 1) + " 帧的长度不符合预期。");

                var expected = new byte[expectedLengths[i]];
                Array.Copy(payload, (int)offset, expected, 0, expected.Length);
                CollectionAssert.AreEqual(expected, frame.ToArray(), "第 " + (i + 1) + " 帧的字节内容不符合预期。");

                offset += consumed;
            }

            Assert.AreEqual(total, (int)offset, "累计消费量必须等于缓冲总长。");

            long tailConsumed;
            ReadOnlySequence<byte> tailFrame;
            Assert.IsFalse(splitter.TrySplit(sequence.Slice(offset), out tailConsumed, out tailFrame),
                "切完后不应继续产出帧。");
        }

        /// <summary>
        /// 缓冲未超上限时整段作为一帧，消费量等于缓冲长度
        /// </summary>
        [TestMethod]
        public void TrySplit_缓冲未超上限_整段一帧()
        {
            var payload = NewPayload(32 * 1024);
            var splitter = new TakeAvailableSplitter(Limit);

            long consumed;
            ReadOnlySequence<byte> frame;

            Assert.IsTrue(splitter.TrySplit(new ReadOnlySequence<byte>(payload), out consumed, out frame));
            Assert.AreEqual(payload.Length, consumed);
            Assert.AreEqual(payload.Length, (int)frame.Length);
            CollectionAssert.AreEqual(payload, frame.ToArray());
        }

        /// <summary>
        /// 空缓冲不产出帧，消费量为 0 且帧为空
        /// </summary>
        [TestMethod]
        public void TrySplit_空缓冲_返回false()
        {
            var splitter = new TakeAvailableSplitter(Limit);

            long consumed;
            ReadOnlySequence<byte> frame;

            Assert.IsFalse(splitter.TrySplit(ReadOnlySequence<byte>.Empty, out consumed, out frame));
            Assert.AreEqual(0, consumed);
            Assert.IsTrue(frame.IsEmpty);
        }

        private static byte[] NewPayload(int length)
        {
            // 用周期 251 的递增模式填充，便于断言切块后字节既不丢也不串位。
            var payload = new byte[length];
            for (var i = 0; i < payload.Length; i++)
                payload[i] = (byte)(i % 251);

            return payload;
        }
    }
}