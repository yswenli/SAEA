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
*文件名： MultipartBodyTests
*版本号： net10.0
*唯一标识：87a45d66-210f-4039-95fb-e6dba49eae7e
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/24 14:21:06
*描述：multipart 请求体解析用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/24 15:59:54
*修改人： yswenli
*版本号： net10.0
*描述：新增跨读边界、文件分段落盘、近似定界串、截断报头、落盘 JSON 与异常流用例
*
*****************************************************************************/
using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Http.Base;
using SAEA.Http.Model;

namespace SAEA.Http.Tests
{
    /// <summary>multipart 请求体解析：分段、字段、文件与畸形输入的行为用例。</summary>
    [TestClass]
    public class MultipartBodyTests
    {
        private const string Boundary = "saeaTestBoundary";

        private const string MultipartContentType = "multipart/form-data; boundary=" + Boundary;

        /// <summary>
        /// 单文件解析后文件数据必须与原始字节逐字节一致，数据里出现的 CRLF、单独的 --、以及近乎 boundary 的串都不能被误判成结束。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_SingleFileWithBoundaryLikeBytes_KeepsExactBytes()
        {
            var payload = Concat(
                Encoding.UTF8.GetBytes("line1\r\n"),
                new byte[] { 0x00, 0xFF, 0x0D, 0x0A },
                Encoding.UTF8.GetBytes("--"),
                Encoding.UTF8.GetBytes("\r\n--boun"),
                Encoding.UTF8.GetBytes("\r\n--" + Boundary + "X"),
                new byte[] { 0x0D, 0x0A, 0x01 });

            var message = Parse(MultipartContentType, BuildMultipart(Boundary,
                Part.File("upload", "data.bin", "application/octet-stream", payload)));

            Assert.AreEqual(1, message.PostFiles.Count, "应解析出一个文件分段。");
            Assert.AreEqual("upload", message.PostFiles[0].Name, "应解析出表单字段名。");
            Assert.AreEqual("data.bin", message.PostFiles[0].FileName, "应解析出文件名。");
            Assert.AreEqual("application/octet-stream", message.PostFiles[0].Type, "应解析出 MIME 类型。");
            CollectionAssert.AreEqual(payload, ReadPart(message.PostFiles[0]), "文件数据必须与原始字节完全一致。");
            Assert.AreEqual(0, message.Forms.Count, "纯文件请求不应产生表单字段。");
        }

        /// <summary>
        /// 同一请求里的多个文件都要进 PostFiles，各自的字节、字段名、文件名与类型都要正确。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_TwoFiles_ReturnsBothFiles()
        {
            var first = Encoding.UTF8.GetBytes("first-file-content");
            var second = new byte[] { 0x02, 0x00, 0x0D, 0x0A, 0x03 };

            var message = Parse(MultipartContentType, BuildMultipart(Boundary,
                Part.File("a", "a.txt", "text/plain", first),
                Part.File("b", "b.bin", "application/octet-stream", second)));

            Assert.AreEqual(2, message.PostFiles.Count, "两个文件分段都应返回。");
            Assert.AreEqual("a", message.PostFiles[0].Name, "第一个文件的字段名应正确。");
            Assert.AreEqual("a.txt", message.PostFiles[0].FileName, "第一个文件的文件名应正确。");
            Assert.AreEqual("text/plain", message.PostFiles[0].Type, "第一个文件的类型应正确。");
            CollectionAssert.AreEqual(first, ReadPart(message.PostFiles[0]), "第一个文件的数据应正确。");
            Assert.AreEqual("b", message.PostFiles[1].Name, "第二个文件的字段名应正确。");
            Assert.AreEqual("b.bin", message.PostFiles[1].FileName, "第二个文件的文件名应正确。");
            Assert.AreEqual("application/octet-stream", message.PostFiles[1].Type, "第二个文件的类型应正确。");
            CollectionAssert.AreEqual(second, ReadPart(message.PostFiles[1]), "第二个文件的数据应正确。");
        }

        /// <summary>
        /// 文件在表单字段之后时，字段值与文件数据都要解析正确。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_FileThenField_ParsesBoth()
        {
            var fileData = Encoding.UTF8.GetBytes("payload-1");

            var message = Parse(MultipartContentType, BuildMultipart(Boundary,
                Part.File("f", "f.txt", "text/plain", fileData),
                Part.Field("title", "hello")));

            Assert.AreEqual("hello", message.Forms["title"], "文件之后的表单字段应解析出来。");
            Assert.AreEqual(1, message.PostFiles.Count, "应解析出一个文件。");
            CollectionAssert.AreEqual(fileData, ReadPart(message.PostFiles[0]), "文件数据应正确。");
        }

