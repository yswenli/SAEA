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
*修改时间：2026/11/24 18:21:25
*修改人： yswenli
*版本号： net10.0
*描述：补 Content-Length 超 int 上限与数值溢出的 413 用例
*
*****************************************************************************/
using System;
using System.Text;
using System.Buffers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Sockets.Pipeline;

namespace SAEA.Http.Tests
{
    /// <summary>HTTP 请求分帧器：整帧切分、上限早判、chunked 切分与续扫、升级屏障与粘包。</summary>
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
            var ex = Assert.ThrowsExactly<FrameProtocolException>(
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
            var ex = Assert.ThrowsExactly<FrameProtocolException>(
                () => splitter.TrySplit(new ReadOnlySequence<byte>(bytes), out consumed, out frame));

            Assert.AreEqual(HttpProtocolErrorCodes.RequestEntityTooLarge, ex.Message, "应以 413 token 报告体积超限。");
        }

        /// <summary>413 时应给出「Content-Length 减去已到达字节」的剩余请求体长度，供收口有界排空。</summary>
        [TestMethod]
        public void HttpRequestSplitter_BodyOverMaxRequestSize_ReportsUnreadBodyBytes()
        {
            var head = "POST /big HTTP/1.1\r\nHost: a\r\nContent-Length: 100\r\n\r\n";
            var headerLength = Encoding.ASCII.GetByteCount(head);
            var bytes = new byte[headerLength + 30];
            Encoding.ASCII.GetBytes(head).CopyTo(bytes, 0);

            var splitter = new HttpRequestSplitter(maxRequestSize: 10);

            long consumed;
            ReadOnlySequence<byte> frame;
            var ex = Assert.ThrowsExactly<FrameProtocolException>(
                () => splitter.TrySplit(new ReadOnlySequence<byte>(bytes), out consumed, out frame));

            Assert.AreEqual(100L - 30, ex.UnreadBodyBytes, "剩余请求体应等于 Content-Length 减去已到达字节。");
        }

        /// <summary>Content-Length 超过 int 上限仍按 long 记录，413 的剩余量不能被截断成负数。</summary>
        [TestMethod]
        public void HttpRequestSplitter_ContentLengthOverIntMax_ReportsUnreadBodyBytes()
        {
            const long contentLength = int.MaxValue + 4096L;
            var head = "POST /big HTTP/1.1\r\nHost: a\r\nContent-Length: " + contentLength + "\r\n\r\n";
            var headerLength = Encoding.ASCII.GetByteCount(head);
            var bytes = new byte[headerLength + 30];
            Encoding.ASCII.GetBytes(head).CopyTo(bytes, 0);

            var splitter = new HttpRequestSplitter(maxRequestSize: 10);

            long consumed;
            ReadOnlySequence<byte> frame;
            var ex = Assert.ThrowsExactly<FrameProtocolException>(
                () => splitter.TrySplit(new ReadOnlySequence<byte>(bytes), out consumed, out frame));

            Assert.AreEqual(HttpProtocolErrorCodes.RequestEntityTooLarge, ex.Message, "超过 int 上限的合法长度仍应判为 413。");
            Assert.AreEqual(contentLength - 30, ex.UnreadBodyBytes, "剩余量应按 long 计算，不能因 int 截断变成负数。");
        }

        /// <summary>Content-Length 超出 long 上限属于数值超范围，应回 413 且剩余量未知。</summary>
        [TestMethod]
        public void HttpRequestSplitter_ContentLengthBeyondLong_Throws413WithUnknownUnread()
        {
            var bytes = Encoding.ASCII.GetBytes(
                "POST /big HTTP/1.1\r\nHost: a\r\nContent-Length: 99999999999999999999\r\n\r\n");
            var splitter = new HttpRequestSplitter(maxRequestSize: 10);

            long consumed;
            ReadOnlySequence<byte> frame;
            var ex = Assert.ThrowsExactly<FrameProtocolException>(
                () => splitter.TrySplit(new ReadOnlySequence<byte>(bytes), out consumed, out frame));

            Assert.AreEqual(HttpProtocolErrorCodes.RequestEntityTooLarge, ex.Message, "数值超范围应按 413 拒绝，而不是 400。");
            Assert.AreEqual(-1L, ex.UnreadBodyBytes, "剩余量未知时应交回处理器按请求体上限兜底。");
        }

