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
*命名空间：SAEA.Sockets.PipelineTest
*文件名： Program
*版本号： v26.11.5.1
*唯一标识：65b632e5-26f8-4122-b383-e5dfc81bffdc
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：通道链演示与端到端自测入口
*
*=====================================================================
*修改标记
*修改时间：2026/11/9 14:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：装配出站串行器并补启动校验、冻链与回调外写出站三个演示用例
*
*****************************************************************************/
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using SAEA.Sockets.Base;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.PipelineTest
{
    /// <summary>
    /// 通道链演示与端到端自测入口。
    /// <para>
    /// <c>--all</c> 在内存回环上跑全链路用例；<c>--socket</c> 用真实
    /// <see cref="SAEA.Sockets.Core.Tcp.SaeaServerSocket"/> 验证接入点。
    /// 不带参数（或 <c>--help</c>）只打印用法并返回 0，自动化场景必须显式传参。
    /// </para>
    /// </summary>
    internal static class Program
    {
        private const int MaxFrameLength = 64 * 1024;

        private const int WaitTimeoutMilliseconds = 5000;

        private const int SocketCaseTimeoutMilliseconds = 15000;

        private static int Main(string[] args)
        {
            if (args != null && Array.IndexOf(args, "--all") >= 0)
                return RunAllAsync().GetAwaiter().GetResult();

            if (args != null && Array.IndexOf(args, "--socket") >= 0)
                return RunSocketCaseAsync(useTls: false).GetAwaiter().GetResult();

            if (args != null && Array.IndexOf(args, "--tls") >= 0)
                return RunSocketCaseAsync(useTls: true).GetAwaiter().GetResult();

            if (args != null && Array.IndexOf(args, "--client") >= 0)
                return RunClientCaseAsync(useTls: false).GetAwaiter().GetResult();

            if (args != null && Array.IndexOf(args, "--client-tls") >= 0)
                return RunClientCaseAsync(useTls: true).GetAwaiter().GetResult();

            if (args != null && Array.IndexOf(args, "--bench") >= 0)
                return RunBenchAsync().GetAwaiter().GetResult();

            PrintUsage();
            return 0;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("SAEA.Sockets.PipelineTest 用法：");
            Console.WriteLine("  --all     运行全部内存回环用例（粘包/切分、慢鉴权、出站字节、背压、超时、断开、逐字节）");
            Console.WriteLine("  --socket  使用真实 SaeaServerSocket 跑一次端到端登录回包（默认随机空闲端口）");
            Console.WriteLine("  --tls     在 --socket 的基础上启用 TLS，验证首帧在握手成功之后到达");
            Console.WriteLine("  --client  客户端也接入通道链：连接后由输入处理器接管读循环，事件泵不启动");
            Console.WriteLine("  --client-tls 在 --client 的基础上启用 TLS，验证握手完成前不启动客户端通道链");
            Console.WriteLine("  --bench   与 BaseCoder.DecodeStream 直连写法对比吞吐与每帧分配（趋势值，Release 下跑）");
            Console.WriteLine("  --help    显示本帮助");
            Console.WriteLine();
            Console.WriteLine("环境变量：SAEA_PIPELINE_TEST_PORT 指定 --socket/--tls/--client/--client-tls 的监听端口（1-65535）。");
        }

        // ---------- 用例调度 ----------

        private static async Task<int> RunAllAsync()
        {
            Console.WriteLine("SAEA.Sockets.PipelineTest --all");
            Console.WriteLine("线格式：8B 小端 body 长度 + 1B 类型码 + body（UTF-8 JSON）");
            Console.WriteLine();

            var failures = 0;
            if (!await RunCaseAsync("a 内存回环：3 帧粘包与任意切分", LoopbackCaseAsync)) failures++;
            if (!await RunCaseAsync("b 慢鉴权：5 条梯度延迟并发完成，完成顺序≠到达顺序", SlowLoginCaseAsync)) failures++;
            if (!await RunCaseAsync("c 出站字节：全链路输出与手工帧编码逐字节一致", OutboundBytesCaseAsync)) failures++;
            if (!await RunCaseAsync("d 背压：QueueCapacity=2、单并发、10 帧只暂停一次", BackpressureCaseAsync)) failures++;
            if (!await RunCaseAsync("e 单消息超时：100ms 超时 token 被取消且连接保持活跃", MessageTimeoutCaseAsync)) failures++;
            if (!await RunCaseAsync("f 断开后回包：CloseAsync 后 WriteAsync 抛 ChannelClosedException", CloseThenWriteCaseAsync)) failures++;
            if (!await RunCaseAsync("g 逐字节喂入：每帧拆成 1 字节仍正确还原", OneByteAtATimeCaseAsync)) failures++;
            if (!await RunCaseAsync("h 启动校验：RequireTerminalHandler 装配齐全后正常启动", RequireTerminalCaseAsync)) failures++;
            if (!await RunCaseAsync("i 启动后冻链：AddLast 抛 InvalidOperationException", FrozenPipelineCaseAsync)) failures++;
            if (!await RunCaseAsync("j 出站回调外 ctx.WriteAsync：抛 InvalidOperationException", ContextWriteOutsideCallbackCaseAsync)) failures++;

            Console.WriteLine();
            Console.WriteLine(failures == 0 ? "全部用例通过" : "失败用例数：" + failures);
            return failures == 0 ? 0 : 1;
        }

        private static async Task<bool> RunCaseAsync(string name, Func<Task> body)
        {
            try
            {
                await body().ConfigureAwait(false);
                Console.WriteLine("PASS  " + name);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL  " + name + " : " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        // ---------- a：内存回环 + 粘包/切分 ----------

        private static async Task LoopbackCaseAsync()
        {
            var registry = CreateRegistry();
            var wire = Concat(
                EncodeFrame(new Heartbeat { Seq = 1, Payload = "a" }, registry),
                EncodeFrame(new Heartbeat { Seq = 2, Payload = "b" }, registry),
                EncodeFrame(new Heartbeat { Seq = 3, Payload = "c" }, registry));

            await RunLoopbackVariantAsync(registry, wire, null);
            await RunLoopbackVariantAsync(registry, wire, new[] { 1, 2, 3, 7, 5, 4, 9, 6, 8, 11 });
        }

        private static async Task RunLoopbackVariantAsync(TypeRegistry registry, byte[] wire, int[] segments)
        {
            var heartbeat = new HeartbeatHandler();
            using (var session = new InMemorySession(
                "loopback-" + (segments == null ? "glued" : "split"),
                NewOptions(queueCapacity: 64, maxConcurrentMessages: 1),
                registry,
                new LoginHandler(0),
                heartbeat))
            {
                session.Start();

                if (segments == null)
                {
                    await session.Stream.WriteAsync(wire).ConfigureAwait(false);
                }
                else
                {
                    await WriteSegmentsAsync(session.Stream, wire, segments).ConfigureAwait(false);
                }

                await WaitUntilAsync(
                    () => session.Stream.SentCount == 3,
                    "回环回包数量不足 3").ConfigureAwait(false);

                var seqs = DecodeFrames(session.Stream.SentFrames, registry)
                    .OfType<Heartbeat>()
                    .Select(item => item.Seq)
                    .ToArray();

                AssertSequenceEqual(new[] { 1, 2, 3 }, seqs, "回包顺序或内容错误");

                await session.StopAsync().ConfigureAwait(false);
            }
        }

        // ---------- b：慢鉴权并发 ----------

        private static async Task SlowLoginCaseAsync()
        {
            var registry = CreateRegistry();
            var auth = new DelayedAuthHandler(200, 160, 120, 80, 40);
            var login = new LoginHandler(0);
            var heartbeat = new HeartbeatHandler();

            using (var session = new InMemorySession(
                "slow-login",
                NewOptions(queueCapacity: 64, maxConcurrentMessages: 5),
                registry,
                login,
                heartbeat,
                auth))
            {
                session.Start();

                // 序号 1..5 的鉴权耗时依次为 200/160/120/80/40ms：
                // 若同连接消息被串行化，完成顺序等于到达顺序、且总耗时接近 600ms。
                var wire = Concat(
                    EncodeFrame(new Heartbeat { Seq = 1, Payload = "x" }, registry),
                    EncodeFrame(new Heartbeat { Seq = 2, Payload = "x" }, registry),
                    EncodeFrame(new Heartbeat { Seq = 3, Payload = "x" }, registry),
                    EncodeFrame(new Heartbeat { Seq = 4, Payload = "x" }, registry),
                    EncodeFrame(new Heartbeat { Seq = 5, Payload = "x" }, registry));

                var watch = Stopwatch.StartNew();
                await session.Stream.WriteAsync(wire).ConfigureAwait(false);
                await WaitUntilAsync(
                    () => session.Stream.SentCount == 5,
                    "慢鉴权回包数量不足 5").ConfigureAwait(false);
                watch.Stop();

                var replies = DecodeFrames(session.Stream.SentFrames, registry);
                var heartbeatSeqs = replies.OfType<Heartbeat>().Select(item => item.Seq).OrderBy(seq => seq).ToArray();
                AssertSequenceEqual(new[] { 1, 2, 3, 4, 5 }, heartbeatSeqs, "心跳回包序号与请求不匹配");

                AssertSequenceEqual(
                    new[] { 5, 4, 3, 2, 1 },
                    auth.CompletionOrder,
                    "完成顺序应为 40→80→120→160→200ms（序号 5→1）");

                if (auth.PeakConcurrency <= 1)
                    throw new InvalidOperationException("同连接消息未并发执行，峰值并发=" + auth.PeakConcurrency);
                if (watch.ElapsedMilliseconds >= 1000)
                    throw new InvalidOperationException("慢鉴权用例耗时异常：" + watch.ElapsedMilliseconds + "ms");

                Console.WriteLine(
                    "      5 条梯度延迟全部回包，峰值并发 " + auth.PeakConcurrency +
                    "，耗时 " + watch.ElapsedMilliseconds + "ms");

                await session.StopAsync().ConfigureAwait(false);
            }
        }

        // ---------- c：出站全链路字节 ----------

        private static async Task OutboundBytesCaseAsync()
        {
            var registry = CreateRegistry();
            var login = new LoginHandler(0);

            using (var session = new InMemorySession(
                "outbound-bytes",
                NewOptions(queueCapacity: 16, maxConcurrentMessages: 1),
                registry,
                login,
                new HeartbeatHandler()))
            {
                session.Start();

                await session.Stream.WriteAsync(
                    EncodeFrame(new LoginRequest { RequestId = 7, UserName = "wire" }, registry))
                    .ConfigureAwait(false);

                await WaitUntilAsync(
                    () => session.Stream.SentCount == 1,
                    "未收到登录回包").ConfigureAwait(false);

                var response = login.LastResponse;
                if (response == null)
                    throw new InvalidOperationException("登录处理器未记录回包实体。");

                var actual = session.Stream.SentFrames[0];
                var expected = EncodeFrame(response, registry);

                if (!actual.SequenceEqual(expected))
                {
                    throw new InvalidOperationException(
                        "出站帧与手工 LengthFieldFrameEncoder 编码不一致：实际 " + actual.Length +
                        "B，期望 " + expected.Length + "B。");
                }

                await session.StopAsync().ConfigureAwait(false);
            }
        }

        // ---------- d：背压与低水位 ----------

        private static async Task BackpressureCaseAsync()
        {
            var registry = CreateRegistry();
            var handler = new GatedHeartbeatHandler();

            using (var session = new InMemorySession(
                "backpressure",
                NewOptions(queueCapacity: 2, maxConcurrentMessages: 1),
                registry,
                new LoginHandler(0),
                handler))
            {
                session.Start();

                await WriteHeartbeatAsync(session.Stream, registry, 1).ConfigureAwait(false);
                await WriteHeartbeatAsync(session.Stream, registry, 2).ConfigureAwait(false);
                await handler.FirstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

                // 消费侧此时应已取走第 2 帧并卡在并发额度上，队列空出后更容易稳定命中一次暂停。
                await Task.Delay(100).ConfigureAwait(false);

                await WriteHeartbeatAsync(session.Stream, registry, 3).ConfigureAwait(false);
                await WriteHeartbeatAsync(session.Stream, registry, 4).ConfigureAwait(false);
                await WriteHeartbeatAsync(session.Stream, registry, 5).ConfigureAwait(false);

                await WaitUntilAsync(() => session.Stream.PauseCount == 1, "队列满未触发 Pause").ConfigureAwait(false);

                handler.Release();

                await WaitUntilAsync(() => session.Stream.ResumeCount >= 1, "未触发 Resume").ConfigureAwait(false);
                await WaitUntilAsync(() => handler.Count == 5, "恢复后前 5 帧未处理完").ConfigureAwait(false);

                for (var seq = 6; seq <= 10; seq++)
                {
                    await WriteHeartbeatAsync(session.Stream, registry, seq).ConfigureAwait(false);
                    var expected = seq;
                    await WaitUntilAsync(() => handler.Count == expected, "第 " + expected + " 帧未处理").ConfigureAwait(false);
                }

                if (session.Stream.PauseCount != 1)
                    throw new InvalidOperationException("暂停次数不是 1：" + session.Stream.PauseCount);
                if (session.Stream.ResumeCount < 1)
                    throw new InvalidOperationException("恢复次数小于 1：" + session.Stream.ResumeCount);

                AssertSequenceEqual(
                    Enumerable.Range(1, 10).ToArray(),
                    handler.Sequence,
                    "背压恢复后的处理顺序错误");

                Console.WriteLine(
                    "      Pause=" + session.Stream.PauseCount + "，Resume=" + session.Stream.ResumeCount +
                    "，处理 " + handler.Count + " 帧");

                await session.StopAsync().ConfigureAwait(false);
            }
        }

        // ---------- e：单消息超时 ----------

        private static async Task MessageTimeoutCaseAsync()
        {
            var registry = CreateRegistry();
            var handler = new TimeoutHeartbeatHandler();

            using (var session = new InMemorySession(
                "message-timeout",
                NewOptions(
                    queueCapacity: 8,
                    maxConcurrentMessages: 2,
                    messageTimeout: TimeSpan.FromMilliseconds(100)),
                registry,
                new LoginHandler(0),
                handler))
            {
                session.Start();

                await session.Stream.WriteAsync(Concat(
                    EncodeFrame(new Heartbeat { Seq = 1, Payload = "wait" }, registry),
                    EncodeFrame(new Heartbeat { Seq = 2, Payload = "quick" }, registry)))
                    .ConfigureAwait(false);

                await session.Errors.WaitForTimeoutAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                await handler.WaiterCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                await handler.QuickDone.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

                if (!session.Executor.IsActive)
                    throw new InvalidOperationException("单条消息超时不应关闭连接。");

                var unexpected = session.Errors.Exceptions
                    .Where(ex => !(ex is MessageTimeoutException))
                    .ToArray();
                if (unexpected.Length > 0)
                    throw new InvalidOperationException("出现非超时异常：" + unexpected[0].GetType().Name);

                await WaitUntilAsync(() => session.Stream.SentCount == 1, "未收到 20ms 消息的回包").ConfigureAwait(false);

                var replies = DecodeFrames(session.Stream.SentFrames, registry).OfType<Heartbeat>().ToArray();
                if (replies.Length != 1 || replies[0].Seq != 2)
                    throw new InvalidOperationException("20ms 消息回包序号错误。");

                Console.WriteLine(
                    "      超时上报 " + session.Errors.TimeoutCount + " 次，连接仍活跃，20ms 消息正常回包");

                await session.StopAsync().ConfigureAwait(false);
            }
        }

        // ---------- f：断开后回包 ----------

        private static async Task CloseThenWriteCaseAsync()
        {
            var registry = CreateRegistry();
            var handler = new CloseInsideHandler();

            using (var session = new InMemorySession(
                "close-then-write",
                NewOptions(queueCapacity: 4, maxConcurrentMessages: 1),
                registry,
                new LoginHandler(0),
                handler))
            {
                session.Start();

                await WriteHeartbeatAsync(session.Stream, registry, 1).ConfigureAwait(false);

                var closed = await handler.ClosedWriteError.Task
                    .WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

                if (!(closed is ChannelClosedException))
                    throw new InvalidOperationException(
                        "关闭后写入未抛出 ChannelClosedException，实际：" +
                        (closed == null ? "无异常" : closed.GetType().Name) + "。");

                if (session.Channel.IsActive)
                    throw new InvalidOperationException("CloseAsync 之后通道仍为活跃状态。");

                await session.Executor.Completion.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

                Console.WriteLine("      关闭原因：" + session.Stream.CloseReason + "，回包被拒绝：" + closed.Message);
            }
        }

        // ---------- g：逐字节喂入 ----------

        private static async Task OneByteAtATimeCaseAsync()
        {
            var registry = CreateRegistry();
            var wire = Concat(
                EncodeFrame(new Heartbeat { Seq = 21, Payload = "a" }, registry),
                EncodeFrame(new Heartbeat { Seq = 22, Payload = "b" }, registry),
                EncodeFrame(new Heartbeat { Seq = 23, Payload = "c" }, registry));

            using (var session = new InMemorySession(
                "one-byte",
                NewOptions(queueCapacity: 64, maxConcurrentMessages: 1),
                registry,
                new LoginHandler(0),
                new HeartbeatHandler()))
            {
                session.Start();

                for (var i = 0; i < wire.Length; i++)
                {
                    await session.Stream.WriteAsync(new[] { wire[i] }).ConfigureAwait(false);
                }

                await WaitUntilAsync(
                    () => session.Stream.SentCount == 3,
                    "逐字节喂入后回包不足 3").ConfigureAwait(false);

                var seqs = DecodeFrames(session.Stream.SentFrames, registry)
                    .OfType<Heartbeat>()
                    .Select(item => item.Seq)
                    .ToArray();

                AssertSequenceEqual(new[] { 21, 22, 23 }, seqs, "逐字节喂入的回包顺序或内容错误");

                await session.StopAsync().ConfigureAwait(false);
            }
        }

        // ---------- h：启动校验通过 ----------

        private static async Task RequireTerminalCaseAsync()
        {
            var registry = CreateRegistry();

            using (var session = new InMemorySession(
                "require-terminal",
                NewOptions(queueCapacity: 8, maxConcurrentMessages: 1, requireTerminalHandler: true),
                registry,
                new LoginHandler(0),
                new HeartbeatHandler()))
            {
                // BuildPipeline 已装配 OutboundSerializingHandler 与链尾 ExceptionTerminalHandler。
                session.Start();

                await WriteHeartbeatAsync(session.Stream, registry, 31).ConfigureAwait(false);
                await WaitUntilAsync(() => session.Stream.SentCount == 1, "启动校验通过后未收到回包").ConfigureAwait(false);

                var seqs = DecodeFrames(session.Stream.SentFrames, registry)
                    .OfType<Heartbeat>()
                    .Select(item => item.Seq)
                    .ToArray();

                AssertSequenceEqual(new[] { 31 }, seqs, "启动校验通过后的回包内容错误");

                await session.StopAsync().ConfigureAwait(false);
            }
        }

        // ---------- i：启动后链冻结 ----------

        private static async Task FrozenPipelineCaseAsync()
        {
            var registry = CreateRegistry();

            using (var session = new InMemorySession(
                "frozen-pipeline",
                NewOptions(queueCapacity: 8, maxConcurrentMessages: 1),
                registry,
                new LoginHandler(0),
                new HeartbeatHandler()))
            {
                session.Start();

                try
                {
                    session.Pipeline.AddLast(new ContextProbeHandler());
                }
                catch (InvalidOperationException)
                {
                    await session.StopAsync().ConfigureAwait(false);
                    return;
                }

                throw new InvalidOperationException("Start() 之后 AddLast 未抛 InvalidOperationException。");
            }
        }

        // ---------- j：出站回调外 ctx.WriteAsync ----------

        private static async Task ContextWriteOutsideCallbackCaseAsync()
        {
            var registry = CreateRegistry();
            var probe = new ContextProbeHandler();

            using (var session = new InMemorySession(
                "ctx-write-outside-callback",
                NewOptions(queueCapacity: 8, maxConcurrentMessages: 1),
                registry,
                new LoginHandler(0),
                new HeartbeatHandler(),
                probe))
            {
                session.Start();

                var ctx = await probe.Captured.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

                Exception error = null;
                try
                {
                    // 回调外没有绑定本次写操作，入口应同步拒绝而不是排队到下游。
                    await ctx.WriteAsync(new Heartbeat { Seq = 90 }).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                if (!(error is InvalidOperationException))
                    throw new InvalidOperationException(
                        "出站回调外 ctx.WriteAsync 未抛 InvalidOperationException，实际：" +
                        (error == null ? "无异常" : error.GetType().Name) + "。");

                if (!session.Executor.IsActive)
                    throw new InvalidOperationException("回调外写被拒绝后连接不应进入收口。");

                await session.StopAsync().ConfigureAwait(false);
            }
        }

        // ---------- --socket：真实 SaeaServerSocket ----------

        private static async Task<int> RunSocketCaseAsync(bool useTls)
        {
            var label = useTls ? "--tls" : "--socket";
            Console.WriteLine("SAEA.Sockets.PipelineTest " + label);

            var run = RunSocketScenarioAsync(useTls);
            var winner = await Task.WhenAny(run, Task.Delay(SocketCaseTimeoutMilliseconds)).ConfigureAwait(false);

            if (winner != run)
            {
                Console.WriteLine("FAIL  " + label + " : " + (SocketCaseTimeoutMilliseconds / 1000) + " 秒内未完成");
                return 1;
            }

            try
            {
                await run.ConfigureAwait(false);
                Console.WriteLine(
                    "PASS  " + label +
                    (useTls
                        ? " 真实 SaeaServerSocket + TLS 登录回包（首帧在握手之后）"
                        : " 真实 SaeaServerSocket 登录回包"));
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL  " + label + " : " + ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
        }

        // ---------- --client：真实 SaeaClientSocket 接入通道链 ----------

        private static async Task<int> RunClientCaseAsync(bool useTls)
        {
            var label = useTls ? "--client-tls" : "--client";
            Console.WriteLine("SAEA.Sockets.PipelineTest " + label);

            var run = RunClientScenarioAsync(useTls);
            var winner = await Task.WhenAny(run, Task.Delay(SocketCaseTimeoutMilliseconds)).ConfigureAwait(false);

            if (winner != run)
            {
                Console.WriteLine("FAIL  " + label + " : " + (SocketCaseTimeoutMilliseconds / 1000) + " 秒内未完成");
                return 1;
            }

            try
            {
                await run.ConfigureAwait(false);
                Console.WriteLine(
                    "PASS  " + label +
                    (useTls
                        ? " 真实 SaeaClientSocket + TLS 由输入处理器接管读循环"
                        : " 真实 SaeaClientSocket 由输入处理器接管读循环"));
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL  " + label + " : " + ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
        }

        /// <summary>
        /// 客户端接入点自测：服务端只做最小回包，客户端通过钩子建立通道链并解出 LoginResponse。
        /// 同时验证配置输入处理器后客户端事件泵不会启动。
        /// </summary>
        private static async Task RunClientScenarioAsync(bool useTls)
        {
            var port = ResolvePort();
            var registry = CreateRegistry();
            var serverErrors = new ConcurrentQueue<string>();
            var clientErrors = new ConcurrentQueue<string>();
            var certificate = useTls ? CreateServerCertificate() : null;

            SaeaServerSocket server = null;
            SaeaClientSocket client = null;
            var serverHeartbeat = new TaskCompletionSource<int>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                var serverBuilder = new SocketOptionBuilder()
                    .UseStream()
                    .SetIP("127.0.0.1")
                    .SetPort(port);

                if (useTls)
                    serverBuilder = serverBuilder.WithSsl(o => o.ServerCertificate = certificate);

                server = new SaeaServerSocket(serverBuilder.Build());
                server.OnError += (id, ex) =>
                    serverErrors.Enqueue(id + ": " + ex.GetType().Name + ": " + ex.Message);
                server.OnAccepted += obj =>
                {
                    var info = obj as ChannelInfo;
                    if (info == null)
                        return;

                    // 服务端验证的是客户端接入点：读一帧登录请求并回包，再读客户端经通道链发出的心跳。
                    Task.Run(async () =>
                    {
                        try
                        {
                            var request = DecodeFrame(ReadFrame(info.Stream), registry) as LoginRequest;
                            if (request == null)
                                throw new InvalidOperationException("服务端未解出 LoginRequest。");

                            var wire = EncodeFrame(LoginHandler.CreateResponse(request), registry);
                            await info.Stream.WriteAsync(wire, 0, wire.Length).ConfigureAwait(false);
                            await info.Stream.FlushAsync().ConfigureAwait(false);

                            // 第二帧由客户端链的出站方向产生，读到它说明适配器的发送委托接通了。
                            var heartbeat = DecodeFrame(ReadFrame(info.Stream), registry) as Heartbeat;
                            if (heartbeat == null)
                                throw new InvalidOperationException("服务端未解出客户端链回写的心跳。");

                            serverHeartbeat.TrySetResult(heartbeat.Seq);
                        }
                        catch (Exception ex)
                        {
                            serverErrors.Enqueue("server-read: " + ex.GetType().Name + ": " + ex.Message);
                        }
                    });
                };
                server.Start();

                var host = new PipelineHost();
                var executorReady = new TaskCompletionSource<ChannelPipelineExecutor>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var responseHandler = new LoginResponseHandler(heartbeatSeq: 7);
                var spanEventCount = 0;
                ChannelPipelineExecutor clientExecutor = null;

                var clientBuilder = new SocketOptionBuilder()
                    .UseStream()
                    .SetIP("127.0.0.1")
                    .SetPort(port)
                    .SetReadBufferSize(64 * 1024)
                    .UseConnectionOpened(info =>
                    {
                        var channel = new DefaultChannel(info.ID, reason =>
                        {
                            var current = clientExecutor;
                            if (current != null)
                                return current.BeginShutdown(reason);

                            try { client?.Disconnect(); } catch { }
                            return Task.CompletedTask;
                        });

                        channel.SetAttribute(TokenAuthHandler.TokenAttributeKey, "client-seed-token");
                        host.Set(channel);
                    })
                    .UseInputHandler(async (userToken, input, flow, token) =>
                    {
                        IChannel channel;
                        if (!host.TryGetChannel(userToken.ID, out channel))
                            throw new InvalidOperationException("客户端连接建立钩子未登记通道：" + userToken.ID);

                        var adapter = new SocketStreamAdapter(
                            userToken.ID,
                            input,
                            flow,
                            (data, owner) =>
                            {
                                // 客户端发送重载会按需复制，并在底层交接完成或失败后释放 owner，适配器不接管所有权。
                                return client.SendAsync(data, owner);
                            });

                        var options = NewOptions(queueCapacity: 64, maxConcurrentMessages: 4);
                        var pipeline = BuildClientPipeline(options, registry, responseHandler);
                        var executor = new ChannelPipelineExecutor(adapter, channel, options, token, host);
                        executor.Attach(pipeline);
                        clientExecutor = executor;
                        executor.Start();
                        executorReady.TrySetResult(executor);

                        try
                        {
                            await executor.Completion.ConfigureAwait(false);
                        }
                        finally
                        {
                            if (ReferenceEquals(clientExecutor, executor))
                                clientExecutor = null;
                        }
                    });

                if (useTls)
                    clientBuilder = clientBuilder.WithSsl(o =>
                    {
                        o.TargetHost = "localhost";
                        o.AllowInvalidCertificates = true;
                    });

                client = new SaeaClientSocket(clientBuilder.Build());
                client.OnError += (id, ex) =>
                    clientErrors.Enqueue(id + ": " + ex.GetType().Name + ": " + ex.Message);
                client.OnClientReceiveSpan += data => Interlocked.Increment(ref spanEventCount);
                client.Connect();

                if (useTls && !client.IsAuthenticated)
                    throw new InvalidOperationException("客户端 TLS 握手未完成。");

                var executor = await executorReady.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                var sessionId = executor.Channel.Id;

                var request = EncodeFrame(new LoginRequest { RequestId = 99, UserName = "client" }, registry);
                client.SendAsync(new ReadOnlyMemory<byte>(request));

                var response = await responseHandler.Received.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                if (response.RequestId != 99 || !response.Success || response.Token != "token-client")
                    throw new InvalidOperationException("客户端通道链解出的 LoginResponse 字段不正确。");

                if (Volatile.Read(ref spanEventCount) != 0)
                    throw new InvalidOperationException("配置输入处理器后 OnClientReceiveSpan 仍然触发。");

                var heartbeatSeq = await serverHeartbeat.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                if (heartbeatSeq != 7)
                    throw new InvalidOperationException("客户端链出站回写的心跳序号不正确：" + heartbeatSeq);

                if (clientErrors.Count > 0)
                    throw new InvalidOperationException("客户端报告错误：" + string.Join(" | ", clientErrors));
                if (serverErrors.Count > 0)
                    throw new InvalidOperationException("服务端报告错误：" + string.Join(" | ", serverErrors));

                Console.WriteLine(
                    "      客户端 " + sessionId + " 经通道链收到 LoginResponse：RequestId=" + response.RequestId +
                    "，Token=" + response.Token + "；出站回写心跳 Seq=" + heartbeatSeq);

                client.Disconnect();
                await executor.Completion.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                VerifySessionRemoved(host, sessionId);
            }
            finally
            {
                try { client?.Dispose(); } catch { }
                try { server?.Stop(); } catch { }
                try { server?.Dispose(); } catch { }
                if (certificate != null)
                    certificate.Dispose();
            }
        }

        // ---------- 性能趋势（--bench） ----------

        private const int BenchChunkSize = 64 * 1024;

        private const int BenchIterations = 5;

        private static async Task<int> RunBenchAsync()
        {
            const int frameCount = 500000;
            const int bodyLength = 256;

            var wire = BuildBenchWire(frameCount, bodyLength);
            var chunks = Split(wire, BenchChunkSize);

            Console.WriteLine("SAEA.Sockets.PipelineTest --bench");
            Console.WriteLine(
                "线格式：8B 小端 body 长度 + 1B 类型码 + " + bodyLength + "B body，共 " + frameCount +
                " 帧，" + BenchIterations + " 轮取中位数");

            // 预热一轮，把 JIT 与首次租借排除在测量窗口外。
            BenchLegacy(chunks, frameCount, 1);
            BenchDecodeLayer(chunks, frameCount, 1);
            await BenchPumpAsync(chunks, frameCount, 1, false).ConfigureAwait(false);
            await BenchPumpAsync(chunks, frameCount, 1, true).ConfigureAwait(false);

            var legacy = BenchLegacy(chunks, frameCount, BenchIterations);
            var decode = BenchDecodeLayer(chunks, frameCount, BenchIterations);
            var pump = await BenchPumpAsync(chunks, frameCount, BenchIterations, false).ConfigureAwait(false);
            var inlinePump = await BenchPumpAsync(chunks, frameCount, BenchIterations, true).ConfigureAwait(false);

            Console.WriteLine("  旧栈 BaseCoder.DecodeStream 直连回调：" + legacy);
            Console.WriteLine("  新栈 分帧 + Envelope 解码：" + decode);
            Console.WriteLine("  新栈 接收泵 + 入队 + 处理器（Queued）：" + pump);
            Console.WriteLine("  新栈 接收泵 + 内联分发 + 处理器（Inline）：" + inlinePump);
            Console.WriteLine(
                "  吞吐比（新/旧）：解码层 " +
                (decode.FramesPerSecond / legacy.FramesPerSecond * 100).ToString("F1") +
                "%，完整接收路径 Queued " +
                (pump.FramesPerSecond / legacy.FramesPerSecond * 100).ToString("F1") +
                "%，Inline " +
                (inlinePump.FramesPerSecond / legacy.FramesPerSecond * 100).ToString("F1") + "%");
            Console.WriteLine("（趋势口径，不作门禁；详见设计文档 8.4）");
            return 0;
        }

        private static byte[] BuildBenchWire(int frameCount, int bodyLength)
        {
            var payload = new byte[bodyLength];
            for (var i = 0; i < payload.Length; i++)
                payload[i] = (byte)(i & 0xFF);

            var writer = new ArrayBufferWriter<byte>((bodyLength + FrameConstants.P_Head) * frameCount);
            var encoder = new LengthFieldFrameEncoder();
            for (var i = 0; i < frameCount; i++)
            {
                // 类型码避开 1（Heart）与 3（BigData），否则 BaseCoder 不会把它们当普通数据帧回调。
                encoder.Encode(payload, 7, writer);
            }

            return writer.WrittenSpan.ToArray();
        }

        private static byte[][] Split(byte[] data, int chunkSize)
        {
            var count = (data.Length + chunkSize - 1) / chunkSize;
            var chunks = new byte[count][];
            for (var i = 0; i < count; i++)
            {
                var offset = i * chunkSize;
                var length = Math.Min(chunkSize, data.Length - offset);
                var chunk = new byte[length];
                Buffer.BlockCopy(data, offset, chunk, 0, length);
                chunks[i] = chunk;
            }

            return chunks;
        }

        private static BenchResult BenchLegacy(byte[][] chunks, int expectedFrames, int iterations)
        {
            var samples = new List<BenchResult>(iterations);
            for (var iteration = 0; iteration < iterations; iteration++)
            {
                var coder = new BaseCoder();
                var counter = new CountingFrameHandler();

                var allocatedBefore = GC.GetTotalAllocatedBytes(false);
                var watch = Stopwatch.StartNew();

                for (var i = 0; i < chunks.Length; i++)
                    coder.DecodeStream(chunks[i], counter);

                watch.Stop();
                var allocated = GC.GetTotalAllocatedBytes(false) - allocatedBefore;

                if (counter.Count != expectedFrames)
                    throw new InvalidOperationException(
                        "旧栈解码帧数不符：期望 " + expectedFrames + "，实际 " + counter.Count);

                samples.Add(new BenchResult(counter.Count, watch.ElapsedTicks, allocated));
            }

            return BenchResult.Median(samples);
        }

        private static BenchResult BenchDecodeLayer(byte[][] chunks, int expectedFrames, int iterations)
        {
            var splitter = LengthFieldSplitter.Default(MaxFrameLength);
            var codec = new EnvelopeCodec();
            var samples = new List<BenchResult>(iterations);

            for (var iteration = 0; iteration < iterations; iteration++)
            {
                var accumulator = new FrameAccumulator();
                var count = 0;
                BenchResult result;

                var allocatedBefore = GC.GetTotalAllocatedBytes(false);
                var watch = Stopwatch.StartNew();

                try
                {
                    for (var i = 0; i < chunks.Length; i++)
                    {
                        accumulator.Append(chunks[i]);

                        while (true)
                        {
                            long consumed;
                            ReadOnlySequence<byte> frame;
                            if (!splitter.TrySplit(new ReadOnlySequence<byte>(accumulator.Buffered), out consumed, out frame))
                                break;

                            accumulator.Consume((int)consumed);

                            // 累加器背衬连续，切出的帧必为单段，First 就是整帧。
                            if (codec.Decode(frame.First) != null)
                                count++;
                        }
                    }

                    watch.Stop();
                    var allocated = GC.GetTotalAllocatedBytes(false) - allocatedBefore;
                    result = new BenchResult(count, watch.ElapsedTicks, allocated);
                }
                finally
                {
                    accumulator.Dispose();
                }

                if (result.Frames != expectedFrames)
                    throw new InvalidOperationException(
                        "解码层帧数不符：期望 " + expectedFrames + "，实际 " + result.Frames);

                samples.Add(result);
            }

            return BenchResult.Median(samples);
        }

        private static async Task<BenchResult> BenchPumpAsync(byte[][] chunks, int expectedFrames, int iterations, bool inline)
        {
            var options = NewOptions(queueCapacity: 8192, maxConcurrentMessages: 8);
            if (inline)
                options.DispatchMode = ChannelDispatchMode.Inline;

            var samples = new List<BenchResult>(iterations);

            for (var iteration = 0; iteration < iterations; iteration++)
            {
                using (var pipeline = ChannelPipeline.Create(options))
                {
                    var stream = new FakeRawByteStream("bench");
                    var channel = new DefaultChannel("bench", reason => Task.CompletedTask);
                    var host = new PipelineHost();
                    host.Set(channel);
                    var handler = new BenchCountingHandler();
                    pipeline.AddLast(handler);

                    var executor = new ChannelPipelineExecutor(stream, channel, options, CancellationToken.None, host);
                    executor.Attach(pipeline);
                    executor.Start();

                    var allocatedBefore = GC.GetTotalAllocatedBytes(false);
                    var watch = Stopwatch.StartNew();

                    for (var i = 0; i < chunks.Length; i++)
                        await stream.WriteAsync(chunks[i]).ConfigureAwait(false);

                    await WaitUntilAsync(
                        () => handler.Count == expectedFrames, "性能用例未在超时前处理完所有帧").ConfigureAwait(false);

                    watch.Stop();
                    var allocated = GC.GetTotalAllocatedBytes(false) - allocatedBefore;

                    await executor.BeginShutdown("bench-done").ConfigureAwait(false);
                    await executor.Completion.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

                    samples.Add(new BenchResult(expectedFrames, watch.ElapsedTicks, allocated));
                }
            }

            return BenchResult.Median(samples);
        }

        private sealed class BenchResult
        {
            public BenchResult(int frames, long elapsedTicks, long allocatedBytes)
            {
                Frames = frames;
                ElapsedTicks = elapsedTicks;
                AllocatedBytes = allocatedBytes;
            }

            public int Frames { get; }

            public long ElapsedTicks { get; }

            public long AllocatedBytes { get; }

            /// <summary>
            /// 取各轮样本耗时的中位数，避免单轮 GC 或调度抖动放大吞吐差异。
            /// </summary>
            /// <param name="samples">各轮样本，至少一个</param>
            /// <returns>耗时中位数所在的那一轮样本</returns>
            public static BenchResult Median(List<BenchResult> samples)
            {
                samples.Sort((left, right) => left.ElapsedTicks.CompareTo(right.ElapsedTicks));
                return samples[samples.Count / 2];
            }

            public double FramesPerSecond
            {
                get { return Frames / (ElapsedTicks / (double)Stopwatch.Frequency); }
            }

            public double BytesPerFrame
            {
                get { return AllocatedBytes / (double)Frames; }
            }

            public override string ToString()
            {
                return FramesPerSecond.ToString("F0") + " 帧/s，耗时 " +
                    (ElapsedTicks * 1000.0 / Stopwatch.Frequency).ToString("F0") + "ms，" +
                    BytesPerFrame.ToString("F0") + "B/帧";
            }
        }

        private sealed class CountingFrameHandler : IFrameHandler
        {
            private int _count;

            public int Count
            {
                get { return Volatile.Read(ref _count); }
            }

            public void OnFrame(in SocketFrame frame)
            {
                Interlocked.Increment(ref _count);
            }
        }

        private sealed class BenchCountingHandler : InboundHandlerAdapter
        {
            private int _count;

            public int Count
            {
                get { return Volatile.Read(ref _count); }
            }

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                // 空链上没有编解码器，消息就是 PooledMessage，必须在此归还池化缓冲。
                var disposable = msg as IDisposable;
                if (disposable != null)
                    disposable.Dispose();

                Interlocked.Increment(ref _count);
                return Task.CompletedTask;
            }
        }

        private static async Task RunSocketScenarioAsync(bool useTls)
        {
            var port = ResolvePort();
            var registry = CreateRegistry();
            var host = new PipelineHost();
            var executors = new ConcurrentDictionary<string, ChannelPipelineExecutor>(StringComparer.Ordinal);
            var errors = new ConcurrentQueue<string>();
            var executorReady = new TaskCompletionSource<ChannelPipelineExecutor>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            SaeaServerSocket server = null;
            var certificate = useTls ? CreateServerCertificate() : null;
            var handshakeCompletedAt = DateTime.MinValue;

            try
            {
                var builder = new SocketOptionBuilder()
                    .UseStream()
                    .SetIP("127.0.0.1")
                    .SetPort(port)
                    .UseConnectionOpened(info =>
                    {
                        var channel = new DefaultChannel(info.ID, reason =>
                        {
                            ChannelPipelineExecutor executor;
                            if (executors.TryGetValue(info.ID, out executor))
                                return executor.BeginShutdown(reason);

                            try { server?.Disconnect(info.ID); } catch { }
                            return Task.CompletedTask;
                        });

                        channel.SetAttribute(TokenAuthHandler.TokenAttributeKey, "socket-seed-token");
                        channel.SetAttribute("openedAt", DateTime.UtcNow);
                        host.Set(channel);

                        // 验收 11：TLS 下连接建立回调只应在握手成功后触发。
                        if (useTls && (info.Tls == null || !info.Tls.IsAuthenticated))
                            throw new InvalidOperationException("TLS 管道在握手完成之前启动。");

                        // 模拟耗时 50ms 的连接建立回调，首帧仍应在映射已存在之后到达。
                        Thread.Sleep(50);
                    })
                    .UseInputHandler(async (userToken, input, flow, token) =>
                    {
                        var id = userToken.ID;

                        IChannel channel;
                        if (!host.TryGetChannel(id, out channel))
                            throw new InvalidOperationException("连接建立钩子未登记通道：" + id);
                        if (channel.GetAttribute("openedAt") == null)
                            throw new InvalidOperationException("通道缺少 openedAt 属性：" + id);
                        if (useTls && handshakeCompletedAt == DateTime.MinValue)
                            throw new InvalidOperationException("TLS 首帧到达时握手尚未完成。");

                        var stream = new SocketStreamAdapter(id, input, flow, server);
                        var options = NewOptions(queueCapacity: 128, maxConcurrentMessages: 4);
                        var pipeline = BuildPipeline(
                            options,
                            registry,
                            new LoginHandler(30),
                            new HeartbeatHandler(),
                            new ExceptionRecorder());

                        var executor = new ChannelPipelineExecutor(stream, channel, options, token, host);
                        executor.Attach(pipeline);
                        executors[id] = executor;
                        executor.Start();
                        executorReady.TrySetResult(executor);

                        try
                        {
                            await executor.Completion.ConfigureAwait(false);
                        }
                        finally
                        {
                            ChannelPipelineExecutor removed;
                            executors.TryRemove(id, out removed);
                        }
                    });

                if (useTls)
                {
                    builder = builder.WithSsl(o =>
                    {
                        o.ServerCertificate = certificate;
                        o.HandshakeCompleted = context => handshakeCompletedAt = DateTime.UtcNow;
                    });
                }

                var option = builder.Build();

                server = new SaeaServerSocket(option);
                server.OnError += (id, ex) => errors.Enqueue(id + ": " + ex.GetType().Name + ": " + ex.Message);
                server.Start();

                if (useTls)
                {
                    var clientOption = new SocketOptionBuilder()
                        .UseStream()
                        .SetIP("127.0.0.1")
                        .SetPort(port)
                        .SetReadBufferSize(64 * 1024)
                        .WithSsl(o =>
                        {
                            o.TargetHost = "localhost";
                            o.AllowInvalidCertificates = true;
                        })
                        .Build();

                    using (var client = new SaeaClientSocket(clientOption))
                    {
                        client.Connect();
                        if (!client.IsAuthenticated)
                            throw new InvalidOperationException("TLS 客户端握手未完成。");

                        var executor = await executorReady.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                        var sessionId = executor.Channel.Id;

                        var frame = EncodeFrame(new LoginRequest { RequestId = 42, UserName = "tls" }, registry);
                        var stream = client.GetStream();
                        await stream.WriteAsync(frame, 0, frame.Length).ConfigureAwait(false);
                        await stream.FlushAsync().ConfigureAwait(false);

                        var reply = DecodeFrame(ReadFrame(stream), registry) as LoginResponse;
                        if (reply == null)
                            throw new InvalidOperationException("服务端回包不是 LoginResponse。");
                        if (reply.RequestId != 42 || !reply.Success || string.IsNullOrEmpty(reply.Token))
                            throw new InvalidOperationException("登录回包字段不正确。");
                        if (handshakeCompletedAt == DateTime.MinValue)
                            throw new InvalidOperationException("TLS 握手完成回调未触发。");

                        Console.WriteLine(
                            "      客户端 " + sessionId + " 经 TLS 收到 LoginResponse：RequestId=" + reply.RequestId +
                            "，握手完成于 " + handshakeCompletedAt.ToString("HH:mm:ss.fff"));

                        client.Disconnect();

                        await executor.Completion.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                        VerifySessionRemoved(host, sessionId);
                        ReportErrors(errors);
                    }
                }
                else
                {
                    using (var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                    {
                        client.ReceiveTimeout = 5000;
                        client.SendTimeout = 5000;
                        client.Connect(new IPEndPoint(IPAddress.Loopback, port));

                        var executor = await executorReady.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                        var sessionId = executor.Channel.Id;

                        SendAll(client, EncodeFrame(new LoginRequest { RequestId = 42, UserName = "socket" }, registry));

                        var reply = DecodeFrame(ReadFrame(client), registry) as LoginResponse;
                        if (reply == null)
                            throw new InvalidOperationException("服务端回包不是 LoginResponse。");
                        if (reply.RequestId != 42 || !reply.Success || string.IsNullOrEmpty(reply.Token))
                            throw new InvalidOperationException("登录回包字段不正确。");

                        Console.WriteLine(
                            "      客户端 " + sessionId + " 收到 LoginResponse：RequestId=" + reply.RequestId +
                            "，Token=" + reply.Token);

                        client.Shutdown(SocketShutdown.Both);

                        await executor.Completion.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                        VerifySessionRemoved(host, sessionId);
                        ReportErrors(errors);
                    }
                }
            }
            finally
            {
                try { server?.Stop(); } catch { }
                try { server?.Dispose(); } catch { }
                if (certificate != null)
                    certificate.Dispose();
            }
        }

        private static void VerifySessionRemoved(PipelineHost host, string sessionId)
        {
            IChannel removed;
            if (host.TryGetChannel(sessionId, out removed))
                throw new InvalidOperationException("收口后通道仍留在登记表：" + sessionId);
        }

        private static void ReportErrors(ConcurrentQueue<string> errors)
        {
            if (!errors.IsEmpty)
                Console.WriteLine("      服务端上报：" + string.Join(" | ", errors.ToArray()));
        }

        private static int ResolvePort()
        {
            var raw = Environment.GetEnvironmentVariable("SAEA_PIPELINE_TEST_PORT");
            int port;
            if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out port) && port > 0 && port <= 65535)
                return port;

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        // ---------- 标准装配 ----------

        private static ChannelPipeline BuildPipeline(
            ChannelPipelineOptions options,
            TypeRegistry registry,
            IMessageHandler loginHandler,
            IMessageHandler heartbeatHandler,
            ExceptionRecorder errors,
            IChannelHandler extraInbound = null)
        {
            var pipeline = ChannelPipeline.Create(options);
            var codec = new EnvelopeCodec();

            // 出站必须是链首连续前缀：串行器 → 实体 → 信封 → body → 完整帧 → 传输层。
            pipeline.AddLast(new FrameEncoderHandler(new LengthFieldFrameEncoder()));
            pipeline.AddLast(new OutboundLoggingHandler());
            pipeline.AddLast(new CodecEncoderHandler(codec));
            pipeline.AddLast(new EnvelopeWrapHandler(registry, new JsonMessageCodec()));

            // 串行器必须是出站前缀最右侧（紧贴第一个入站节点）：先排队再编码，避免异步编码器打乱先后。
            pipeline.AddLast(new OutboundSerializingHandler());

            // 入站：分帧由接收泵完成，链上没有 FrameSplitterHandler。
            pipeline.AddLast(new CodecDecoderHandler(codec));
            pipeline.AddLast(new AuthHandler(new[] { MessageTypeCodes.LoginRequest, MessageTypeCodes.Heartbeat }));
            pipeline.AddLast(new EnvelopeUnwrapHandler(registry, new JsonMessageCodec()));
            pipeline.AddLast(new TokenAuthHandler());
            if (extraInbound != null)
                pipeline.AddLast(extraInbound);
            pipeline.AddLast(new InboundLoggingHandler());
            pipeline.AddLast(new MessageDispatcher()
                .Register<LoginRequest>(loginHandler)
                .Register<Heartbeat>(heartbeatHandler));
            pipeline.AddLast(errors);
            pipeline.AddLast(new ExceptionTerminalHandler());

            return pipeline;
        }

        /// <summary>
        /// 客户端链：出站编码与入站解码复用同一套编解码器，只保留响应分发，不配置服务端鉴权。
        /// </summary>
        private static ChannelPipeline BuildClientPipeline(
            ChannelPipelineOptions options,
            TypeRegistry registry,
            LoginResponseHandler responseHandler)
        {
            var pipeline = ChannelPipeline.Create(options);
            var codec = new EnvelopeCodec();

            pipeline.AddLast(new FrameEncoderHandler(new LengthFieldFrameEncoder()));
            pipeline.AddLast(new CodecEncoderHandler(codec));
            pipeline.AddLast(new EnvelopeWrapHandler(registry, new JsonMessageCodec()));

            // 客户端链与服务端链保持相同位置：串行器在出站前缀最右侧。
            pipeline.AddLast(new OutboundSerializingHandler());

            pipeline.AddLast(new CodecDecoderHandler(codec));
            pipeline.AddLast(new EnvelopeUnwrapHandler(registry, new JsonMessageCodec()));
            pipeline.AddLast(new MessageDispatcher().Register<LoginResponse>(responseHandler));
            pipeline.AddLast(new ExceptionRecorder());
            pipeline.AddLast(new ExceptionTerminalHandler());

            return pipeline;
        }

        /// <summary>
        /// 构造演示用例配置。<paramref name="maxConcurrentMessages"/> 取 1 的用例用于验证逐帧保序
        /// 或稳定制造背压；并发与超时用例分别取 5 与 2，不要把所有用例都压成单并发。
        /// </summary>
        private static ChannelPipelineOptions NewOptions(
            int queueCapacity,
            int maxConcurrentMessages,
            TimeSpan? messageTimeout = null,
            bool requireTerminalHandler = false)
        {
            return new ChannelPipelineOptions
            {
                Splitter = LengthFieldSplitter.Default(MaxFrameLength),
                QueueCapacity = queueCapacity,
                MaxConcurrentMessages = maxConcurrentMessages,
                MessageTimeout = messageTimeout ?? TimeSpan.Zero,
                DrainPolicy = DisconnectDrainPolicy.CancelPending,
                Guard = NullChannelGuard.Instance,
                RequireTerminalHandler = requireTerminalHandler
            };
        }

        private static TypeRegistry CreateRegistry()
        {
            var registry = new TypeRegistry();
            registry.Register(MessageTypeCodes.LoginRequest, typeof(LoginRequest));
            registry.Register(MessageTypeCodes.LoginResponse, typeof(LoginResponse));
            registry.Register(MessageTypeCodes.Heartbeat, typeof(Heartbeat));
            return registry;
        }

        private static class MessageTypeCodes
        {
            public const int LoginRequest = 1;
            public const int LoginResponse = 2;
            public const int Heartbeat = 3;
        }

        // ---------- 线格式工具 ----------

        private static byte[] EncodeFrame(object entity, TypeRegistry registry)
        {
            var payload = new JsonMessageCodec().Encode(entity);
            var typeCode = registry.ResolveCode(entity.GetType());
            if (typeCode < 0)
                throw new InvalidOperationException("类型未注册：" + entity.GetType().FullName);

            var writer = new ArrayBufferWriter<byte>(payload.Length + FrameConstants.P_Head);
            new LengthFieldFrameEncoder().Encode(payload, typeCode, writer);
            return writer.WrittenSpan.ToArray();
        }

        private static List<object> DecodeFrames(byte[][] wires, TypeRegistry registry)
        {
            var result = new List<object>(wires.Length);
            foreach (var wire in wires)
                result.Add(DecodeFrame(wire, registry));

            return result;
        }

        private static object DecodeFrame(byte[] wire, TypeRegistry registry)
        {
            var splitter = LengthFieldSplitter.Default(MaxFrameLength);
            long consumed;
            ReadOnlySequence<byte> frame;
            if (!splitter.TrySplit(new ReadOnlySequence<byte>(wire), out consumed, out frame) || consumed != wire.Length)
                throw new InvalidOperationException("发送出去的字节不是一条完整帧，长度 " + wire.Length + "。");

            var envelope = new EnvelopeCodec().Decode(frame.First) as Envelope;
            if (envelope == null)
                throw new InvalidOperationException("第一层解码未产出 Envelope。");

            var target = registry.Resolve(envelope.Type);
            if (target == null)
                throw new InvalidOperationException("未知 type code：" + envelope.Type);

            return new JsonMessageCodec().Decode(envelope.Payload ?? Array.Empty<byte>(), target);
        }

        private static async Task WriteHeartbeatAsync(FakeRawByteStream stream, TypeRegistry registry, int seq)
        {
            await stream.WriteAsync(
                EncodeFrame(new Heartbeat { Seq = seq, Payload = "p" + seq }, registry)).ConfigureAwait(false);
        }

        private static async Task WriteSegmentsAsync(FakeRawByteStream stream, byte[] wire, int[] segments)
        {
            var offset = 0;
            var index = 0;
            while (offset < wire.Length)
            {
                var size = Math.Min(segments[index % segments.Length], wire.Length - offset);
                var chunk = new byte[size];
                Buffer.BlockCopy(wire, offset, chunk, 0, size);
                await stream.WriteAsync(chunk).ConfigureAwait(false);
                offset += size;
                index++;
            }
        }

        private static byte[] Concat(params byte[][] parts)
        {
            var total = parts.Sum(part => part.Length);
            var result = new byte[total];
            var offset = 0;
            foreach (var part in parts)
            {
                Buffer.BlockCopy(part, 0, result, offset, part.Length);
                offset += part.Length;
            }

            return result;
        }

        private static async Task WaitUntilAsync(Func<bool> condition, string message)
        {
            var watch = Stopwatch.StartNew();
            while (!condition())
            {
                if (watch.ElapsedMilliseconds > WaitTimeoutMilliseconds)
                    throw new TimeoutException("等待超时：" + message);
                await Task.Delay(10).ConfigureAwait(false);
            }
        }

        private static void AssertSequenceEqual(int[] expected, int[] actual, string message)
        {
            if (expected.SequenceEqual(actual))
                return;

            throw new InvalidOperationException(
                message + "；期望 [" + string.Join(",", expected) + "]，实际 [" + string.Join(",", actual) + "]");
        }

        private static void SendAll(Socket socket, byte[] data)
        {
            var offset = 0;
            while (offset < data.Length)
                offset += socket.Send(data, offset, data.Length - offset, SocketFlags.None);
        }

        private static byte[] ReadFrame(Socket socket)
        {
            var head = new byte[FrameConstants.P_Head];
            ReadExactly(socket, head, 0, head.Length);

            var bodyLength = BinaryPrimitives.ReadInt64LittleEndian(head.AsSpan(0, FrameConstants.P_LEN));
            if (bodyLength < 0 || bodyLength > MaxFrameLength)
                throw new InvalidOperationException("回包帧体长度非法：" + bodyLength);

            var frame = new byte[head.Length + (int)bodyLength];
            Buffer.BlockCopy(head, 0, frame, 0, head.Length);
            ReadExactly(socket, frame, head.Length, (int)bodyLength);
            return frame;
        }

        private static void ReadExactly(Socket socket, byte[] buffer, int offset, int count)
        {
            var read = 0;
            while (read < count)
            {
                var current = socket.Receive(buffer, offset + read, count - read, SocketFlags.None);
                if (current <= 0)
                    throw new EndOfStreamException("对端在读取完整帧之前关闭了连接。");
                read += current;
            }
        }

        private static byte[] ReadFrame(Stream stream)
        {
            var head = new byte[FrameConstants.P_Head];
            ReadExactly(stream, head, 0, head.Length);

            var bodyLength = BinaryPrimitives.ReadInt64LittleEndian(head.AsSpan(0, FrameConstants.P_LEN));
            if (bodyLength < 0 || bodyLength > MaxFrameLength)
                throw new InvalidOperationException("回包帧体长度非法：" + bodyLength);

            var frame = new byte[head.Length + (int)bodyLength];
            Buffer.BlockCopy(head, 0, frame, 0, head.Length);
            ReadExactly(stream, frame, head.Length, (int)bodyLength);
            return frame;
        }

        private static void ReadExactly(Stream stream, byte[] buffer, int offset, int count)
        {
            var read = 0;
            while (read < count)
            {
                var current = stream.Read(buffer, offset + read, count - read);
                if (current <= 0)
                    throw new EndOfStreamException("对端在读取完整帧之前关闭了连接。");
                read += current;
            }
        }

        /// <summary>
        /// 生成仅用于本地回环演示的自签名服务端证书；私钥经 PFX 往返，保证可被 TLS 传输层稳定读取。
        /// </summary>
        private static X509Certificate2 CreateServerCertificate()
        {
            using (var rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest(
                    "CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));

                var usages = new OidCollection
                {
                    new Oid("1.3.6.1.5.5.7.3.1")
                };
                request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usages, false));

                var san = new SubjectAlternativeNameBuilder();
                san.AddDnsName("localhost");
                san.AddIpAddress(IPAddress.Loopback);
                request.CertificateExtensions.Add(san.Build());

                using (var certificate = request.CreateSelfSigned(
                    DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30)))
                {
                    var pfx = certificate.Export(X509ContentType.Pfx, "saea");
                    return X509CertificateLoader.LoadPkcs12(pfx, "saea");
                }
            }
        }

        // ---------- 内存回环基础设施 ----------

        private sealed class InMemorySession : IDisposable
        {
            private int _disposed;

            public InMemorySession(
                string id,
                ChannelPipelineOptions options,
                TypeRegistry registry,
                IMessageHandler loginHandler,
                IMessageHandler heartbeatHandler,
                IChannelHandler extraInbound = null)
            {
                Stream = new FakeRawByteStream(id);
                Channel = new DefaultChannel(id, reason => BeginShutdown(reason));
                Channel.SetAttribute(TokenAuthHandler.TokenAttributeKey, "seed-token");
                Host = new PipelineHost();
                Host.Set(Channel);

                Errors = new ExceptionRecorder();
                Pipeline = BuildPipeline(options, registry, loginHandler, heartbeatHandler, Errors, extraInbound);
                Executor = new ChannelPipelineExecutor(Stream, Channel, options, CancellationToken.None, Host);
                Executor.Attach(Pipeline);
            }

            public FakeRawByteStream Stream { get; }

            public DefaultChannel Channel { get; }

            public PipelineHost Host { get; }

            public ChannelPipeline Pipeline { get; }

            public ChannelPipelineExecutor Executor { get; }

            public ExceptionRecorder Errors { get; }

            public void Start()
            {
                Executor.Start();
            }

            public async Task StopAsync()
            {
                await Executor.BeginShutdown("case-done").ConfigureAwait(false);
                await Executor.Completion.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }

            private Task BeginShutdown(string reason)
            {
                var executor = Executor;
                return executor == null ? Task.CompletedTask : executor.BeginShutdown(reason);
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                    return;

                try { Executor.Dispose(); } catch { }
                try { Stream.Dispose(); } catch { }
            }
        }

        private sealed class FakeRawByteStream : IRawByteStream
        {
            private readonly Pipe _pipe;
            private readonly object _sentGate = new object();
            private readonly List<byte[]> _sent = new List<byte[]>();

            private readonly TaskCompletionSource<string> _closed =
                new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            private int _pauseCount;
            private int _resumeCount;

            public FakeRawByteStream(string id)
            {
                ChannelId = id;
                _pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 4 * 1024 * 1024));
            }

            public string ChannelId { get; }

            public PipeReader Input
            {
                get { return _pipe.Reader; }
            }

            public int PauseCount
            {
                get { return Volatile.Read(ref _pauseCount); }
            }

            public int ResumeCount
            {
                get { return Volatile.Read(ref _resumeCount); }
            }

            public string CloseReason
            {
                get { return _closed.Task.IsCompleted ? _closed.Task.Result : null; }
            }

            public int SentCount
            {
                get
                {
                    lock (_sentGate)
                    {
                        return _sent.Count;
                    }
                }
            }

            public byte[][] SentFrames
            {
                get
                {
                    lock (_sentGate)
                    {
                        return _sent.ToArray();
                    }
                }
            }

            public async Task WriteAsync(byte[] data)
            {
                await _pipe.Writer.WriteAsync(data).ConfigureAwait(false);
                await _pipe.Writer.FlushAsync().ConfigureAwait(false);
            }

            public Task SendAsync(ReadOnlyMemory<byte> data, IDisposable owner)
            {
                lock (_sentGate)
                {
                    // 先拷贝再释放：owner 可能持有池化缓冲。
                    _sent.Add(data.ToArray());
                }

                if (owner != null)
                    owner.Dispose();

                return Task.CompletedTask;
            }

            public void Pause()
            {
                Interlocked.Increment(ref _pauseCount);
            }

            public void Resume()
            {
                Interlocked.Increment(ref _resumeCount);
            }

            public Task CloseAsync(string reason)
            {
                _closed.TrySetResult(reason);
                return Task.CompletedTask;
            }

            public void Dispose()
            {
                try { _pipe.Writer.Complete(); } catch { }
                try { _pipe.Reader.Complete(); } catch { }
            }
        }

        // ---------- 演示专用处理器 ----------

        /// <summary>捕获所属节点上下文，供用例在回调外验证 ctx.WriteAsync 的拒绝行为。</summary>
        private sealed class ContextProbeHandler : InboundHandlerAdapter
        {
            public readonly TaskCompletionSource<IChannelHandlerContext> Captured =
                new TaskCompletionSource<IChannelHandlerContext>(TaskCreationOptions.RunContinuationsAsynchronously);

            public override Task ChannelActiveAsync(IChannelHandlerContext ctx)
            {
                Captured.TrySetResult(ctx);
                return Task.CompletedTask;
            }
        }

        /// <summary>记录异常并继续向链尾传播，用于观察超时与意外错误。</summary>
        private sealed class ExceptionRecorder : InboundHandlerAdapter
        {
            private readonly List<Exception> _exceptions = new List<Exception>();

            private readonly TaskCompletionSource<bool> _timeoutSeen =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Exception[] Exceptions
            {
                get
                {
                    lock (_exceptions)
                    {
                        return _exceptions.ToArray();
                    }
                }
            }

            public int TimeoutCount
            {
                get { return Exceptions.Count(ex => ex is MessageTimeoutException); }
            }

            public async Task WaitForTimeoutAsync(TimeSpan timeout)
            {
                await _timeoutSeen.Task.WaitAsync(timeout).ConfigureAwait(false);
            }

            public override async Task ChannelExceptionCaughtAsync(IChannelHandlerContext ctx, Exception ex)
            {
                lock (_exceptions)
                {
                    _exceptions.Add(ex);
                }

                if (ex is MessageTimeoutException)
                    _timeoutSeen.TrySetResult(true);

                await ctx.FireExceptionCaughtAsync(ex).ConfigureAwait(false);
            }
        }

        /// <summary>第 1 帧阻塞在闸门上，其余帧立即完成；用于稳定制造队列积压。</summary>
        private sealed class GatedHeartbeatHandler : IMessageHandler
        {
            private readonly TaskCompletionSource<bool> _gate =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly List<int> _sequence = new List<int>();

            private int _count;

            public readonly TaskCompletionSource<bool> FirstEntered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public int Count
            {
                get { return Volatile.Read(ref _count); }
            }

            public int[] Sequence
            {
                get
                {
                    lock (_sequence)
                    {
                        return _sequence.ToArray();
                    }
                }
            }

            public void Release()
            {
                _gate.TrySetResult(true);
            }

            public async Task HandleAsync(IChannelHandlerContext ctx, object message)
            {
                var heartbeat = (Heartbeat)message;

                lock (_sequence)
                {
                    _sequence.Add(heartbeat.Seq);
                }

                if (heartbeat.Seq == 1)
                {
                    FirstEntered.TrySetResult(true);
                    await _gate.Task.WaitAsync(ctx.CancellationToken).ConfigureAwait(false);
                }

                Interlocked.Increment(ref _count);
            }
        }

        /// <summary>第 1 帧等待自身 token 直到超时，第 2 帧 20ms 后正常回包。</summary>
        private sealed class TimeoutHeartbeatHandler : IMessageHandler
        {
            public readonly TaskCompletionSource<bool> WaiterCancelled =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public readonly TaskCompletionSource<bool> QuickDone =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public async Task HandleAsync(IChannelHandlerContext ctx, object message)
            {
                var heartbeat = (Heartbeat)message;

                if (heartbeat.Seq == 1)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), ctx.CancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        WaiterCancelled.TrySetResult(ctx.CancellationToken.IsCancellationRequested);
                        throw;
                    }
                }
                else
                {
                    await Task.Delay(20, ctx.CancellationToken).ConfigureAwait(false);
                    await ctx.Pipeline.WriteAsync(new Heartbeat
                    {
                        Seq = heartbeat.Seq,
                        Payload = heartbeat.Payload
                    }).ConfigureAwait(false);
                    QuickDone.TrySetResult(true);
                }
            }
        }

        /// <summary>
        /// 在消息执行过程中关闭连接，随后立刻回包；此时消息仍在途，收口尚未释放通道链，
        /// 出站链会正常走完并由链头以 ChannelClosedException 拒绝。
        /// </summary>
        private sealed class CloseInsideHandler : IMessageHandler
        {
            public readonly TaskCompletionSource<Exception> ClosedWriteError =
                new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

            public async Task HandleAsync(IChannelHandlerContext ctx, object message)
            {
                await ctx.Channel.CloseAsync("test").ConfigureAwait(false);

                try
                {
                    await ctx.Pipeline.WriteAsync(new Heartbeat { Seq = 99, Payload = "after-close" })
                        .ConfigureAwait(false);
                    ClosedWriteError.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    ClosedWriteError.TrySetResult(ex);
                }
            }
        }

        /// <summary>
        /// 客户端响应处理器：记录 LoginResponse，并按需经出站方向回写一帧心跳，用于验证适配器的发送委托。
        /// </summary>
        private sealed class LoginResponseHandler : IMessageHandler
        {
            private readonly int _heartbeatSeq;

            private readonly TaskCompletionSource<LoginResponse> _received =
                new TaskCompletionSource<LoginResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>指定回写心跳的序号；0 表示不回写。</summary>
            /// <param name="heartbeatSeq">心跳序号，小于 0 抛 ArgumentOutOfRangeException</param>
            public LoginResponseHandler(int heartbeatSeq)
            {
                if (heartbeatSeq < 0)
                    throw new ArgumentOutOfRangeException(nameof(heartbeatSeq), "心跳序号不能为负。");

                _heartbeatSeq = heartbeatSeq;
            }

            /// <summary>等待中的响应；只允许完成一次。</summary>
            public Task<LoginResponse> Received
            {
                get { return _received.Task; }
            }

            /// <inheritdoc />
            public Task HandleAsync(IChannelHandlerContext ctx, object message)
            {
                var response = message as LoginResponse;
                if (response == null)
                    throw new InvalidOperationException("LoginResponseHandler 只接受 LoginResponse。");

                _received.TrySetResult(response);

                if (_heartbeatSeq == 0)
                    return Task.CompletedTask;

                // 业务写只能走通道链入口；ctx.WriteAsync 仅限出站回调内转发。
                return ctx.Pipeline.WriteAsync(new Heartbeat { Seq = _heartbeatSeq });
            }
        }
    }
}