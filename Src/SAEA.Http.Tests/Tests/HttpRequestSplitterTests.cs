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
*命名空间：SAEA.Http.Tests
*文件名： HttpRequestSplitterTests
*版本号： net10.0
*唯一标识：b5676c5f-ad88-405b-9cdd-733cba781fa0
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/9 13:12:01
*描述：HTTP 请求分帧器的行为用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 10:20:00
*修改人： yswenli
*版本号： net10.0
*描述：补畸形报头按 400 拒绝的用例
*
*****************************************************************************/
using System;
using System.Text;
using System.Buffers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Sockets.Pipeline;

namespace SAEA.Http.Tests
{
    /// <summary>HTTP 请求分帧器：整帧切分、上限早判、chunked 拒绝、升级屏障与粘包。</summary>
    [TestClass]
    public class HttpRequestSplitterTests
    {
        /// <summary>带 Content-Length 的请求应整帧切出，consumed 等于整帧长度。</summary>
        [TestMethod]
        public void HttpRequestSplitter_ContentLengthBody_SplitsWholeRequest()
        {
            var bytes = Encoding.ASCII.GetBytes("POST /submit HTTP/1.1\r\nHost: a\r\nContent-Length: 5\r\n\r\nhello");
            var splitter = new HttpRequestSplitter();

            long consumed;
            ReadOnlySequence<byte> frame;
            var result = splitter.TrySplit(new ReadOnlySequence<byte>(bytes), out consumed, out frame);

            Assert.IsTrue(result, "整帧到齐时应切分成功。");
            Assert.AreEqual((long)bytes.Length, consumed, "consumed 应等于整帧长度。");
            CollectionAssert.AreEqual(bytes, frame.ToArray(), "帧应包含报头与请求体。");
        }

        /// <summary>报头未结束且超过头上限时，应立即按 431 抛出协议错误。</summary>
        [TestMethod]
        public void HttpRequestSplitter_HeaderOver64KB_Throws431()
        {
            var bytes = new byte[200];
            for (var i = 0; i < bytes.Length; i++)
                bytes[i] = (byte)'a';

            var splitter = new HttpRequestSplitter(maxHeaderSize: 64);

            long consumed;
            ReadOnlySequence<byte> frame;
            var ex = Assert.ThrowsException<FrameProtocolException>(
                () => splitter.TrySplit(new ReadOnlySequence<byte>(bytes), out consumed, out frame));

            Assert.AreEqual(HttpProtocolErrorCodes.RequestHeaderFieldsTooLarge, ex.Message, "应以 431 token 报告头上限。");
        }

        /// <summary>Content-Length 超限时，只拿到报头就应抛 413，不等请求体。</summary>
        [TestMethod]
        public void HttpRequestSplitter_BodyOverMaxRequestSize_Throws413WithoutWaitingBody()
        {
            var bytes = Encoding.ASCII.GetBytes("POST /big HTTP/1.1\r\nHost: a\r\nContent-Length: 100\r\n\r\n");
            var splitter = new HttpRequestSplitter(maxRequestSize: 10);

            long consumed;
            ReadOnlySequence<byte> frame;
            var ex = Assert.ThrowsException<FrameProtocolException>(
                () => splitter.TrySplit(new ReadOnlySequence<byte>(bytes), out consumed, out frame));

            Assert.AreEqual(HttpProtocolErrorCodes.RequestEntityTooLarge, ex.Message, "应以 413 token 报告体积超限。");
        }

        /// <summary>chunked 传输编码应显式拒绝，并以固定 token 报告。</summary>
        [TestMethod]
        public void HttpRequestSplitter_Chunked_ThrowsChunkedToken()
        {
            var bytes = Encoding.ASCII.GetBytes("POST /c HTTP/1.1\r\nHost: a\r\nTransfer-Encoding: chunked\r\n\r\n");
            var splitter = new HttpRequestSplitter();

            long consumed;
            ReadOnlySequence<byte> frame;
            var ex = Assert.ThrowsException<FrameProtocolException>(
                () => splitter.TrySplit(new ReadOnlySequence<byte>(bytes), out consumed, out frame));

            Assert.AreEqual(HttpProtocolErrorCodes.ChunkedNotSupported, ex.Message, "应以 chunked token 报告不支持。");
        }

        /// <summary>报头已结束但请求行畸形时，应立即按 400 抛出，不能当成数据不足继续等。</summary>
        [TestMethod]
        public void HttpRequestSplitter_MalformedHeader_Throws400()
        {
            var bytes = Encoding.ASCII.GetBytes("GARBAGE\r\n\r\n");
            var splitter = new HttpRequestSplitter();

            long consumed;
            ReadOnlySequence<byte> frame;
            var ex = Assert.ThrowsException<FrameProtocolException>(
                () => splitter.TrySplit(new ReadOnlySequence<byte>(bytes), out consumed, out frame));

            Assert.AreEqual(HttpProtocolErrorCodes.BadRequest, ex.Message, "应以 400 token 报告畸形报文。");
        }