        /// <summary>chunked 完整帧应整帧切出，consumed 与帧字节都覆盖报头加原始 chunk 字节。</summary>
        [TestMethod]
        public void HttpRequestSplitter_Chunked_SplitsWholeRequest()
        {
            var head = "POST /c HTTP/1.1\r\nHost: a\r\nTransfer-Encoding: chunked\r\n\r\n";
            var body = "5\r\nhello\r\n0\r\n\r\n";
            var bytes = Encoding.ASCII.GetBytes(head + body);
            var splitter = new HttpRequestSplitter();

            long consumed;
            ReadOnlySequence<byte> frame;
            var result = splitter.TrySplit(new ReadOnlySequence<byte>(bytes), out consumed, out frame);

            Assert.IsTrue(result, "chunked 终止块到齐后应切分成功。");
            Assert.AreEqual((long)bytes.Length, consumed, "consumed 应等于报头加原始 chunk 字节总长。");
            Assert.AreEqual(head + body, Encoding.ASCII.GetString(frame.ToArray()), "帧应逐字节等于报头加原始 chunk 字节。");
        }

        /// <summary>多个 chunk 应切出一帧，且 chunked 帧之后的普通请求仍能照常切分。</summary>
        [TestMethod]
        public void HttpRequestSplitter_ThreeChunksSplitsAndResetsState()
        {
            var head = "POST /c HTTP/1.1\r\nHost: a\r\nTransfer-Encoding: chunked\r\n\r\n";
            var body = "5\r\nhello\r\n6\r\n world\r\n0\r\n\r\n";
            var next = "GET /next HTTP/1.1\r\nHost: a\r\n\r\n";
            var buffer = new ReadOnlySequence<byte>(Encoding.ASCII.GetBytes(head + body + next));
            var splitter = new HttpRequestSplitter();

            long consumed;
            ReadOnlySequence<byte> frame;
            Assert.IsTrue(splitter.TrySplit(buffer, out consumed, out frame), "三个 chunk 到齐后应切出整帧。");
            Assert.AreEqual((long)(head.Length + body.Length), consumed, "consumed 应覆盖报头与 chunk 字节，不含后续请求。");
            Assert.AreEqual(head + body, Encoding.ASCII.GetString(frame.ToArray()), "帧应逐字节等于报头加原始 chunk 字节。");

            Assert.IsTrue(splitter.TrySplit(buffer.Slice(consumed), out consumed, out frame), "chunked 帧之后的普通请求应照常切分。");
            Assert.AreEqual(next, Encoding.ASCII.GetString(frame.ToArray()), "下一条请求前必须复位 chunk 扫描状态。");
        }

        /// <summary>同一请求分四次喂入时，前三次只推进状态，最后一次才切出整帧。</summary>
        [TestMethod]
        public void HttpRequestSplitter_ChunkedAcrossFourReads_SplitsOnLastCall()
        {
            var head = "POST /c HTTP/1.1\r\nHost: a\r\nTransfer-Encoding: chunked\r\n\r\n";
            var body = "5\r\nhello\r\n6\r\n world\r\n0\r\n\r\n";
            var all = Encoding.ASCII.GetBytes(head + body);
            var splitter = new HttpRequestSplitter();

            // 依次停在 chunk-size 行中、data 末尾、终止空行前，最后给全量
            var cuts = new[] { head.Length + 2, head.Length + 8, all.Length - 2 };

            long consumed;
            ReadOnlySequence<byte> frame;

            foreach (var cut in cuts)
            {
                Assert.IsFalse(
                    splitter.TrySplit(new ReadOnlySequence<byte>(all, 0, cut), out consumed, out frame),
                    "数据不足时不应切出帧。");

                Assert.AreEqual(0L, consumed, "数据不足时 consumed 应为 0。");
            }

            Assert.IsTrue(splitter.TrySplit(new ReadOnlySequence<byte>(all), out consumed, out frame), "最后一次数据到齐后应切出整帧。");
            Assert.AreEqual((long)all.Length, consumed, "consumed 应等于整帧长度。");
            Assert.AreEqual(head + body, Encoding.ASCII.GetString(frame.ToArray()), "跨多次调用续扫后帧字节不应变化。");
        }

