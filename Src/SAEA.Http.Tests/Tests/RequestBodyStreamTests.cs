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
*文件名： RequestBodyStreamTests
*版本号： net10.0
*唯一标识：c8d1f4a2-6b39-4e07-9a58-3f2b7d0e6c14
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/24 15:41:00
*描述：请求体流载体与落盘生命周期的行为用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/24 15:41:00
*修改人： yswenli
*版本号： net10.0
*描述：新建，覆盖默认阈值、阈值边界的内存/落盘、临时文件删除、流重复读取与文件分段保存
*
*****************************************************************************/
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Http.Base;
using SAEA.Http.Model;
using SAEA.Http.Tests.Tests.Support;
using SAEA.Sockets.Pipeline;

namespace SAEA.Http.Tests
{
    /// <summary>请求体流载体与落盘生命周期的行为用例。</summary>
    [TestClass]
    public class RequestBodyStreamTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 落盘阈值默认应为 1MB。
        /// </summary>
        [TestMethod]
        public void WebConfig_LargeBodyThreshold_DefaultIsOneMB()
        {
            Assert.AreEqual(1024 * 1024, new WebConfig().LargeBodyThreshold, "落盘阈值默认应为 1MB。");
        }

        /// <summary>
        /// 请求体超过阈值时应落到临时文件，读出字节仍逐字节一致。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpRequestDecoder_BodyOverThreshold_IsFileBackedAndByteExact()
        {
            var body = new byte[32];

            for (var i = 0; i < body.Length; i++)
            {
                body[i] = (byte)(i * 7 + 1);
            }

            var tempDirectory = CreateTempDirectory();

            var message = await DecodeAsync(16, tempDirectory, "body-over-threshold", body);

            try
            {
                var bodyStream = message.Body as HttpRequestBodyStream;

                Assert.IsNotNull(bodyStream, "请求体应是 HttpRequestBodyStream。");
                Assert.IsTrue(bodyStream.IsFileBacked, "超过阈值时应落到临时文件。");
                Assert.IsFalse(string.IsNullOrEmpty(bodyStream.TempFilePath), "文件载体应记录临时文件路径。");
                CollectionAssert.AreEqual(body, ReadAll(bodyStream), "落盘后的请求体字节必须与原始字节一致。");
            }
            finally
            {
                message.Dispose();

                DeleteDirectory(tempDirectory);
            }
        }

        /// <summary>
        /// 请求体等于阈值时仍应留在内存载体，不产生临时文件。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpRequestDecoder_BodyAtThreshold_StaysInMemory()
        {
            var body = new byte[32];

            for (var i = 0; i < body.Length; i++)
            {
                body[i] = (byte)i;
            }

            var tempDirectory = CreateTempDirectory();

            var message = await DecodeAsync(32, tempDirectory, "body-at-threshold", body);

            try
            {
                var bodyStream = message.Body as HttpRequestBodyStream;

                Assert.IsNotNull(bodyStream, "请求体应是 HttpRequestBodyStream。");
                Assert.IsFalse(bodyStream.IsFileBacked, "等于阈值时应留在内存。");
                Assert.IsNull(bodyStream.TempFilePath, "内存载体不应有临时文件路径。");
                CollectionAssert.AreEqual(body, ReadAll(bodyStream), "内存载体的字节必须与原始字节一致。");
            }
            finally
            {
                message.Dispose();

                DeleteDirectory(tempDirectory);
            }
        }

        /// <summary>
        /// 消息释放后，落盘产生的临时文件应被删除。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpMessage_Dispose_DeletesSpilledTempFile()
        {
            var tempDirectory = CreateTempDirectory();

            var message = await DecodeAsync(1, tempDirectory, "dispose-spilled", Encoding.UTF8.GetBytes("spilled-body"));

            try
            {
                var bodyStream = message.Body as HttpRequestBodyStream;

                Assert.IsNotNull(bodyStream, "请求体应是 HttpRequestBodyStream。");
                Assert.IsTrue(bodyStream.IsFileBacked, "超过阈值时应落到临时文件。");

                var path = bodyStream.TempFilePath;

                Assert.IsTrue(File.Exists(path), "释放前临时文件应存在。");

                message.Dispose();

                Assert.IsFalse(File.Exists(path), "释放后临时文件应被删除。");
                AssertThrows<ObjectDisposedException>(() => ReadAll(bodyStream));
            }
            finally
            {
                message.Dispose();

                DeleteDirectory(tempDirectory);
            }
        }

