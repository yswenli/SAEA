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
using System.Threading;
using System.Threading.Tasks;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Interface;
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
                return RunSocketCaseAsync().GetAwaiter().GetResult();

            PrintUsage();
            return 0;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("SAEA.Sockets.PipelineTest 用法：");
            Console.WriteLine("  --all     运行全部内存回环用例（粘包/切分、慢鉴权、出站字节、背压、超时、断开、逐字节）");
            Console.WriteLine("  --socket  使用真实 SaeaServerSocket 跑一次端到端登录回包（默认随机空闲端口）");
            Console.WriteLine("  --help    显示本帮助");
            Console.WriteLine();
            Console.WriteLine("环境变量：SAEA_PIPELINE_TEST_PORT 指定 --socket 的监听端口（1-65535）。");
        }

        // ---------- 用例调度 ----------

        private static async Task<int> RunAllAsync()
        {
            Console.WriteLine("SAEA.Sockets.PipelineTest --all");
            Console.WriteLine("线格式：8B 小端 body 长度 + 1B 类型码 + body（UTF-8 JSON）");
            Console.WriteLine();

            var failures = 0;
            if (!await RunCaseAsync("a 内存回环：3 帧粘包与任意切分", LoopbackCaseAsync)) failures++;
            if (!await RunCaseAsync("b 慢鉴权：30ms 登录与 3 条并发消息按序号配对", SlowLoginCaseAsync)) failures++;
            if (!await RunCaseAsync("c 出站字节：全链路输出与手工帧编码逐字节一致", OutboundBytesCaseAsync)) failures++;
            if (!await RunCaseAsync("d 背压：QueueCapacity=2、单并发、10 帧只暂停一次", BackpressureCaseAsync)) failures++;
            if (!await RunCaseAsync("e 单消息超时：100ms 超时 token 被取消且连接保持活跃", MessageTimeoutCaseAsync)) failures++;
            if (!await RunCaseAsync("f 断开后回包：CloseAsync 后 WriteAsync 抛 ChannelClosedException", CloseThenWriteCaseAsync)) failures++;
            if (!await RunCaseAsync("g 逐字节喂入：每帧拆成 1 字节仍正确还原", OneByteAtATimeCaseAsync)) failures++;

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
            var login = new LoginHandler(30);
            var heartbeat = new HeartbeatHandler();

            using (var session = new InMemorySession(
                "slow-login",
                NewOptions(queueCapacity: 64, maxConcurrentMessages: 4),
                registry,
                login,
                heartbeat))
            {
                session.Start();

                var wire = Concat(
                    EncodeFrame(new LoginRequest { RequestId = 100, UserName = "slow" }, registry),
                    EncodeFrame(new Heartbeat { Seq = 11, Payload = "x" }, registry),
                    EncodeFrame(new Heartbeat { Seq = 12, Payload = "x" }, registry),
                    EncodeFrame(new Heartbeat { Seq = 13, Payload = "x" }, registry));

                var watch = Stopwatch.StartNew();
                await session.Stream.WriteAsync(wire).ConfigureAwait(false);
                await WaitUntilAsync(
                    () => session.Stream.SentCount == 4,
                    "慢鉴权回包数量不足 4").ConfigureAwait(false);
                watch.Stop();

                var replies = DecodeFrames(session.Stream.SentFrames, registry);

                var loginReplies = replies.OfType<LoginResponse>().ToArray();
                if (loginReplies.Length != 1)
                    throw new InvalidOperationException("登录回包数量不是 1：" + loginReplies.Length);
                if (loginReplies[0].RequestId != 100 || !loginReplies[0].Success)
                    throw new InvalidOperationException("登录回包字段与请求不匹配。");

                var heartbeatSeqs = replies.OfType<Heartbeat>().Select(item => item.Seq).OrderBy(seq => seq).ToArray();
                AssertSequenceEqual(new[] { 11, 12, 13 }, heartbeatSeqs, "心跳回包序号与请求不匹配");

                if (watch.ElapsedMilliseconds >= 2000)
                    throw new InvalidOperationException("慢鉴权用例耗时异常：" + watch.ElapsedMilliseconds + "ms");

                Console.WriteLine(
                    "      30ms 登录 + 3 条心跳全部回包，耗时 " + watch.ElapsedMilliseconds + "ms");

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

        // ---------- --socket：真实 SaeaServerSocket ----------

        private static async Task<int> RunSocketCaseAsync()
        {
            Console.WriteLine("SAEA.Sockets.PipelineTest --socket");

            var run = RunSocketScenarioAsync();
            var winner = await Task.WhenAny(run, Task.Delay(SocketCaseTimeoutMilliseconds)).ConfigureAwait(false);

            if (winner != run)
            {
                Console.WriteLine("FAIL  --socket : " + (SocketCaseTimeoutMilliseconds / 1000) + " 秒内未完成");
                return 1;
            }

            try
            {
                await run.ConfigureAwait(false);
                Console.WriteLine("PASS  --socket 真实 SaeaServerSocket 登录回包");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL  --socket : " + ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
        }

        private static async Task RunSocketScenarioAsync()
        {
            var port = ResolvePort();
            var registry = CreateRegistry();
            var host = new PipelineHost();
            var executors = new ConcurrentDictionary<string, ChannelPipelineExecutor>(StringComparer.Ordinal);
            var errors = new ConcurrentQueue<string>();
            var executorReady = new TaskCompletionSource<ChannelPipelineExecutor>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            SaeaServerSocket server = null;

            var option = new SocketOptionBuilder()
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

                    // 验收 11：模拟耗时 50ms 的连接建立回调，首帧仍应在映射已存在之后到达。
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
                })
                .Build();

            server = new SaeaServerSocket(option);
            server.OnError += (id, ex) => errors.Enqueue(id + ": " + ex.GetType().Name + ": " + ex.Message);
            server.Start();

            try
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

                    IChannel removed;
                    if (host.TryGetChannel(sessionId, out removed))
                        throw new InvalidOperationException("收口后通道仍留在登记表：" + sessionId);

                    if (!errors.IsEmpty)
                    {
                        Console.WriteLine("      服务端上报：" + string.Join(" | ", errors.ToArray()));
                    }
                }
            }
            finally
            {
                try { server.Stop(); } catch { }
                try { server.Dispose(); } catch { }
            }
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
            ExceptionRecorder errors)
        {
            var pipeline = ChannelPipeline.Create(options);
            var codec = new EnvelopeCodec();

            // 出站必须是链首连续前缀：实体 → 信封 → body → 完整帧 → 传输层。
            pipeline.AddLast(new FrameEncoderHandler(new LengthFieldFrameEncoder()));
            pipeline.AddLast(new OutboundLoggingHandler());
            pipeline.AddLast(new CodecEncoderHandler(codec));
            pipeline.AddLast(new EnvelopeWrapHandler(registry, new JsonMessageCodec()));

            // 入站：分帧由接收泵完成，链上没有 FrameSplitterHandler。
            pipeline.AddLast(new CodecDecoderHandler(codec));
            pipeline.AddLast(new AuthHandler(new[] { MessageTypeCodes.LoginRequest, MessageTypeCodes.Heartbeat }));
            pipeline.AddLast(new EnvelopeUnwrapHandler(registry, new JsonMessageCodec()));
            pipeline.AddLast(new TokenAuthHandler());
            pipeline.AddLast(new InboundLoggingHandler());
            pipeline.AddLast(new MessageDispatcher()
                .Register<LoginRequest>(loginHandler)
                .Register<Heartbeat>(heartbeatHandler));
            pipeline.AddLast(errors);
            pipeline.AddLast(new ExceptionTerminalHandler());

            return pipeline;
        }

        private static ChannelPipelineOptions NewOptions(
            int queueCapacity,
            int maxConcurrentMessages,
            TimeSpan? messageTimeout = null)
        {
            return new ChannelPipelineOptions
            {
                Splitter = LengthFieldSplitter.Default(MaxFrameLength),
                QueueCapacity = queueCapacity,
                MaxConcurrentMessages = maxConcurrentMessages,
                MessageTimeout = messageTimeout ?? TimeSpan.Zero,
                DrainPolicy = DisconnectDrainPolicy.CancelPending,
                Guard = NullChannelGuard.Instance
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
            int consumed;
            ReadOnlyMemory<byte> frame;
            if (!splitter.TrySplit(wire, out consumed, out frame) || consumed != wire.Length)
                throw new InvalidOperationException("发送出去的字节不是一条完整帧，长度 " + wire.Length + "。");

            var envelope = new EnvelopeCodec().Decode(frame) as Envelope;
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

        // ---------- 内存回环基础设施 ----------

        private sealed class InMemorySession : IDisposable
        {
            private int _disposed;

            public InMemorySession(
                string id,
                ChannelPipelineOptions options,
                TypeRegistry registry,
                IMessageHandler loginHandler,
                IMessageHandler heartbeatHandler)
            {
                Stream = new FakeRawByteStream(id);
                Channel = new DefaultChannel(id, reason => BeginShutdown(reason));
                Channel.SetAttribute(TokenAuthHandler.TokenAttributeKey, "seed-token");
                Host = new PipelineHost();
                Host.Set(Channel);

                Errors = new ExceptionRecorder();
                Pipeline = BuildPipeline(options, registry, loginHandler, heartbeatHandler, Errors);
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
                    await ctx.WriteAsync(new Heartbeat
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
                    await ctx.WriteAsync(new Heartbeat { Seq = 99, Payload = "after-close" })
                        .ConfigureAwait(false);
                    ClosedWriteError.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    ClosedWriteError.TrySetResult(ex);
                }
            }
        }
    }
}