        /// <summary>带 chunk 扩展与 trailer 的请求应切出整帧，扩展与 trailer 一并算进帧长。</summary>
        [TestMethod]
        public void HttpRequestSplitter_ChunkExtensionAndTrailer_SplitsWholeRequest()
        {
            var head = "POST /c HTTP/1.1\r\nHost: a\r\nTransfer-Encoding: chunked\r\n\r\n";
            var body = "5;name=value\r\nhello\r\n0\r\nX-Trace: abc\r\n\r\n";
            var bytes = Encoding.ASCII.GetBytes(head + body);
            var splitter = new HttpRequestSplitter();

            long consumed;
            ReadOnlySequence<byte> frame;
            Assert.IsTrue(splitter.TrySplit(new ReadOnlySequence<byte>(bytes), out consumed, out frame), "含扩展与 trailer 的请求应切分成功。");
            Assert.AreEqual((long)bytes.Length, consumed, "consumed 应包含 chunk 扩展与 trailer 的原始字节。");
            Assert.AreEqual(head + body, Encoding.ASCII.GetString(frame.ToArray()), "帧应逐字节等于原始报文。");
        }

        /// <summary>chunk 数据累计超过请求体上限时，应抛 413，不等全部数据到齐。</summary>
        [TestMethod]
        public void HttpRequestSplitter_ChunkedDecodedOverLimit_Throws413()
        {
            var bytes = Encoding.ASCII.GetBytes(
                "POST /c HTTP/1.1\r\nHost: a\r\nTransfer-Encoding: chunked\r\n\r\n"
                + "6\r\nhello \r\n6\r\nworld!\r\n0\r\n\r\n");

            var splitter = new HttpRequestSplitter(maxRequestSize: 10);

            long consumed;
            ReadOnlySequence<byte> frame;
            var ex = Assert.ThrowsExactly<FrameProtocolException>(
                () => splitter.TrySplit(new ReadOnlySequence<byte>(bytes), out consumed, out frame));

            Assert.AreEqual(HttpProtocolErrorCodes.RequestEntityTooLarge, ex.Message, "解码长度超限应以 413 token 报告。");
        }

        /// <summary>chunked 与 Content-Length 同时出现时定界有歧义，应按 400 拒绝。</summary>
        [TestMethod]
        public void HttpRequestSplitter_ChunkedWithContentLength_Throws400()
        {
            var bytes = Encoding.ASCII.GetBytes(
                "POST /c HTTP/1.1\r\nHost: a\r\n"
                + "Transfer-Encoding: chunked\r\nContent-Length: 5\r\n\r\n"
                + "5\r\nhello\r\n0\r\n\r\n");

            var splitter = new HttpRequestSplitter();

            long consumed;
            ReadOnlySequence<byte> frame;
            var ex = Assert.ThrowsExactly<FrameProtocolException>(
                () => splitter.TrySplit(new ReadOnlySequence<byte>(bytes), out consumed, out frame));

            Assert.AreEqual(HttpProtocolErrorCodes.BadRequest, ex.Message, "两种定界方式同时出现应以 400 token 拒绝。");
        }

