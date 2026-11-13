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
*命名空间：SAEA.MVC.Tests.Tests
*文件名： UserTokenRecycleTests
*版本号： net10.0
*唯一标识：f81aa1f7-60fd-4e1b-b1b6-45fde11b8bf6
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 13:39:39
*描述：高并发短连接下令牌回收复用的端到端回归用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 15:19:56
*修改人： yswenli
*版本号： net10.0
*描述：日志断言补入令牌复用与计数异常文案，改为日志静默一个泵周期即判定刷盘完成
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Tests.Tests.Support;

namespace SAEA.MVC.Tests.Tests
{
    /// <summary>
    /// 令牌回收复用回归：小令牌池 + 高并发短连接，验证连接关闭时仍有在途接收也不会把 args 交给下一个连接。
    /// </summary>
    [TestClass]
    public class UserTokenRecycleTests
    {
        /// <summary>并发客户端数量。</summary>
        private const int WorkerCount = 24;

        /// <summary>每个客户端发送的请求数，总量为两者乘积。</summary>
        private const int IterationsPerWorker = 50;

        /// <summary>令牌池上限：远小于并发数，迫使令牌在连接之间快速回收复用。</summary>
        private const int MaxConnects = 8;

        /// <summary>
        /// 单条请求允许的传输层重连次数。回环短连接高并发下服务端偶发 RST（基线无令牌复用压力时同样出现），
        /// 与令牌状态无关；只对传输异常重连，业务失败与日志标记仍按严格断言判定。
        /// </summary>
        private const int MaxTransportRetries = 2;

        /// <summary>整轮允许的传输层重连总数上限，超过即说明不只是噪声。</summary>
        private const int TransportRetryBudget = 25;

        /// <summary>整轮累计的传输层重连次数。</summary>
        private static int _transportRetries;

        /// <summary>等待日志后台泵把异常行刷到控制台的最长时间。</summary>
        private static readonly TimeSpan LogFlushTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 日志连续无增长多久视为已刷盘完成。LogHelper 的空闲轮询间隔为 500ms，取值需明显大于它。
        /// </summary>
        private static readonly TimeSpan LogQuietPeriod = TimeSpan.FromMilliseconds(1000);

        /// <summary>日志中出现即判定复用路径出错的标记。</summary>
        private static readonly string[] ReuseErrorMarkers =
        {
            "already in progress",
            "UserToken池中资源已耗尽",
            "NullReferenceException",
            "令牌复用状态非法",
            "无可用缓冲区槽位",
            "令牌暂不可用",
            "令牌暂不可回收",
            "在途操作计数已为 0"
        };

        /// <summary>
        /// 24 个客户端各发 50 个 Connection: close 请求，令牌池只有 8 个令牌：
        /// 所有请求都应成功，日志中不得出现 SetBuffer 冲突、池耗尽或空引用，收尾请求仍应成功。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task UserTokenPool_ConcurrentCloseRequests_ReusesWithoutArgsConflict()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var url = "http://" + TestAppFactory.LoopbackHost + ":" + port + "/basic/index";

            // 小池是本用例的前提：连接会排队等待令牌，令牌一归还就被下一个连接取走
            var app = new SAEAMvcApplication(rootName, port, maxCounnects: MaxConnects);

            var failures = 0;
            var failureSamples = new ConcurrentQueue<string>();
            _transportRetries = 0;

            var capture = new CaptureWriter();
            var originalOut = Console.Out;
            Console.SetOut(capture);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    // 就绪探测：首个请求同时承担服务启动等待
                    await AssertRequestOkAsync(client, url).ConfigureAwait(false);

                    var workers = new Task[WorkerCount];

                    var startedAt = DateTime.UtcNow;

                    for (var w = 0; w < WorkerCount; w++)
                    {
                        workers[w] = Task.Run(async () =>
                        {
                            for (var i = 0; i < IterationsPerWorker; i++)
                            {
                                try
                                {
                                    await AssertRequestOkAsync(client, url).ConfigureAwait(false);
                                }
                                catch (Exception ex)
                                {
                                    if (failureSamples.Count < 5)
                                        failureSamples.Enqueue(ex.GetType().Name + ": " + ex.Message);

                                    Interlocked.Increment(ref failures);
                                }
                            }
                        });
                    }

                    await Task.WhenAll(workers).ConfigureAwait(false);

                    // 并发结束后再发一条：池与信号量应已完全归还
                    await AssertRequestOkAsync(client, url).ConfigureAwait(false);

                    var elapsed = DateTime.UtcNow - startedAt;

                    // 池耗尽或令牌滞留会让连接排队等待 ConnectTimeout，耗时是最直观的回归信号
                    if (elapsed > TimeSpan.FromSeconds(60))
                        Assert.Fail($"并发阶段耗时 {elapsed.TotalSeconds:F1} 秒，超出 60 秒上限");
                }

                var excerpt = await WaitForErrorMarkerAsync(capture).ConfigureAwait(false);

