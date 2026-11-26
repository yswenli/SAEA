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
*命名空间：SAEA.MVC.Tests
*文件名： BigDataAbortTests
*版本号： net10.0
*唯一标识：2559beed-c7fa-4744-bc70-365b87abfe0c
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/26 17:59:02
*描述：客户端中断后大数据结果停止读取数据流的用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/26 17:59:02
*修改人： yswenli
*版本号： net10.0
*描述：新增，验证客户端强制断开后读循环停止且不把整个流读完
*
*****************************************************************************/
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Tests.Tests.Support;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// 大数据结果在客户端中断后停止读取数据流的用例。
    /// </summary>
    [TestClass]
    public class BigDataAbortTests
    {
        /// <summary>
        /// 客户端读到报头后强制断开，读循环应在连接失效后停止，不把整个流读完。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task BigDataResult_ClientAbort_StopsReadingStream()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                // 先用普通入口确认服务已就绪，避免把连接未就绪误判成读循环未停
                using (var client = TestAppFactory.CreateHttpClient())
                using (var ready = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/bigdataprobe/plain").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, ready.StatusCode, "GET /bigdataprobe/plain 应返回 200，用于确认服务已就绪");
                }

                CountingSlowStream.ResetCounter();

                await AbortAfterHeadersAsync(port, "/bigdataprobe/slow").ConfigureAwait(false);

                // 留出时间让服务端在下次写出失败时关闭连接并退出读循环
                await Task.Delay(1500).ConfigureAwait(false);

                var first = CountingSlowStream.BytesRead;

                await Task.Delay(1000).ConfigureAwait(false);

                var second = CountingSlowStream.BytesRead;

                Assert.IsTrue(first > 0, "客户端断开前服务端应已开始传输，实读 " + first + " 字节");
                Assert.IsTrue(first < 1024 * 1024, "客户端断开后不应把整个 1MB 流读完，实读 " + first + " 字节");
                Assert.IsTrue(second - first <= 3 * 16384, "客户端断开后读循环应停止，后来新增 " + (second - first) + " 字节");
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 用原始 socket 发 GET 请求，读到报头结束后强制关闭连接制造 RST。
        /// </summary>
        /// <param name="port">服务监听端口</param>
        /// <param name="path">请求路径，以 / 开头</param>
        /// <returns>关闭连接后的任务</returns>
        private static async Task AbortAfterHeadersAsync(int port, string path)
        {
            var tcp = new TcpClient();

            try
            {
                await tcp.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);

                var stream = tcp.GetStream();

                var request = Encoding.ASCII.GetBytes("GET " + path + " HTTP/1.1\r\nHost: " + TestAppFactory.LoopbackHost + "\r\nConnection: close\r\n\r\n");

                await stream.WriteAsync(request, 0, request.Length).ConfigureAwait(false);

                // 5 秒上限是硬保护，避免服务端迟迟不回报头时用例挂死
                using (var readTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    var buffer = new byte[4096];

                    var builder = new StringBuilder();

                    while (builder.ToString().IndexOf("\r\n\r\n", StringComparison.Ordinal) < 0)
                    {
                        var read = await stream.ReadAsync(buffer, 0, buffer.Length, readTimeout.Token).ConfigureAwait(false);

                        if (read <= 0)
                            throw new InvalidOperationException("读报头时连接被服务端提前关闭");

                        builder.Append(Encoding.ASCII.GetString(buffer, 0, read));
                    }
                }
            }
            finally
            {
                // 直接置为零延迟关闭，让内核发出 RST 而不是正常 FIN，模拟客户端异常中断
                try
                {
                    tcp.Client.LingerState = new LingerOption(true, 0);
                }
                catch (SocketException)
                {
                }

                tcp.Close();
            }
        }

        /// <summary>
        /// 拼出本用例服务的回环基地址。
        /// </summary>
        /// <param name="port">监听端口</param>
        /// <returns>不含路径的基地址</returns>
        private static string BaseUrl(int port)
        {
            return "http://" + TestAppFactory.LoopbackHost + ":" + port;
        }
    }

    /// <summary>
    /// 慢速数据流：每块 16 KB、每块延时 40 毫秒，共 64 块，供中断用例统计已读字节数。
    /// </summary>
    internal sealed class CountingSlowStream : Stream
    {
        /// <summary>
        /// 单块字节数。
        /// </summary>
        private const int BlockSize = 16 * 1024;

        /// <summary>
        /// 总块数，合计 1 MB。
        /// </summary>
        private const int BlockCount = 64;

        /// <summary>
        /// 每块读取前的延时毫秒数，让读循环耗时足够长以便观察中断。
        /// </summary>
        private const int BlockDelayMilliseconds = 40;

        /// <summary>
        /// 已读出的总字节数，跨线程由 Interlocked 维护。
        /// </summary>
        private static long _bytesRead;

        /// <summary>
        /// 已读出的块序号。
        /// </summary>
        private int _blockIndex;

        /// <summary>
        /// 是否已释放；释放后不再可读。
        /// </summary>
        private volatile bool _disposed;

        /// <summary>
        /// 当前累计已读字节数。
        /// </summary>
        public static long BytesRead
        {
            get { return Interlocked.Read(ref _bytesRead); }
        }

        /// <summary>
        /// 归零累计已读字节数，供每个用例开始时重置。
        /// </summary>
        public static void ResetCounter()
        {
            Interlocked.Exchange(ref _bytesRead, 0);
        }

        /// <summary>
        /// 是否可读：释放前恒为 true。
        /// </summary>
        public override bool CanRead
        {
            get { return !_disposed; }
        }

        /// <summary>
        /// 不可定位。
        /// </summary>
        public override bool CanSeek
        {
            get { return false; }
        }

        /// <summary>
        /// 不可写。
        /// </summary>
        public override bool CanWrite
        {
            get { return false; }
        }

        /// <summary>
        /// 长度未知，访问即抛异常。
        /// </summary>
        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        /// <summary>
        /// 不可定位，读写位置访问即抛异常。
        /// </summary>
        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        /// <summary>
        /// 逐块读取：每块延时后返回最多 16 KB，读完 64 块后返回 0。
        /// </summary>
        /// <param name="buffer">目标缓冲</param>
        /// <param name="offset">写入起点</param>
        /// <param name="count">最多读取字节数</param>
        /// <returns>实际读取字节数</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_disposed || _blockIndex >= BlockCount)
                return 0;

            Thread.Sleep(BlockDelayMilliseconds);

            var length = Math.Min(count, BlockSize);

            Array.Clear(buffer, offset, length);

            _blockIndex++;

            Interlocked.Add(ref _bytesRead, length);

            return length;
        }

        /// <summary>
        /// 无缓冲需要刷新。
        /// </summary>
        public override void Flush()
        {
        }

        /// <summary>
        /// 不可定位。
        /// </summary>
        /// <param name="offset">偏移</param>
        /// <param name="origin">基准</param>
        /// <returns>永不返回</returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 不可设置长度。
        /// </summary>
        /// <param name="value">长度</param>
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 不可写。
        /// </summary>
        /// <param name="buffer">源缓冲</param>
        /// <param name="offset">起点</param>
        /// <param name="count">字节数</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 标记释放，之后读取返回 0。
        /// </summary>
        /// <param name="disposing">是否为显式释放</param>
        protected override void Dispose(bool disposing)
        {
            _disposed = true;

            base.Dispose(disposing);
        }
    }
}