        /// <summary>chunk 数据之后缺 CRLF 时按 400 拒绝，不能把后续字节当成下一条请求。</summary>
        [TestMethod]
        public void HttpRequestSplitter_ChunkDataWithoutCrLf_Throws400()
        {
            var bytes = Encoding.ASCII.GetBytes(
                "POST /c HTTP/1.1\r\nHost: a\r\nTransfer-Encoding: chunked\r\n\r\n"
                + "5\r\nhelloXX0\r\n\r\n");

            var splitter = new HttpRequestSplitter();

            long consumed;
            ReadOnlySequence<byte> frame;
            var ex = Assert.ThrowsExactly<FrameProtocolException>(
                () => splitter.TrySplit(new ReadOnlySequence<byte>(bytes), out consumed, out frame));

            Assert.AreEqual(HttpProtocolErrorCodes.BadRequest, ex.Message, "数据段行尾不是 CRLF 应回 400。");
        }

        /// <summary>解码长度未超上限、但原始 chunk 字节超出帧预算时，应按 413 拒绝。</summary>
        [TestMethod]
        public void HttpRequestSplitter_ChunkedRawBytesOverFrameLimit_Throws413()
        {
            var text = new StringBuilder("POST /c HTTP/1.1\r\nHost: a\r\nTransfer-Encoding: chunked\r\n\r\n");

            // 每个 1 字节 chunk 占 6 字节原始字节：解码总长 100 未超请求体上限，原始字节远超帧预算
            for (var i = 0; i < 100; i++)
                text.Append("1\r\nx\r\n");

            text.Append("0\r\n\r\n");

            var bytes = Encoding.ASCII.GetBytes(text.ToString());

            // 报头 56 字节小于 64，帧预算为 100 + 64
            var splitter = new HttpRequestSplitter(maxRequestSize: 100, maxHeaderSize: 64);

            long consumed;
            ReadOnlySequence<byte> frame;
            var ex = Assert.ThrowsExactly<FrameProtocolException>(
                () => splitter.TrySplit(new ReadOnlySequence<byte>(bytes), out consumed, out frame));

            Assert.AreEqual(HttpProtocolErrorCodes.RequestEntityTooLarge, ex.Message, "原始字节超帧预算应以 413 token 报告。");
        }

        /// <summary>8 位十六进制的 chunk-size 超过 int 上限时，应按 413 拒绝，不能溢出成负数后越界。</summary>
        [TestMethod]
        public void HttpRequestSplitter_ChunkSizeOverIntMax_Throws413()
        {
            var bytes = Encoding.ASCII.GetBytes(
                "POST /c HTTP/1.1\r\nHost: a\r\nTransfer-Encoding: chunked\r\n\r\n"
                + "FFFFFFFF\r\n");

            var splitter = new HttpRequestSplitter();

            long consumed;
            ReadOnlySequence<byte> frame;
            var ex = Assert.ThrowsExactly<FrameProtocolException>(
                () => splitter.TrySplit(new ReadOnlySequence<byte>(bytes), out consumed, out frame));

            Assert.AreEqual(HttpProtocolErrorCodes.RequestEntityTooLarge, ex.Message, "chunk-size 超限应以 413 token 报告。");
            Assert.AreNotEqual(typeof(ArgumentOutOfRangeException), ex.GetType(), "不能因溢出后反向移动偏移而抛出越界异常。");
        }

        /// <summary>data 段一次跨过帧预算并正好停在缓冲末尾时，也必须在本次调用内抛 413。</summary>
        [TestMethod]
        public void HttpRequestSplitter_ChunkedDataFillsBufferExceedsFrameLimit_Throws413()
        {
            const int maxRequestSize = 100;

            var head = "POST /c HTTP/1.1\r\nHost: a\r\nTransfer-Encoding: chunked\r\n\r\n";

            var text = new StringBuilder(head);

            // 17 个完整的一字节 chunk，再补一个只到 data 的未完成 chunk；
            // 最后一个 data 推进后正好停在缓冲末尾，且原始字节超过帧预算。
            for (var i = 0; i < 17; i++)
                text.Append("1\r\nx\r\n");

            text.Append("1\r\nx");

            var bytes = Encoding.ASCII.GetBytes(text.ToString());
            var headLength = Encoding.ASCII.GetByteCount(head);

            // 帧预算比最后一个 data 的结束偏移小 1，让 data 段一次跨过预算并停在缓冲末尾
            var maxHeaderSize = headLength + 5;

            var splitter = new HttpRequestSplitter(maxRequestSize: maxRequestSize, maxHeaderSize: maxHeaderSize);

            long consumed;
            ReadOnlySequence<byte> frame;
            var ex = Assert.ThrowsExactly<FrameProtocolException>(
                () => splitter.TrySplit(new ReadOnlySequence<byte>(bytes), out consumed, out frame));

            Assert.AreEqual(HttpProtocolErrorCodes.RequestEntityTooLarge, ex.Message, "data 段跨过帧预算应以 413 token 报告。");
        }