        /// <summary>带 Upgrade: websocket 的请求切出后，应置起切换屏障。</summary>
        [TestMethod]
        public void HttpRequestSplitter_UpgradeRequest_SetsStopAfterCurrent()
        {
            var bytes = Encoding.ASCII.GetBytes(
                "GET /ws HTTP/1.1\r\nHost: a\r\nUpgrade: WebSocket\r\nConnection: Upgrade\r\n\r\n");

            // 先建分帧器再包屏障，最后回填，打破构造期的循环引用。
            var splitter = new HttpRequestSplitter();
            var barrier = new SwitchableSplitter(splitter);
            splitter.Barrier = barrier;

            long consumed;
            ReadOnlySequence<byte> frame;
            var result = splitter.TrySplit(new ReadOnlySequence<byte>(bytes), out consumed, out frame);

            Assert.IsTrue(result, "升级请求本身是合法帧，应先切出。");
            Assert.IsTrue(barrier.StopAfterCurrent, "升级请求后必须停切，等待业务决定是否切换协议。");
        }

        /// <summary>同一缓冲里的两个请求应按顺序切出，consumed 依次推进。</summary>
        [TestMethod]
        public void HttpRequestSplitter_PipelinedRequests_SplitInOrder()
        {
            var firstText = "GET /one HTTP/1.1\r\nHost: a\r\n\r\n";
            var secondText = "GET /two HTTP/1.1\r\nHost: b\r\n\r\n";
            var bytes = Encoding.ASCII.GetBytes(firstText + secondText);
            var buffer = new ReadOnlySequence<byte>(bytes);
            var splitter = new HttpRequestSplitter();

            var offset = 0L;
            var rest = buffer;

            long consumed;
            ReadOnlySequence<byte> frame;
            Assert.IsTrue(splitter.TrySplit(rest, out consumed, out frame), "第一个请求应切分成功。");
            Assert.AreEqual((long)firstText.Length, consumed, "第一个请求的 consumed 应只覆盖第一帧。");
            Assert.AreEqual(firstText, Encoding.ASCII.GetString(frame.ToArray()), "第一帧应是第一个请求。");

            offset += consumed;
            rest = buffer.Slice(offset);

            Assert.IsTrue(splitter.TrySplit(rest, out consumed, out frame), "第二个请求应切分成功。");
            Assert.AreEqual((long)secondText.Length, consumed, "第二个请求的 consumed 应只覆盖第二帧。");
            Assert.AreEqual(secondText, Encoding.ASCII.GetString(frame.ToArray()), "第二帧应是第二个请求。");

            offset += consumed;
            rest = buffer.Slice(offset);

            Assert.IsTrue(rest.IsEmpty, "两帧之后缓冲应正好消费完。");
            Assert.IsFalse(splitter.TrySplit(rest, out consumed, out frame), "空缓冲应返回 false。");
            Assert.AreEqual(0L, consumed, "数据不足时 consumed 应为 0。");
        }

        /// <summary>报头与请求体跨段时，应仍能切出完整一帧。</summary>
        [TestMethod]
        public void HttpRequestSplitter_MultiSegment_CrossSegmentHeaderAndBody()
        {
            var part1 = Encoding.ASCII.GetBytes("POST /multi HTTP/1.1\r\nHost: a\r\nContent-Le");
            var part2 = Encoding.ASCII.GetBytes("ngth: 4\r\n\r\nbody");
            var buffer = CreateTwoSegmentSequence(part1, part2);

            var splitter = new HttpRequestSplitter();

            long consumed;
            ReadOnlySequence<byte> frame;
            var result = splitter.TrySplit(buffer, out consumed, out frame);

            Assert.IsTrue(result, "跨段请求应切分成功。");
            Assert.AreEqual((long)(part1.Length + part2.Length), consumed, "consumed 应覆盖两段的总长度。");
            CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("POST /multi HTTP/1.1\r\nHost: a\r\nContent-Length: 4\r\n\r\nbody"),
                frame.ToArray(), "跨段帧应还原为完整请求。");
        }

        /// <summary>把两段内存拼成一个跨段的可读字节序列。</summary>
        /// <param name="first">第一段字节</param>
        /// <param name="second">第二段字节</param>
        /// <returns>跨段序列</returns>
        private static ReadOnlySequence<byte> CreateTwoSegmentSequence(byte[] first, byte[] second)
        {
            var firstSegment = new Segment(first);
            var secondSegment = new Segment(second);
            firstSegment.SetNext(secondSegment);
            return new ReadOnlySequence<byte>(firstSegment, 0, secondSegment, second.Length);
        }

        /// <summary>ReadOnlySequence 的分段节点实现。</summary>
        private sealed class Segment : ReadOnlySequenceSegment<byte>
        {
            /// <summary>以一段内存创建分段节点。</summary>
            /// <param name="bytes">本段字节</param>
            public Segment(byte[] bytes)
            {
                Memory = new ReadOnlyMemory<byte>(bytes);
            }

            /// <summary>把下一段接到本段之后。</summary>
            /// <param name="next">下一段</param>
            /// <returns>本段实例</returns>
            public Segment SetNext(Segment next)
            {
                next.RunningIndex = RunningIndex + Memory.Length;
                Next = next;
                return this;
            }
        }
    }
}