        /// <summary>
        /// 内存载体复位到起点后应能读出同样的字节，且拒绝写入。
        /// </summary>
        [TestMethod]
        public void HttpRequestBodyStream_SeekAndReadTwice_ReturnsSameBytes()
        {
            var bytes = Encoding.UTF8.GetBytes("stream-body-content");

            using (var stream = HttpRequestBodyStream.FromMemory(bytes, null))
            {
                var first = ReadAll(stream);

                Assert.AreEqual(0, stream.Seek(0, SeekOrigin.Begin), "Seek 到起点应返回 0。");

                var second = ReadAll(stream);

                CollectionAssert.AreEqual(bytes, first, "第一次读取应得到原始字节。");
                CollectionAssert.AreEqual(first, second, "复位后再次读取应得到同样的字节。");
                Assert.IsFalse(stream.CanWrite, "请求体流应为只读。");
                AssertThrows<NotSupportedException>(() => stream.Write(bytes, 0, 1));
            }
        }

        /// <summary>
        /// 文件分段保存时应写入完整字节，并保留流原来的位置。
        /// </summary>
        [TestMethod]
        public void FilePart_Save_WritesStreamBytesAndKeepsPosition()
        {
            var bytes = Encoding.UTF8.GetBytes("file-part-data");

            var directory = CreateTempDirectory();

            var path = Path.Combine(directory, "saved.bin");

            using (var source = new MemoryStream(bytes))
            {
                source.Position = 4;

                using (var part = new FilePart("upload", "a.bin", "application/octet-stream", source))
                {
                    part.Save(path);

                    CollectionAssert.AreEqual(bytes, File.ReadAllBytes(path), "保存的文件应与流内容一致。");
                    Assert.AreEqual(4, source.Position, "保存后流位置应恢复到原处。");
                }
            }

            DeleteDirectory(directory);
        }

        private static async Task<HttpMessage> DecodeAsync(int largeBodyThreshold, string tempDirectory, string channelId, byte[] body)
        {
            var options = PipelineTestKit.CreateOptions(new HttpRequestSplitter());
            var stream = new TestRawByteStream(channelId);
            var capturing = new CapturingInboundHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new HttpRequestDecoder(largeBodyThreshold, tempDirectory, channelId));
                pipeline.AddLast(capturing);

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel(channelId), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    var header = Encoding.ASCII.GetBytes(
                        "POST /upload HTTP/1.1\r\n"
                        + "Host: h\r\n"
                        + "Content-Type: application/octet-stream\r\n"
                        + "Content-Length: " + body.Length + "\r\n"
                        + "\r\n");

                    var frame = new byte[header.Length + body.Length];

                    Buffer.BlockCopy(header, 0, frame, 0, header.Length);
                    Buffer.BlockCopy(body, 0, frame, header.Length, body.Length);

                    await PipelineTestKit.WriteAsync(stream, frame);

                    Assert.IsTrue(await capturing.WaitCountAsync(1, WaitTimeout), "五秒内应产出一条消息。");

                    return (HttpMessage)await capturing.FirstAsync(WaitTimeout);
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor);
                }
            }
        }

        private static byte[] ReadAll(Stream stream)
        {
            stream.Position = 0;

            using (var buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);

                return buffer.ToArray();
            }
        }

        private static string CreateTempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "saea-http-tests", Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(directory);

            return directory;
        }

        private static void AssertThrows<TException>(Action action) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            Assert.Fail("应抛出 " + typeof(TException).Name + "。");
        }

        private static void DeleteDirectory(string directory)
        {
            try
            {
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
            catch
            {
                // 清理失败不影响断言结果，残留目录留给系统临时目录回收
            }
        }
    }
}