        /// <summary>带 Expect 且请求体未到齐时应触发一次 100 Continue，帧到齐后不再触发。</summary>
        [TestMethod]
        public void HttpRequestSplitter_ExpectContinueWithoutBody_FiresOnce()
        {
            var head = "POST /e HTTP/1.1\r\nHost: a\r\nExpect: 100-continue\r\nContent-Length: 5\r\n\r\n";
            var all = Encoding.ASCII.GetBytes(head + "hello");
            var splitter = new HttpRequestSplitter();
            var count = 0;

            splitter.ExpectContinueHandler = () => count++;

            long consumed;
            ReadOnlySequence<byte> frame;
            Assert.IsFalse(
                splitter.TrySplit(new ReadOnlySequence<byte>(all, 0, head.Length), out consumed, out frame),
                "只给报头时应返回 false。");
            Assert.AreEqual(1, count, "请求体未到齐时应触发一次 100 Continue。");

            Assert.IsTrue(splitter.TrySplit(new ReadOnlySequence<byte>(all), out consumed, out frame), "请求体到齐后应切出整帧。");
            Assert.AreEqual(1, count, "同一请求最多触发一次 100 Continue。");
        }

        /// <summary>请求体与报头一次到齐时不需要 100 Continue，不应触发回调。</summary>
        [TestMethod]
        public void HttpRequestSplitter_ExpectContinueWithCompleteBody_DoesNotFire()
        {
            var bytes = Encoding.ASCII.GetBytes(
                "POST /e HTTP/1.1\r\nHost: a\r\nExpect: 100-continue\r\nContent-Length: 5\r\n\r\nhello");

            var splitter = new HttpRequestSplitter();
            var count = 0;
            splitter.ExpectContinueHandler = () => count++;

            long consumed;
            ReadOnlySequence<byte> frame;
            Assert.IsTrue(splitter.TrySplit(new ReadOnlySequence<byte>(bytes), out consumed, out frame), "完整帧应切分成功。");
            Assert.AreEqual(0, count, "请求体已到齐时不应触发 100 Continue。");
        }

        /// <summary>Content-Length 为 0 时没有请求体，不应触发 100 Continue。</summary>
        [TestMethod]
        public void HttpRequestSplitter_ExpectContinueZeroLength_DoesNotFire()
        {
            var bytes = Encoding.ASCII.GetBytes(
                "POST /e HTTP/1.1\r\nHost: a\r\nExpect: 100-continue\r\nContent-Length: 0\r\n\r\n");

            var splitter = new HttpRequestSplitter();
            var count = 0;
            splitter.ExpectContinueHandler = () => count++;

            long consumed;
            ReadOnlySequence<byte> frame;
            Assert.IsTrue(splitter.TrySplit(new ReadOnlySequence<byte>(bytes), out consumed, out frame), "零长度请求体应立即切出整帧。");
            Assert.AreEqual(0, count, "零长度请求体不应触发 100 Continue。");
        }

