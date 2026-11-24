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
*文件名： HttpRequestSplitterHeaderScanTests
*版本号： net10.0
*唯一标识：e793f1e7-da20-418f-b5f8-12b6f639428a
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 18:52:00
*描述：分帧器按字节扫报头字段的行为用例：大小写、值两侧空白、重复头、跨段与非法数值
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 18:52:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，锁定 Content-Length 与 Transfer-Encoding 的字节扫描结果与原解析一致
*
*****************************************************************************/
using System;
using System.Buffers;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Sockets.Pipeline;

namespace SAEA.Http.Tests
{
    /// <summary>分帧器只按字节扫定帧字段时必须与原来的报头解析结果一致。</summary>
    [TestClass]
    public class HttpRequestSplitterHeaderScanTests
    {
        /// <summary>头名大小写混写、值两侧带空白时，仍应按该值等齐请求体。</summary>
        [TestMethod]
        public void HttpRequestSplitter_MixedCaseNameAndPaddedValue_StillFramesBody()
        {
            var head = "POST /submit HTTP/1.1\r\nHost: a\r\nCoNtEnT-LeNgTh:   25 \r\n\r\n";
            var body = new string('x', 25);
            var bytes = Encoding.ASCII.GetBytes(head + body);
            var splitter = new HttpRequestSplitter();

            long consumed;
            ReadOnlySequence<byte> frame;
            var result = splitter.TrySplit(new ReadOnlySequence<byte>(bytes), out consumed, out frame);

            Assert.IsTrue(result, "大小写混写且值带空白的 Content-Length 应正常定帧。");
            Assert.AreEqual((long)bytes.Length, consumed, "consumed 应覆盖报头与 25 字节请求体。");
            Assert.AreEqual(body, Encoding.ASCII.GetString(frame.Slice(head.Length).ToArray()), "帧内请求体应完整。");
        }

        /// <summary>同名头重复出现时取最后一个，与解码阶段「后写覆盖」一致。</summary>
        [TestMethod]
        public void HttpRequestSplitter_DuplicateContentLength_UsesLastValue()
        {
            var head = "POST /dup HTTP/1.1\r\nHost: a\r\nContent-Length: 3\r\nContent-Length: 5\r\n\r\n";
            var bytes = Encoding.ASCII.GetBytes(head + "hello");
            var splitter = new HttpRequestSplitter();

            long consumed;
            ReadOnlySequence<byte> frame;
            var result = splitter.TrySplit(new ReadOnlySequence<byte>(bytes), out consumed, out frame);

            Assert.IsTrue(result, "重复 Content-Length 时应按最后一个值定帧。");
            Assert.AreEqual((long)bytes.Length, consumed, "consumed 应按最后一个 Content-Length（5）算整帧。");
        }

        /// <summary>头名与数值都跨段时，应仍按完整数值定帧。</summary>
        [TestMethod]
        public void HttpRequestSplitter_ContentLengthAcrossThreeSegments_SplitsBody()
        {
            var part1 = Encoding.ASCII.GetBytes("POST /multi HTTP/1.1\r\nHost: a\r\nContent-Len");
            var part2 = Encoding.ASCII.GetBytes("gth: 1");
            var part3 = Encoding.ASCII.GetBytes("2\r\n\r\n" + new string('y', 12));
            var buffer = CreateSequence(part1, part2, part3);
            var splitter = new HttpRequestSplitter();

            long consumed;
            ReadOnlySequence<byte> frame;
            var result = splitter.TrySplit(buffer, out consumed, out frame);

            Assert.IsTrue(result, "头名与数值跨三段时仍应切出整帧。");
            Assert.AreEqual((long)(part1.Length + part2.Length + part3.Length), consumed, "consumed 应覆盖三段总长。");
        }

        /// <summary>不带 Content-Length 的 GET 只按报头定帧，不必等请求体。</summary>
        [TestMethod]
        public void HttpRequestSplitter_GetWithoutContentLength_FramesHeaderOnly()
        {
            var head = "GET /ping HTTP/1.1\r\nHost: a\r\nUpgrade-Insecure-Requests: 1\r\n\r\n";
            var bytes = Encoding.ASCII.GetBytes(head);
            var splitter = new HttpRequestSplitter();

            long consumed;
            ReadOnlySequence<byte> frame;
            var result = splitter.TrySplit(new ReadOnlySequence<byte>(bytes), out consumed, out frame);

            Assert.IsTrue(result, "无 Content-Length 的请求应立即按报头定帧。");
            Assert.AreEqual((long)bytes.Length, consumed, "consumed 应等于报头长度。");
        }