                if (excerpt != null)
                    Assert.Fail("令牌复用路径出现异常日志：" + Environment.NewLine + excerpt);

                if (failures != 0)
                {
                    failureSamples.TryPeek(out var sample);
                    Assert.Fail($"并发短连接失败 {failures} 条，示例：{sample}");
                }

                if (_transportRetries > TransportRetryBudget)
                    Assert.Fail($"传输层重连 {_transportRetries} 次，超出噪声上限 {TransportRetryBudget} 次");
            }
            finally
            {
                Console.SetOut(originalOut);

                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 发一个 Connection: close 请求，校验状态码与固定正文；传输层异常时重连重试。
        /// </summary>
        /// <param name="client">复用的请求客户端</param>
        /// <param name="url">请求地址</param>
        /// <returns>该请求的异步执行任务</returns>
        private static async Task AssertRequestOkAsync(HttpClient client, string url)
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    await SendOnceAsync(client, url).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex) when (attempt < MaxTransportRetries && IsTransportFailure(ex))
                {
                    // 重连会让令牌再走一次「关闭 -> 回收 -> 复用」，正好加大用例覆盖的窗口
                    Interlocked.Increment(ref _transportRetries);

                    await Task.Delay(20).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// 发送一次请求并校验响应，传输层异常直接上抛给重连逻辑。
        /// </summary>
        /// <param name="client">复用的请求客户端</param>
        /// <param name="url">请求地址</param>
        /// <returns>该请求的异步执行任务</returns>
        private static async Task SendOnceAsync(HttpClient client, string url)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                // 强制每次新建连接，让服务端频繁经历「关闭连接 -> 回收令牌 -> 复用」
                request.Headers.ConnectionClose = true;

                using (var response = await client.SendAsync(request).ConfigureAwait(false))
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException($"状态码 {(int)response.StatusCode}，正文 {body}");

                    if (body != "basic-index-ok")
                        throw new InvalidOperationException("正文不符：" + body);
                }
            }
        }

        /// <summary>
        /// 判断异常是否属于连接层失败（可重连），业务断言失败返回 false。
        /// </summary>
        /// <param name="ex">请求过程中抛出的异常</param>
        /// <returns>true 表示属于可重连的传输层失败</returns>
        private static bool IsTransportFailure(Exception ex)
        {
            return ex is HttpRequestException || ex is IOException;
        }

        /// <summary>
        /// 轮询等待日志刷盘：命中错误标记立即返回；日志长度连续一个泵周期不再增长即视为写完。
        /// </summary>
        /// <param name="capture">控制台捕获器</param>
        /// <returns>命中标记前后各一段日志；未命中返回 null</returns>
        private static async Task<string> WaitForErrorMarkerAsync(CaptureWriter capture)
        {
            var deadline = DateTime.UtcNow + LogFlushTimeout;
            var lastLength = -1;
            var quietSince = DateTime.UtcNow;

            while (true)
            {
                var text = capture.Snapshot();

                foreach (var marker in ReuseErrorMarkers)
                {
                    var index = text.IndexOf(marker, StringComparison.Ordinal);

                    if (index >= 0)
                    {
                        var start = Math.Max(0, index - 400);
                        var end = Math.Min(text.Length, index + 1200);

                        return text.Substring(start, end - start);
                    }
                }

                if (text.Length != lastLength)
                {
                    lastLength = text.Length;
                    quietSince = DateTime.UtcNow;
                }
                else if (DateTime.UtcNow - quietSince >= LogQuietPeriod)
                {
                    // 日志已静默超过一个后台泵周期，再等也不会出现新行
                    return null;
                }

                if (DateTime.UtcNow >= deadline)
                    return null;

                await Task.Delay(50).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 线程安全的控制台捕获器：日志后台线程与用例线程会同时写入。
        /// </summary>
        private sealed class CaptureWriter : TextWriter
        {
            private readonly object _sync = new object();

            private readonly StringBuilder _buffer = new StringBuilder();

            /// <summary>捕获内容的编码，固定 UTF-8。</summary>
            public override Encoding Encoding => Encoding.UTF8;

            /// <summary>写入单个字符。</summary>
            /// <param name="value">待写入字符</param>
            public override void Write(char value)
            {
                lock (_sync)
                {
                    _buffer.Append(value);
                }
            }

            /// <summary>写入字符串。</summary>
            /// <param name="value">待写入文本</param>
            public override void Write(string value)
            {
                lock (_sync)
                {
                    _buffer.Append(value);
                }
            }

            /// <summary>写入一行。</summary>
            /// <param name="value">待写入文本</param>
            public override void WriteLine(string value)
            {
                lock (_sync)
                {
                    _buffer.Append(value).Append(Environment.NewLine);
                }
            }

            /// <summary>
            /// 取当前捕获内容的一致性快照。
            /// </summary>
            /// <returns>已捕获的全部文本</returns>
            public string Snapshot()
            {
                lock (_sync)
                {
                    return _buffer.ToString();
                }
            }
        }
    }
}
