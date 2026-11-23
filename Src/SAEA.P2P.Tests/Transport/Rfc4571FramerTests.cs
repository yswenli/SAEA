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
*命名空间：SAEA.P2P.Tests.Transport
*文件名： Rfc4571FramerTests
*版本号： net10.0
*唯一标识：0a10fdb1-d2d1-45be-b57e-c740732e1238
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 13:08:57
*描述：RFC 4571 两字节长度前缀分帧验收
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 13:08:57
*修改人： yswenli
*版本号： net10.0
*描述：新增 RFC 4571 分帧用例
*
*****************************************************************************/
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.P2P.Transport;

namespace SAEA.P2P.Tests.Transport
{
    /// <summary>
    /// RFC 4571 分帧：零长度帧、半包等待、粘包逐帧、超长拒绝与写入目标校验。
    /// </summary>
    [TestCategory(P2PTestSuite.Unit)]
    [TestClass]
    public class Rfc4571FramerTests
    {
        /// <summary>
        /// 零长度帧只占两字节前缀，取出的帧体长度为 0 且每个前缀只产出一帧
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public void Rfc4571_零长度帧_合法()
        {
            var encoded = Rfc4571Framer.Encode(ReadOnlySpan<byte>.Empty);

            Assert.AreEqual(2, encoded.Length);
            Assert.AreEqual(0x00, encoded[0]);
            Assert.AreEqual(0x00, encoded[1]);

            var framer = new Rfc4571Framer();
            framer.Append(encoded);

            ReadOnlyMemory<byte> frame;
            Assert.IsTrue(framer.TryReadFrame(out frame));
            Assert.AreEqual(0, frame.Length);

            Assert.IsFalse(framer.TryReadFrame(out frame));
            Assert.AreEqual(0, framer.BufferedLength);
        }

        /// <summary>
        /// 帧体不足时保持 false 并保留已收字节，补齐后才产出完整载荷
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public void Rfc4571_半包_等待()
        {
            var payload = new byte[] { 1, 2, 3, 4, 5 };
            var encoded = Rfc4571Framer.Encode(payload);

            var framer = new Rfc4571Framer();
            framer.Append(encoded.AsSpan(0, 4));

            ReadOnlyMemory<byte> frame;
            Assert.IsFalse(framer.TryReadFrame(out frame));
            Assert.AreEqual(4, framer.BufferedLength);

            framer.Append(encoded.AsSpan(4));

            Assert.IsTrue(framer.TryReadFrame(out frame));
            CollectionAssert.AreEqual(payload, frame.ToArray());
        }

        /// <summary>
        /// 一次追加两帧时按顺序各产出一次，帧体切片在当场复制后仍与前缀一致
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public void Rfc4571_粘包_逐帧产出()
        {
            var first = new byte[] { 0x0A, 0x0B, 0x0C };
            var second = new byte[] { 1, 2, 3, 4, 5, 6 };

            var firstFrame = Rfc4571Framer.Encode(first);
            var secondFrame = Rfc4571Framer.Encode(second);
            var combined = new byte[firstFrame.Length + secondFrame.Length];
            firstFrame.CopyTo(combined, 0);
            secondFrame.CopyTo(combined, firstFrame.Length);

            var framer = new Rfc4571Framer();
            framer.Append(combined);
            Assert.AreEqual(combined.Length, framer.BufferedLength);

            ReadOnlyMemory<byte> frame;
            Assert.IsTrue(framer.TryReadFrame(out frame));
            CollectionAssert.AreEqual(first, frame.ToArray());

            Assert.IsTrue(framer.TryReadFrame(out frame));
            CollectionAssert.AreEqual(second, frame.ToArray());

            Assert.IsFalse(framer.TryReadFrame(out frame));
            Assert.AreEqual(0, framer.BufferedLength);
        }

        /// <summary>
        /// 声明长度超过上限立即抛错并复位，随后仍能正常解析合法帧
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public void Rfc4571_超长帧_拒绝()
        {
            var framer = new Rfc4571Framer(16);

            Assert.ThrowsExactly<InvalidDataException>(
                () => framer.Append(new byte[] { 0x07, 0xD0 }));
            Assert.AreEqual(0, framer.BufferedLength);

            framer.Append(new byte[] { 0x00, 0x01, 0x2A });

            ReadOnlyMemory<byte> frame;
            Assert.IsTrue(framer.TryReadFrame(out frame));
            CollectionAssert.AreEqual(new byte[] { 0x2A }, frame.ToArray());
        }

        /// <summary>
        /// 正常写入时前缀为大端长度，目标不足或载荷超限都按参数错误拒绝
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public void Rfc4571_WriteFrame_目标不足抛错()
        {
            var payload = new byte[8];
            var destination = new byte[10];

            var written = Rfc4571Framer.WriteFrame(payload, destination);

            Assert.AreEqual(10, written);
            Assert.AreEqual(0x00, destination[0]);
            Assert.AreEqual(0x08, destination[1]);

            Assert.ThrowsExactly<ArgumentException>(
                () => Rfc4571Framer.WriteFrame(payload, new byte[9]));
            Assert.ThrowsExactly<ArgumentException>(
                () => Rfc4571Framer.WriteFrame(new byte[65536], new byte[65538]));
        }
    }
}