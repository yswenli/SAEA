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
*文件名： DataPlaneCodecTests
*版本号： net10.0
*唯一标识：af72434f-f59f-4e4f-a746-45b8f20dad59
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 19:00:00
*描述：数据面业务帧前缀、首字节解复用与 UDP 通道投递验收
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 19:00:00
*修改人： yswenli
*版本号： net10.0
*描述：新增数据面编码与 UDP 通道用例
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.P2P.Common;
using SAEA.P2P.Transport;

namespace SAEA.P2P.Tests.Transport
{
    /// <summary>
    /// 数据面编码与 UDP 直连通道：前缀、解复用边界、超长拒绝与一次回调一条业务帧。
    /// </summary>
    [TestCategory(P2PTestSuite.Unit)]
    [TestClass]
    public class DataPlaneCodecTests
    {
        /// <summary>
        /// 业务帧以 0xC0 开头，其后原样携带调用方负载
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void DataPlaneCodec_业务帧_以0xC0开头()
        {
            var frame = DataPlaneCodec.Wrap(new byte[] { 1, 2, 3 });

            Assert.AreEqual(4, frame.Length);
            Assert.AreEqual(0xC0, frame[0]);
            Assert.AreEqual(1, frame[1]);
            Assert.AreEqual(2, frame[2]);
            Assert.AreEqual(3, frame[3]);
            Assert.AreEqual(DataPlaneKind.Data, DataPlaneCodec.Classify(frame));

            ReadOnlySpan<byte> payload;
            Assert.IsTrue(DataPlaneCodec.TryUnwrap(frame, out payload));
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, payload.ToArray());
        }

        /// <summary>
        /// 首字节 0x00 到 0x3F 归 STUN，0x40 以上的未分配区间与 0xC0 之外的取值归未知
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void DataPlaneCodec_STUN首字节_归类为STUN()
        {
            Assert.AreEqual(DataPlaneKind.Stun, DataPlaneCodec.Classify(new byte[] { 0x00, 0x01 }));
            Assert.AreEqual(DataPlaneKind.Stun, DataPlaneCodec.Classify(new byte[] { 0x01 }));
            Assert.AreEqual(DataPlaneKind.Stun, DataPlaneCodec.Classify(new byte[] { 0x3F }));

            Assert.AreEqual(DataPlaneKind.Unknown, DataPlaneCodec.Classify(new byte[] { 0x40 }));
            Assert.AreEqual(DataPlaneKind.Unknown, DataPlaneCodec.Classify(new byte[] { 0xBF }));
            Assert.AreEqual(DataPlaneKind.Unknown, DataPlaneCodec.Classify(new byte[] { 0xC1 }));
            Assert.AreEqual(DataPlaneKind.Unknown, DataPlaneCodec.Classify(new byte[] { 0xFF }));
        }

        /// <summary>
        /// 首字节不是 0xC0 或数据报为空时解包失败，不产出载荷
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void DataPlaneCodec_未知首字节_丢弃()
        {
            ReadOnlySpan<byte> payload;
            Assert.IsFalse(DataPlaneCodec.TryUnwrap(new byte[] { 0x40, 0x01 }, out payload));
            Assert.AreEqual(0, payload.Length);

            Assert.IsFalse(DataPlaneCodec.TryUnwrap(Array.Empty<byte>(), out payload));
            Assert.AreEqual(0, payload.Length);
        }

        /// <summary>
        /// 载荷恰好占满单包上限时允许封装，超出 1 字节即拒绝并带 PayloadTooLarge
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void DataPlaneCodec_超长帧_拒绝()
        {
            var frame = DataPlaneCodec.Wrap(new byte[DataPlaneCodec.MaxPayloadLength]);
            Assert.AreEqual(DataPlaneCodec.MaxUdpDatagramLength, frame.Length);

            var ex = Assert.ThrowsExactly<P2PException>(
                () => DataPlaneCodec.Wrap(new byte[DataPlaneCodec.MaxPayloadLength + 1]));
            Assert.AreEqual(ErrorCode.PayloadTooLarge, ex.ErrorCode);
        }

        /// <summary>
        /// 发送一次只产出一个带前缀的数据报，收到的两条业务帧各自独立回调，STUN 只计数不回调
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void UdpDataChannel_两次发送_两次独立回调()
        {
            var fake = new FakeUdpTransport();
            var received = new List<byte[]>();

            using (var channel = new UdpDataChannel(fake, new IPEndPoint(IPAddress.Loopback, 6000)))
            {
                channel.FrameReceived += payload => received.Add(payload.ToArray());
                channel.Start();

                channel.Send(new byte[] { 9, 8 });

                Assert.AreEqual(1, fake.SentDatagrams.Count);
                CollectionAssert.AreEqual(new byte[] { 0xC0, 9, 8 }, fake.SentDatagrams[0]);
                Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 6000), fake.SentEndPoints[0]);

                fake.Raise(new byte[] { 0xC0, 0x01 });
                fake.Raise(new byte[] { 0xC0, 0x02, 0x03 });

                Assert.AreEqual(2, received.Count);
                CollectionAssert.AreEqual(new byte[] { 0x01 }, received[0]);
                CollectionAssert.AreEqual(new byte[] { 0x02, 0x03 }, received[1]);
                Assert.AreEqual(2, channel.ReceivedFrameCount);

                fake.Raise(new byte[] { 0x00, 0x01 });

                Assert.AreEqual(1, channel.DroppedDatagramCount);
                Assert.AreEqual(2, received.Count);
            }
        }

        /// <summary>
        /// 进程内假 UDP 传输：记录发送内容并按需触发收到的数据报
        /// </summary>
        private sealed class FakeUdpTransport : IUdpTransport
        {
            private static readonly IPEndPoint BoundEndPoint = new IPEndPoint(IPAddress.Loopback, 5000);

            public event Action<IPEndPoint, ReadOnlyMemory<byte>> DatagramReceived;

            public List<IPEndPoint> SentEndPoints { get; } = new List<IPEndPoint>();

            public List<byte[]> SentDatagrams { get; } = new List<byte[]>();

            public IPEndPoint LocalEndPoint
            {
                get { return BoundEndPoint; }
            }

            public bool IsConnected
            {
                get { return true; }
            }

            public Task BindAsync(IPEndPoint localEndPoint, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public void StartReceive()
            {
            }

            public Task SendToAsync(IPEndPoint remoteEndPoint, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
            {
                SentEndPoints.Add(remoteEndPoint);
                SentDatagrams.Add(data.ToArray());
                return Task.CompletedTask;
            }

            public void Dispose()
            {
            }

            /// <summary>
            /// 模拟传输层收到一个数据报，来源固定为回环地址
            /// </summary>
            /// <param name="datagram">数据报内容</param>
            public void Raise(byte[] datagram)
            {
                var handler = DatagramReceived;
                if (handler != null)
                {
                    handler(new IPEndPoint(IPAddress.Loopback, 6000), new ReadOnlyMemory<byte>(datagram));
                }
            }
        }
    }
}