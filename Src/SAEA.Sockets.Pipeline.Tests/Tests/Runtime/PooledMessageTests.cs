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
*命名空间：SAEA.Sockets.Pipeline.Tests.Tests.Runtime
*文件名： PooledMessageTests
*版本号： v26.11.5.1
*唯一标识：c8cc81c2-3b8c-458c-99b9-adab16aba6fc
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：池化消息的引用计数与缓冲归还契约：最后一份引用释放时才归还，重复释放幂等
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：池化消息的引用计数与缓冲归还契约：最后一份引用释放时才归还，重复释放幂等
*
*****************************************************************************/
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.Caching;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Runtime
{
    /// <summary>
    /// 池化消息的引用计数与缓冲归还契约：最后一份引用释放时才归还，重复释放幂等。
    /// </summary>
    [TestClass]
    public class PooledMessageTests
    {
        private const int PooledFrameLength = 1024;

        [TestMethod]
        public void Release_PooledBacking_ReturnsBufferToSmallPool()
        {
            var buffer = MemoryPoolManager.Rent(PooledFrameLength);
            var message = CreateMessage(
                new ReadOnlyMemory<byte>(buffer, 0, PooledFrameLength), buffer, PooledFrameLength, true);

            var before = MemoryPoolManager.GetStatistics().SmallPoolReturned;
            message.Release();
            var after = MemoryPoolManager.GetStatistics().SmallPoolReturned;

            Assert.AreEqual(before + 1, after, "池化背衬应在引用计数归零时归还到小池。");
            Assert.IsTrue(message.IsReleased);
            Assert.IsTrue(message.IsPooled);
        }

        [TestMethod]
        public void Release_NonPooledBacking_DoesNotTouchPoolCounters()
        {
            var exact = new byte[64];
            var message = CreateMessage(new ReadOnlyMemory<byte>(exact), exact, exact.Length, false);

            var before = MemoryPoolManager.GetStatistics().SmallPoolReturned;
            message.Release();
            var after = MemoryPoolManager.GetStatistics().SmallPoolReturned;

            Assert.AreEqual(before, after, "非池化背衬归还时只丢引用，不得计入归还计数。");
            Assert.IsTrue(message.IsReleased);
            Assert.IsFalse(message.IsPooled);
        }

        [TestMethod]
        public void Release_Repeated_IsIdempotentAndReturnsOnce()
        {
            var buffer = MemoryPoolManager.Rent(PooledFrameLength);
            var message = CreateMessage(
                new ReadOnlyMemory<byte>(buffer, 0, PooledFrameLength), buffer, PooledFrameLength, true);

            var before = MemoryPoolManager.GetStatistics().SmallPoolReturned;
            message.Release();
            message.Release();
            message.Release();
            var after = MemoryPoolManager.GetStatistics().SmallPoolReturned;

            Assert.AreEqual(before + 1, after, "重复 Release 不得重复归还。");
            Assert.IsTrue(message.IsReleased);
        }

        [TestMethod]
        public void Frame_AfterRelease_ThrowsObjectDisposedException()
        {
            var exact = new byte[32];
            var message = CreateMessage(new ReadOnlyMemory<byte>(exact), exact, exact.Length, false);

            message.Release();

            Assert.ThrowsException<ObjectDisposedException>(() => message.Frame.ToArray());
        }

        [TestMethod]
        public void Retain_DefersReturnUntilLastRelease()
        {
            var buffer = MemoryPoolManager.Rent(PooledFrameLength);
            var message = CreateMessage(
                new ReadOnlyMemory<byte>(buffer, 0, PooledFrameLength), buffer, PooledFrameLength, true);

            var before = MemoryPoolManager.GetStatistics().SmallPoolReturned;

            message.Retain();
            message.Release();
            Assert.AreEqual(before, MemoryPoolManager.GetStatistics().SmallPoolReturned,
                "Retain 之后第一次 Release 只减计数，不归还背衬。");
            Assert.IsFalse(message.IsReleased);

            message.Release();
            Assert.AreEqual(before + 1, MemoryPoolManager.GetStatistics().SmallPoolReturned,
                "最后一份引用释放时才归还背衬。");
            Assert.IsTrue(message.IsReleased);
        }

        [TestMethod]
        public void Retain_AfterRelease_ThrowsObjectDisposedException()
        {
            var exact = new byte[32];
            var message = CreateMessage(new ReadOnlyMemory<byte>(exact), exact, exact.Length, false);

            message.Release();

            Assert.ThrowsException<ObjectDisposedException>(() => message.Retain());
        }

        private static PooledMessage CreateMessage(
            ReadOnlyMemory<byte> frame, byte[] buffer, int originalSize, bool isPooled)
        {
            return new PooledMessage(frame, buffer, originalSize, isPooled);
        }
    }
}