        /// <summary>
        /// 文件在表单字段之前时，字段值与文件数据都要解析正确。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_FieldThenFile_ParsesBoth()
        {
            var fileData = Encoding.UTF8.GetBytes("payload-2");

            var message = Parse(MultipartContentType, BuildMultipart(Boundary,
                Part.Field("title", "world"),
                Part.File("f", "f.txt", "text/plain", fileData)));

            Assert.AreEqual("world", message.Forms["title"], "文件之前的表单字段应解析出来。");
            Assert.AreEqual(1, message.PostFiles.Count, "应解析出一个文件。");
            CollectionAssert.AreEqual(fileData, ReadPart(message.PostFiles[0]), "文件数据应正确。");
        }

        /// <summary>
        /// filename="" 的分段仍按文件处理，数据为空数组。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_EmptyFileNameFile_ReturnsFilePartWithEmptyData()
        {
            var message = Parse(MultipartContentType, BuildMultipart(Boundary,
                Part.File("empty", "", "application/octet-stream", Array.Empty<byte>())));

            Assert.AreEqual(1, message.PostFiles.Count, "空文件仍应产生一个文件分段。");
            Assert.AreEqual("empty", message.PostFiles[0].Name, "字段名应正确。");
            Assert.AreEqual(string.Empty, message.PostFiles[0].FileName, "文件名为空串。");
            Assert.AreEqual(0, message.PostFiles[0].Data.Length, "空文件的数据长度应为 0。");
        }

        /// <summary>
        /// boundary 带引号、且不在 Content-Type 的第二段时仍能解析，boundary 与 IsFormData 都要正确。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_QuotedBoundaryAfterCharset_ParsesForm()
        {
            const string contentType = "multipart/form-data; charset=utf-8; boundary=\"" + Boundary + "\"";

            var request = BuildRequest(contentType, BuildMultipart(Boundary, Part.Field("k", "v")));

            RequestDataReader.Analysis(request.AsSpan(), out var message);

            Assert.IsTrue(message.IsFormData, "带 boundary 的 multipart 应标记为表单请求。");
            Assert.AreEqual("--" + Boundary, message.Boundary, "boundary 应去掉引号并保留 -- 前缀。");
            Assert.IsTrue(RequestDataReader.AnalysisBody(request, message), "正文应解析成功。");
            Assert.AreEqual("v", message.Forms["k"], "带引号的 boundary 应能正常切分分段。");
        }

        /// <summary>
        /// 只有表单字段没有文件时，Forms 有值且 PostFiles 为空。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_OnlyFields_FillsFormsWithoutFiles()
        {
            var message = Parse(MultipartContentType, BuildMultipart(Boundary,
                Part.Field("a", "1"),
                Part.Field("b", "中文值")));

            Assert.AreEqual(2, message.Forms.Count, "两个表单字段都应解析出来。");
            Assert.AreEqual("1", message.Forms["a"], "第一个字段值应正确。");
            Assert.AreEqual("中文值", message.Forms["b"], "字段值应按 UTF-8 解码。");
            Assert.AreEqual(0, message.PostFiles.Count, "没有文件时 PostFiles 应为空。");
        }

        /// <summary>
        /// 结束符之后的额外字节不影响已解析的分段。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_TrailingBytesAfterClosingMarker_IgnoresThem()
        {
            var fileData = Encoding.UTF8.GetBytes("kept");

            var body = Concat(
                BuildMultipart(Boundary, Part.File("f", "f.txt", "text/plain", fileData)),
                Encoding.UTF8.GetBytes("\r\nextra--" + Boundary + "--\r\njunk"));

            var message = Parse(MultipartContentType, body);

            Assert.AreEqual(1, message.PostFiles.Count, "结束符之后的内容不应产生新分段。");
            CollectionAssert.AreEqual(fileData, ReadPart(message.PostFiles[0]), "已解析的文件数据应保持不变。");
        }