        /// <summary>Transfer-Encoding 的值大小写混写时，仍应识别为 chunked 并正常切帧。</summary>
        [TestMethod]
        public void HttpRequestSplitter_MixedCaseChunked_SplitsWholeRequest()
        {
            var body = "5\r\nhello\r\n0\r\n\r\n";
            var bytes = Encoding.ASCII.GetBytes(
                "POST /c HTTP/1.1\r\nHost: a\r\nTransfer-Encoding: Gzip, ChUnKeD\r\n\r\n" + body);

            var splitter = new HttpRequestSplitter();

            long consumed;
            ReadOnlySequence<byte> frame;
            var result = splitter.TrySplit(new ReadOnlySequence<byte>(bytes), out consumed, out frame);

            Assert.IsTrue(result, "值里含大小写混写的 chunked 时应按 chunked 切帧。");
            Assert.AreEqual((long)bytes.Length, consumed, "consumed 应等于报头加原始 chunk 字节总长。");
        }

        /// <summary>Content-Length 不是整数时按 400 拒绝，不能当成 0 放行。</summary>
        [TestMethod]
        public void HttpRequestSplitter_NonNumericContentLength_Throws400()
        {
            var bytes = Encoding.ASCII.GetBytes("POST /bad HTTP/1.1\r\nHost: a\r\nContent-Length: 1x\r\n\r\n");
            var splitter = new HttpRequestSplitter();

            long consumed;
            ReadOnlySequence<byte> frame;
            var ex = Assert.ThrowsExactly<FrameProtocolException>(
                () => splitter.TrySplit(new ReadOnlySequence<byte>(bytes), out consumed, out frame));

            Assert.AreEqual(HttpProtocolErrorCodes.BadRequest, ex.Message, "非整数 Content-Length 应回 400。");
        }

        /// <summary>负的 Content-Length 按 400 拒绝；"-0" 解析为 0 仍按 0 字节定帧。</summary>
        [TestMethod]
        public void HttpRequestSplitter_NegativeContentLength_Throws400ButMinusZeroFrames()
        {
            var negative = Encoding.ASCII.GetBytes("POST /n HTTP/1.1\r\nHost: a\r\nContent-Length: -1\r\n\r\n");
            var minusZero = Encoding.ASCII.GetBytes("POST /z HTTP/1.1\r\nHost: a\r\nContent-Length: -0\r\n\r\n");

            var splitter = new HttpRequestSplitter();

            long consumed;
            ReadOnlySequence<byte> frame;
            var ex = Assert.ThrowsExactly<FrameProtocolException>(
                () => splitter.TrySplit(new ReadOnlySequence<byte>(negative), out consumed, out frame));

            Assert.AreEqual(HttpProtocolErrorCodes.BadRequest, ex.Message, "负的 Content-Length 应回 400。");

            Assert.IsTrue(splitter.TrySplit(new ReadOnlySequence<byte>(minusZero), out consumed, out frame), "-0 与 int.TryParse 一致，按 0 定帧。");
            Assert.AreEqual((long)minusZero.Length, consumed, "-0 时 consumed 应等于报头长度。");
        }

        /// <summary>流水线里第一帧带请求体时，第二帧起点必须落在请求体之后，不能落在报头之后。</summary>
        [TestMethod]
        public void HttpRequestSplitter_PipelinedPostWithBody_SecondFrameStartsAfterBody()
        {
            var firstText = "POST /one HTTP/1.1\r\nHost: a\r\nContent-Length: 5\r\n\r\nhello";
            var secondText = "POST /two HTTP/1.1\r\nHost: b\r\nContent-Length: 3\r\n\r\nabc";
            var bytes = Encoding.ASCII.GetBytes(firstText + secondText);
            var buffer = new ReadOnlySequence<byte>(bytes);
            var splitter = new HttpRequestSplitter();

            long consumed;
            ReadOnlySequence<byte> frame;
            Assert.IsTrue(splitter.TrySplit(buffer, out consumed, out frame), "第一帧应切分成功。");
            Assert.AreEqual((long)firstText.Length, consumed, "第一帧的 consumed 应覆盖报头与 5 字节请求体。");
            Assert.AreEqual(firstText, Encoding.ASCII.GetString(frame.ToArray()), "第一帧应是完整的第一请求。");

            var rest = buffer.Slice(consumed);

            Assert.IsTrue(splitter.TrySplit(rest, out consumed, out frame), "第二帧应切分成功。");
            Assert.AreEqual((long)secondText.Length, consumed, "第二帧的 consumed 应是第二请求全长。");
            Assert.AreEqual(secondText, Encoding.ASCII.GetString(frame.ToArray()), "第二帧应是完整的第二请求。");
            Assert.IsTrue(rest.Slice(consumed).IsEmpty, "两帧之后缓冲应正好消费完。");
        }

        /// <summary>把多段内存拼成一个跨段的可读字节序列。</summary>
        /// <param name="parts">按顺序拼接的各段</param>
        /// <returns>跨段序列</returns>
        private static ReadOnlySequence<byte> CreateSequence(params byte[][] parts)
        {
            Segment first = null;
            Segment last = null;

            foreach (var part in parts)
            {
                var segment = new Segment(part);

                if (first == null)
                {
                    first = segment;
                }
                else
                {
                    last.SetNext(segment);
                }

                last = segment;
            }

            return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
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