        /// <summary>chunked 请求只收到报头与首个 chunk 头时，应先触发一次 100 Continue。</summary>
        [TestMethod]
        public void HttpRequestSplitter_ExpectContinueChunked_FiresBeforeTerminator()
        {
            var head = "POST /e HTTP/1.1\r\nHost: a\r\nExpect: 100-continue\r\nTransfer-Encoding: chunked\r\n\r\n";
            var bytes = Encoding.ASCII.GetBytes(head + "5\r\n");

            var splitter = new HttpRequestSplitter();
            var count = 0;
            splitter.ExpectContinueHandler = () => count++;

            long consumed;
            ReadOnlySequence<byte> frame;
            Assert.IsFalse(splitter.TrySplit(new ReadOnlySequence<byte>(bytes), out consumed, out frame), "终止块未到齐时应返回 false。");
            Assert.AreEqual(1, count, "chunked 请求体未到齐时应触发一次 100 Continue。");
        }

        /// <summary>Content-Length 超限被 413 拒绝的请求，不能先收到 100 Continue。</summary>
        [TestMethod]
        public void HttpRequestSplitter_ExpectContinueOverLimit_DoesNotFire()
        {
            var bytes = Encoding.ASCII.GetBytes(
                "POST /e HTTP/1.1\r\nHost: a\r\nExpect: 100-continue\r\nContent-Length: 100\r\n\r\n");

            var splitter = new HttpRequestSplitter(maxRequestSize: 10);
            var count = 0;
            splitter.ExpectContinueHandler = () => count++;

            long consumed;
            ReadOnlySequence<byte> frame;
            var ex = Assert.ThrowsExactly<FrameProtocolException>(
                () => splitter.TrySplit(new ReadOnlySequence<byte>(bytes), out consumed, out frame));

            Assert.AreEqual(HttpProtocolErrorCodes.RequestEntityTooLarge, ex.Message, "超限请求应以 413 token 拒绝。");
            Assert.AreEqual(0, count, "被 413 拒绝的请求不应触发 100 Continue。");
        }

        /// <summary>帧切出后应复位 100 Continue 状态，下一条带 Expect 的请求可再次触发。</summary>
        [TestMethod]
        public void HttpRequestSplitter_ExpectContinueNextRequest_FiresAgain()
        {
            var firstHead = "POST /e1 HTTP/1.1\r\nHost: a\r\nExpect: 100-continue\r\nContent-Length: 1\r\n\r\n";
            var secondHead = "POST /e2 HTTP/1.1\r\nHost: a\r\nExpect: 100-continue\r\nContent-Length: 1\r\n\r\n";
            var first = Encoding.ASCII.GetBytes(firstHead + "x");
            var second = Encoding.ASCII.GetBytes(secondHead + "y");

            var splitter = new HttpRequestSplitter();
            var count = 0;
            splitter.ExpectContinueHandler = () => count++;

            long consumed;
            ReadOnlySequence<byte> frame;

            Assert.IsFalse(splitter.TrySplit(new ReadOnlySequence<byte>(first, 0, firstHead.Length), out consumed, out frame), "第一条请求只给报头时应返回 false。");
            Assert.IsTrue(splitter.TrySplit(new ReadOnlySequence<byte>(first), out consumed, out frame), "第一条请求体到齐后应切出整帧。");
            Assert.AreEqual(1, count, "第一条请求应触发一次 100 Continue。");

            Assert.IsFalse(splitter.TrySplit(new ReadOnlySequence<byte>(second, 0, secondHead.Length), out consumed, out frame), "第二条请求只给报头时应返回 false。");
            Assert.IsTrue(splitter.TrySplit(new ReadOnlySequence<byte>(second), out consumed, out frame), "第二条请求体到齐后应切出整帧。");
            Assert.AreEqual(2, count, "帧切出后应复位状态，第二条请求应再次触发 100 Continue。");
        }

        /// <summary>报头已结束但请求行畸形时，应立即按 400 抛出，不能当成数据不足继续等。</summary>
        [TestMethod]
        public void HttpRequestSplitter_MalformedHeader_Throws400()
        {
            var bytes = Encoding.ASCII.GetBytes("GARBAGE\r\n\r\n");
            var splitter = new HttpRequestSplitter();

            long consumed;
            ReadOnlySequence<byte> frame;
            var ex = Assert.ThrowsExactly<FrameProtocolException>(
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