        /// <summary>
        /// 1MB 文件解析的分配量必须与文件大小同阶，旧实现按块全量复制会分配数百 MB。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_OneMegabyteFile_AllocatesFarLessThanThirtyTwoMegabytes()
        {
            // 先跑一次小报文，避免 JIT 与首次走通的路径噪音算进分配差
            Parse(MultipartContentType, BuildMultipart(Boundary,
                Part.File("warm", "warm.bin", "application/octet-stream", new byte[4096])));

            var payload = new byte[1024 * 1024];

            for (var i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)(i % 251);
            }

            var request = BuildRequest(MultipartContentType, BuildMultipart(Boundary,
                Part.File("big", "big.bin", "application/octet-stream", payload)));

            var before = GC.GetTotalAllocatedBytes(true);

            RequestDataReader.Analysis(request.AsSpan(), out var message);

            var parsed = RequestDataReader.AnalysisBody(request, message);

            var allocated = GC.GetTotalAllocatedBytes(true) - before;

            Assert.IsTrue(parsed, "1MB 文件应解析成功。");
            Assert.AreEqual(payload.Length, message.PostFiles[0].Data.Length, "文件数据长度应与原始长度一致。");
            Assert.IsTrue(allocated < 32L * 1024 * 1024,
                "解析 1MB 文件应只按需复制文件数据，实测分配 " + allocated + " 字节。");
        }

        /// <summary>
        /// 只有报头没有数据、缺少 boundary 参数、以及正文乱码这三种畸形输入都不抛异常，返回 false 或空集合。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_MalformedBody_DoesNotThrowAndReturnsEmpty()
        {
            // 只有报头块，没有数据与结束符
            var truncated = BuildRequest(MultipartContentType,
                Encoding.ASCII.GetBytes("--" + Boundary + "\r\nContent-Disposition: form-data; name=\"f\"\r\n\r\n"));
            RequestDataReader.Analysis(truncated.AsSpan(), out var truncatedMessage);
            Assert.IsFalse(RequestDataReader.AnalysisBody(truncated, truncatedMessage), "分段被截断时应返回 false。");
            Assert.AreEqual(0, truncatedMessage.PostFiles.Count, "截断的请求不应产生文件。");

            // Content-Type 是 multipart 但没有 boundary 参数
            var noBoundary = BuildRequest("multipart/form-data",
                Encoding.ASCII.GetBytes("--x\r\n\r\n--x--\r\n"));
            RequestDataReader.Analysis(noBoundary.AsSpan(), out var noBoundaryMessage);
            Assert.IsFalse(noBoundaryMessage.IsFormData, "缺少 boundary 参数时不应标记为表单请求。");
            Assert.IsNull(noBoundaryMessage.Boundary, "缺少 boundary 参数时 Boundary 应为 null。");
            Assert.IsFalse(RequestDataReader.AnalysisBody(noBoundary, noBoundaryMessage), "缺少 boundary 时不应解析 multipart。");

            // 正文与 multipart 格式无关
            var garbage = BuildRequest(MultipartContentType, Encoding.UTF8.GetBytes("not a multipart body"));
            RequestDataReader.Analysis(garbage.AsSpan(), out var garbageMessage);
            Assert.IsFalse(RequestDataReader.AnalysisBody(garbage, garbageMessage), "乱码正文应返回 false。");
            Assert.AreEqual(0, garbageMessage.Forms.Count, "乱码正文不应产生表单字段。");
            Assert.AreEqual(0, garbageMessage.PostFiles.Count, "乱码正文不应产生文件。");

            // 空正文
            var empty = BuildRequest(MultipartContentType, Array.Empty<byte>());
            RequestDataReader.Analysis(empty.AsSpan(), out var emptyMessage);
            Assert.IsFalse(RequestDataReader.AnalysisBody(empty, emptyMessage), "空正文应返回 false。");
        }

        /// <summary>
        /// 起始 boundary 之前出现前导内容时，字段与文件仍应正常解析出来。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_PreambleBeforeFirstBoundary_ParsesForm()
        {
            var fileData = Encoding.UTF8.GetBytes("preamble-payload");

            var body = Concat(
                Encoding.ASCII.GetBytes("This is a preamble\r\n"),
                BuildMultipart(Boundary,
                    Part.Field("title", "hello"),
                    Part.File("f", "f.txt", "text/plain", fileData)));

            var message = Parse(MultipartContentType, body);

            Assert.AreEqual("hello", message.Forms["title"], "前导内容之后的表单字段应解析出来。");
            Assert.AreEqual(1, message.PostFiles.Count, "前导内容之后应解析出一个文件。");
            CollectionAssert.AreEqual(fileData, ReadPart(message.PostFiles[0]), "文件数据应与原始字节一致。");
        }

        /// <summary>
        /// 分段数超过 1024 时停止解析，保留已解析字段且不抛异常。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_MoreThanMaxParts_StopsAtLimit()
        {
            var parts = new Part[1100];

            for (var i = 0; i < parts.Length; i++)
            {
                parts[i] = Part.Field("f" + i, "v");
            }

            var body = BuildMultipart(Boundary, parts);

            Assert.IsTrue(body.Length < 200 * 1024, "用例前置条件：超限报文总量应控制在 200KB 内。实际 " + body.Length + " 字节。");

            var message = Parse(MultipartContentType, body);

            Assert.IsTrue(message.Forms.Count <= 1024,
                "分段数超过 1024 后应停止解析，最多保留 1024 个字段。实际 " + message.Forms.Count + " 个。");
        }

        /// <summary>
        /// 单个分段报头超过 16KB 时停止解析，不抛异常且该分段不被解析出来。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_PartHeaderOverLimit_StopsParsing()
        {
            var largeHeader = "X-Large: " + new string('a', 20 * 1024) + "\r\n";

            var oversized = new Part
            {
                Headers = "Content-Disposition: form-data; name=\"big\"\r\n" + largeHeader,
                Data = Encoding.UTF8.GetBytes("v")
            };

            // 首个分段就超限，解析器应直接停止，不产生任何字段或文件
            var request = BuildRequest(MultipartContentType, BuildMultipart(Boundary, oversized));

            RequestDataReader.Analysis(request.AsSpan(), out var message);

            var parsed = RequestDataReader.AnalysisBody(request, message);

            Assert.IsFalse(parsed, "报头超限属于解析失败，应返回 false。");
            Assert.AreEqual(0, message.Forms.Count, "报头超限的分段不应被解析成字段。");
            Assert.AreEqual(0, message.PostFiles.Count, "报头超限的分段不应被解析成文件。");
        }

        /// <summary>
        /// 让「CRLF + boundary」正好跨过 64KB 读缓冲边界，验证定界串跨块仍能匹配且文件字节一致。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_BoundarySplitsAcrossReadBuffer_ParsesFile()
        {
            var prefix = Concat(
                Encoding.ASCII.GetBytes("--" + Boundary + "\r\n"),
                Encoding.ASCII.GetBytes("Content-Disposition: form-data; name=\"f\"; filename=\"f.bin\"\r\n"),
                Encoding.ASCII.GetBytes("Content-Type: application/octet-stream\r\n"),
                Encoding.ASCII.GetBytes("\r\n"));

            var suffix = Encoding.ASCII.GetBytes("\r\n--" + Boundary + "--\r\n");

            // 让定界串从读缓冲末尾前 3 字节开始，必然横跨两次读取
            var delimiterStart = 64 * 1024 - 3;

            var payload = new byte[delimiterStart - prefix.Length];

            for (var i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)(i % 251);
            }

            var message = Parse(MultipartContentType, Concat(prefix, payload, suffix));

            Assert.AreEqual(1, message.PostFiles.Count, "跨读边界的定界串不应吞掉文件分段。");
            CollectionAssert.AreEqual(payload, ReadPart(message.PostFiles[0]), "跨块匹配后文件字节必须完全一致。");
        }

        /// <summary>
        /// 每次读取只返回 3 字节时，起始 boundary 仍应被识别，文件字节保持一致。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_StartMarkerAcrossTinyReads_ParsesFile()
        {
            var payload = new byte[] { 0x01, 0x0D, 0x0A, 0xFF, 0x02 };

            var body = BuildMultipart(Boundary, Part.File("upload", "data.bin", "application/octet-stream", payload));

            var message = new HttpMessage { ContentType = MultipartContentType };

            message.Boundary = "--" + Boundary;

            // 每次最多 3 字节：起始 marker 必然被拆到多次读取里
            message.Body = new CappedReadStream(body, 3);

            Assert.IsTrue(RequestDataReader.ParseBody(message), "每次只读到 3 字节时仍应解析成功。");
            Assert.AreEqual(1, message.PostFiles.Count, "应解析出一个文件分段。");
            CollectionAssert.AreEqual(payload, ReadPart(message.PostFiles[0]), "文件字节必须与原始字节一致。");
        }

        /// <summary>
        /// 文件分段超过落盘阈值时改用临时文件承载，消息释放后临时文件必须删除。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_LargeFilePart_SpillsToTempFileAndDeletesOnDispose()
        {
            var tempDirectory = CreateTempDirectory();

            try
            {
                var payload = new byte[8 * 1024];

                for (var i = 0; i < payload.Length; i++)
                {
                    payload[i] = (byte)(i % 241);
                }

                var request = BuildRequest(MultipartContentType, BuildMultipart(Boundary,
                    Part.File("big", "big.bin", "application/octet-stream", payload)));

                RequestDataReader.Analysis(request.AsSpan(), out var message);

                message.LargeBodyThreshold = 1024;
                message.TempDirectory = tempDirectory;

                Assert.IsTrue(RequestDataReader.AnalysisBody(request, message), "超过阈值的文件分段应解析成功。");
                Assert.AreEqual(1, message.PostFiles.Count, "应解析出一个文件分段。");

                var stream = message.PostFiles[0].Data as HttpRequestBodyStream;

                Assert.IsNotNull(stream, "文件分段的载体应为请求体流。");
                Assert.IsTrue(stream.IsFileBacked, "超过阈值的文件分段应落盘。");

                var path = stream.TempFilePath;

                Assert.IsTrue(File.Exists(path), "落盘后临时文件应存在。");
                CollectionAssert.AreEqual(payload, ReadPart(message.PostFiles[0]), "落盘后读回的文件字节必须一致。");

                message.Dispose();

                Assert.IsFalse(File.Exists(path), "消息释放后临时文件应被删除。");
            }
            finally
            {
                // 用例独占的随机子目录用完即删，避免临时目录按用例累积
                if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
            }
        }

        /// <summary>
        /// 文件内容里出现不完整的 boundary 前缀与近似串时，不能被误判成分段结束。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_PayloadContainsBoundaryPrefix_KeepsBytes()
        {
            var payload = Concat(
                Encoding.UTF8.GetBytes("head"),
                Encoding.UTF8.GetBytes("\r\n--" + Boundary.Substring(0, Boundary.Length - 1)),
                Encoding.UTF8.GetBytes("\r\n--" + Boundary + "X"),
                Encoding.UTF8.GetBytes("tail"));

            var message = Parse(MultipartContentType, BuildMultipart(Boundary,
                Part.File("f", "f.bin", "application/octet-stream", payload)));

            Assert.AreEqual(1, message.PostFiles.Count, "近似定界串不应产生额外分段。");
            CollectionAssert.AreEqual(payload, ReadPart(message.PostFiles[0]), "近似定界串必须原样保留。");
        }

        /// <summary>
        /// 第二个分段的报头块被截断时，丢弃该分段、保留第一个分段且不抛异常。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_TruncatedInsideHeaderBlock_KeepsEarlierParts()
        {
            var firstValue = Encoding.UTF8.GetBytes("first");

            var body = Concat(
                Encoding.ASCII.GetBytes("--" + Boundary + "\r\n"),
                Encoding.ASCII.GetBytes("Content-Disposition: form-data; name=\"a\"\r\n\r\n"),
                firstValue,
                Encoding.ASCII.GetBytes("\r\n--" + Boundary + "\r\n"),
                Encoding.ASCII.GetBytes("Content-Disposition: form-data; name=\"b\"\r\n"));

            var request = BuildRequest(MultipartContentType, body);

            RequestDataReader.Analysis(request.AsSpan(), out var message);

            Assert.IsFalse(RequestDataReader.AnalysisBody(request, message), "报头被截断时应返回 false。");
            Assert.AreEqual("first", message.Forms["a"], "截断前的完整分段应保留。");
            Assert.IsFalse(message.Forms.ContainsKey("b"), "报头未结束的分段不应被解析。");
        }

        /// <summary>
        /// 文件载体的 JSON 请求体直接从流读取文本，解析结果与原文一致。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_JsonBodyLargeThanThreshold_ReadsFromStream()
        {
            var tempDirectory = CreateTempDirectory();

            try
            {
                var json = "{\"value\":\"" + new string('x', 64) + "\"}";

                var path = Path.Combine(tempDirectory, "json-body.tmp");

                File.WriteAllText(path, json, new UTF8Encoding(false));

                var message = new HttpMessage { ContentType = "application/json; charset=UTF-8" };

                message.LargeBodyThreshold = 16;
                message.TempDirectory = tempDirectory;

                // 用文件载体模拟超过落盘阈值的请求体，验证文本解析走流而不是整段字节数组
                message.Body = HttpRequestBodyStream.FromFile(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), path);

                Assert.IsTrue(RequestDataReader.ParseBody(message), "落盘请求体的 JSON 应解析成功。");
                Assert.AreEqual(json, message.Json, "JSON 文本应与原文一致。");

                message.Dispose();

                Assert.IsFalse(File.Exists(path), "释放消息后 JSON 临时文件应删除。");
            }
            finally
            {
                // 用例独占的随机子目录用完即删，避免临时目录按用例累积
                if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
            }
        }

        /// <summary>
        /// 读取抛 IOException 的请求体只返回 false，不向调用方抛异常；重复解析不出现按次累积的缓冲分配。
        /// </summary>
        [TestMethod]
        public void RequestDataReader_ThrowingStream_ReturnsFalseWithoutLeakingReadBuffer()
        {
            var broken = new HttpMessage { ContentType = "application/json; charset=UTF-8" };

            broken.Body = new ThrowingStream(64);

            Assert.IsFalse(RequestDataReader.ParseBody(broken), "读取抛异常时应按解析失败返回 false。");

            // 解析器每次租一块 64KB 读缓冲；若漏归还，重复解析会按次累积同量级分配
            var body = BuildMultipart(Boundary, Part.Field("k", "v"));

            var warm = new HttpMessage();

            warm.ResetForNextRequest();
            warm.ContentType = MultipartContentType;
            warm.Boundary = "--" + Boundary;
            warm.Body = new MemoryStream(body, false);

            RequestDataReader.ParseBody(warm);

            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var i = 0; i < 100; i++)
            {
                var loop = new HttpMessage();

                loop.ResetForNextRequest();
                loop.ContentType = MultipartContentType;
                loop.Boundary = "--" + Boundary;
                loop.Body = new MemoryStream(body, false);

                RequestDataReader.ParseBody(loop);
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsTrue(allocated < 8L * 1024 * 1024,
                "重复解析不应按次累积读缓冲分配，实测 " + allocated + " 字节。");
        }

        /// <summary>
        /// 解析完整请求并断言成功，返回消息对象。
        /// </summary>
        /// <param name="contentType">请求的 Content-Type 报头值</param>
        /// <param name="body">请求体字节</param>
        /// <returns>解析成功的消息对象</returns>
        private static HttpMessage Parse(string contentType, byte[] body)
        {
            var request = BuildRequest(contentType, body);

            RequestDataReader.Analysis(request.AsSpan(), out var message);

            Assert.IsNotNull(message, "请求帧应能解析出报头。");
            Assert.IsTrue(RequestDataReader.AnalysisBody(request, message), "正文已收全时应解析成功。");

            return message;
        }

        /// <summary>
        /// 拼一个带 Content-Length 的 POST 请求帧。
        /// </summary>
        /// <param name="contentType">请求的 Content-Type 报头值</param>
        /// <param name="body">请求体字节</param>
        /// <returns>完整请求帧</returns>
        private static byte[] BuildRequest(string contentType, byte[] body)
        {
            using (var stream = new MemoryStream())
            {
                WriteAscii(stream, "POST /upload HTTP/1.1\r\n");
                WriteAscii(stream, "Host: h\r\n");
                WriteAscii(stream, "Content-Type: " + contentType + "\r\n");
                WriteAscii(stream, "Content-Length: " + body.Length + "\r\n");
                WriteAscii(stream, "\r\n");

                if (body.Length > 0) stream.Write(body, 0, body.Length);

                return stream.ToArray();
            }
        }

        /// <summary>
        /// 按 multipart 规则拼接请求体，末尾带结束符。
        /// </summary>
        /// <param name="boundary">不带前导 -- 的分隔串</param>
        /// <param name="parts">各分段</param>
        /// <returns>完整请求体字节</returns>
        private static byte[] BuildMultipart(string boundary, params Part[] parts)
        {
            using (var stream = new MemoryStream())
            {
                foreach (var part in parts)
                {
                    WriteAscii(stream, "--" + boundary + "\r\n");
                    WriteAscii(stream, part.Headers);
                    WriteAscii(stream, "\r\n");

                    if (part.Data.Length > 0) stream.Write(part.Data, 0, part.Data.Length);

                    WriteAscii(stream, "\r\n");
                }

                WriteAscii(stream, "--" + boundary + "--\r\n");

                return stream.ToArray();
            }
        }

        private static void WriteAscii(Stream stream, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);

            stream.Write(bytes, 0, bytes.Length);
        }

        private static byte[] Concat(params byte[][] items)
        {
            using (var stream = new MemoryStream())
            {
                foreach (var item in items)
                {
                    stream.Write(item, 0, item.Length);
                }

                return stream.ToArray();
            }
        }

        /// <summary>
        /// 读出文件分段的全部字节，读前先回到起点。
        /// </summary>
        /// <param name="part">文件分段</param>
        /// <returns>文件内容字节</returns>
        private static byte[] ReadPart(FilePart part)
        {
            Assert.IsNotNull(part, "文件分段不应为 null。");
            Assert.IsNotNull(part.Data, "文件数据流不应为 null。");

            part.Data.Position = 0;

            using (var buffer = new MemoryStream())
            {
                part.Data.CopyTo(buffer);

                return buffer.ToArray();
            }
        }

        /// <summary>
        /// 创建一个用例独占的临时目录，避免并发用例互相清理。
        /// </summary>
        /// <returns>临时目录路径</returns>
        private static string CreateTempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "saea-http-tests", Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(directory);

            return directory;
        }

        /// <summary>每次读取最多返回指定字节数的只读流，用于模拟分块到达。</summary>
        private sealed class CappedReadStream : Stream
        {
            private readonly byte[] _data;

            private readonly int _maxRead;

            private int _position;

            public CappedReadStream(byte[] data, int maxRead)
            {
                _data = data;
                _maxRead = maxRead;
            }

            public override bool CanRead { get { return true; } }

            public override bool CanSeek { get { return true; } }

            public override bool CanWrite { get { return false; } }

            public override long Length { get { return _data.Length; } }

            public override long Position
            {
                get { return _position; }
                set { _position = (int)value; }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                var remaining = _data.Length - _position;

                if (remaining <= 0) return 0;

                var take = Math.Min(Math.Min(count, _maxRead), remaining);

                Buffer.BlockCopy(_data, _position, buffer, offset, take);

                _position += take;

                return take;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        _position = (int)offset;
                        break;
                    case SeekOrigin.Current:
                        _position += (int)offset;
                        break;
                    default:
                        _position = _data.Length + (int)offset;
                        break;
                }

                return _position;
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override void Flush()
            {
            }
        }

        /// <summary>Length 非零但读取必定抛 IOException 的流，用于覆盖解析异常路径。</summary>
        private sealed class ThrowingStream : Stream
        {
            private readonly long _length;

            public ThrowingStream(long length)
            {
                _length = length;
            }

            public override bool CanRead { get { return true; } }

            public override bool CanSeek { get { return true; } }

            public override bool CanWrite { get { return false; } }

            public override long Length { get { return _length; } }

            public override long Position { get; set; }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new IOException("模拟请求体读取失败。");
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return offset;
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override void Flush()
            {
            }
        }

        /// <summary>一个 multipart 分段的报头文本与数据。</summary>
        private sealed class Part
        {
            /// <summary>报头文本，每行都以 CRLF 结尾。</summary>
            public string Headers;

            /// <summary>分段数据。</summary>
            public byte[] Data = Array.Empty<byte>();

            /// <summary>
            /// 构造文件分段。
            /// </summary>
            /// <param name="name">表单字段名</param>
            /// <param name="fileName">客户端提交的文件名</param>
            /// <param name="type">MIME 类型，为 null 时不写 Content-Type 报头</param>
            /// <param name="data">文件内容</param>
            /// <returns>文件分段</returns>
            public static Part File(string name, string fileName, string type, byte[] data)
            {
                var headers = "Content-Disposition: form-data; name=\"" + name + "\"; filename=\"" + fileName + "\"\r\n";

                if (type != null) headers += "Content-Type: " + type + "\r\n";

                return new Part { Headers = headers, Data = data ?? Array.Empty<byte>() };
            }

            /// <summary>
            /// 构造普通表单字段分段。
            /// </summary>
            /// <param name="name">表单字段名</param>
            /// <param name="value">字段值</param>
            /// <returns>表单字段分段</returns>
            public static Part Field(string name, string value)
            {
                return new Part
                {
                    Headers = "Content-Disposition: form-data; name=\"" + name + "\"\r\n",
                    Data = Encoding.UTF8.GetBytes(value)
                };
            }
        }
    }
}