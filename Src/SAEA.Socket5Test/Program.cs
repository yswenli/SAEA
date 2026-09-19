/* SAEA.Socket5Test
 * 命名空间：SAEA.Socket5Test
 * 文件名：Program.cs
 * 版本号：v26.9.20.1
 * 描述：SAEA.Socket5 自验证集成测试（控制台，失败以非 0 退出）
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Sockets;
using SAEA.Socket5;
using SAEA.Socket5.Client;
using SAEA.Socket5.Model;
using SAEA.Socket5.Server;

namespace SAEA.Socket5Test
{
    /// <summary>
    /// SAEA.Socket5 自验证集成测试
    /// </summary>
    public class Program
    {
        private static int _failures = 0;

        private static int _passed = 0;

        public static int Main()
        {
            // 并发 / 性能用例会产生较多并行阻塞线程，放宽线程池下限避免线程注入延迟干扰测量
            ThreadPool.SetMinThreads(256, 256);

            Run("无认证 CONNECT 经代理回显", TestNoAuthConnect);
            Run("用户名/密码 认证通过", TestUserPassSuccess);
            Run("用户名/密码 认证被拒", TestUserPassFailure);
            Run("BIND 两阶段握手", TestBind);
            Run("UDP ASSOCIATE 经代理回显", TestUdpAssociate);
            Run("会话缺失时 Disconnect 不抛异常", TestDisconnectMissingSession);

            Console.WriteLine();
            Console.WriteLine("=== 数据传输 / 性能 ===");

            Run("数据完整性：跨缓冲边界的多尺寸校验", TestDataTransferSizes);
            Run("数据传输：8MB 大包回显", TestLargeDataTransfer);
            Run("性能：单连接高频往返 1000 次", TestHighFrequencyRoundTrip);
            Run("性能：64 并发连接吞吐", TestConcurrentConnections);
            Run("性能：UDP 1000 数据报吞吐", TestUdpThroughput);

            Console.WriteLine();
            Console.WriteLine($"通过 {_passed} 项，失败 {_failures} 项。");

            return _failures == 0 ? 0 : 1;
        }

        private static void Run(string name, Action act)
        {
            try
            {
                act();
                _passed++;
                Console.WriteLine($"[PASS] {name}");
            }
            catch (Exception ex)
            {
                _failures++;
                Console.WriteLine($"[FAIL] {name}: {ex.Message}");
            }
        }

        internal static int GetFreeTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        internal static int GetFreeUdpPort()
        {
            var client = new UdpClient(0);
            var port = ((IPEndPoint)client.Client.LocalEndPoint).Port;
            client.Close();
            return port;
        }

        private static Socks5Server StartProxy(Socks5AuthMethod[] methods, ISocks5UserValidator validator, out int port)
        {
            port = GetFreeTcpPort();

            var options = new Socks5ServerOptions
            {
                IP = "127.0.0.1",
                Port = port,
                AllowedMethods = methods,
                UserValidator = validator
            };

            var server = new Socks5Server(options);
            server.Start();
            return server;
        }

        private static byte[] ReadExact(Stream stream, int count, int timeoutMs = 5000)
        {
            if (stream.CanTimeout)
            {
                stream.ReadTimeout = timeoutMs;
            }

            var buffer = new byte[count];
            var offset = 0;

            while (offset < count)
            {
                var n = stream.Read(buffer, offset, count - offset);

                if (n <= 0)
                {
                    throw new EndOfStreamException("读取数据不足");
                }

                offset += n;
            }

            return buffer;
        }

        /// <summary>
        /// 无认证：客户端经代理 CONNECT 到本地 TCP 回显服务
        /// </summary>
        private static void TestNoAuthConnect()
        {
            using var echo = new TcpEchoServer();

            var proxy = StartProxy(new[] { Socks5AuthMethod.NoAuth }, null, out var proxyPort);

            try
            {
                var client = new Socks5Client(new Socks5ClientOptions
                {
                    ProxyHost = "127.0.0.1",
                    ProxyPort = proxyPort
                });

                using (client)
                {
                    var tunnel = client.Connect("127.0.0.1", echo.Port);

                    var message = Encoding.UTF8.GetBytes("hello socks5");
                    tunnel.Write(message, 0, message.Length);

                    var received = ReadExact(tunnel, message.Length);

                    Assert(Encoding.UTF8.GetString(received) == "hello socks5", "回显内容不一致");
                }
            }
            finally
            {
                proxy.Stop();
            }
        }

        /// <summary>
        /// 用户名/密码：凭据正确，CONNECT 成功
        /// </summary>
        private static void TestUserPassSuccess()
        {
            using var echo = new TcpEchoServer();

            var validator = new DefaultUserValidator(new System.Collections.Generic.Dictionary<string, string>
            {
                ["user"] = "pass"
            });

            var proxy = StartProxy(new[] { Socks5AuthMethod.UserPass }, validator, out var proxyPort);

            try
            {
                var client = new Socks5Client(new Socks5ClientOptions
                {
                    ProxyHost = "127.0.0.1",
                    ProxyPort = proxyPort,
                    UserName = "user",
                    Password = "pass"
                });

                using (client)
                {
                    var tunnel = client.Connect("127.0.0.1", echo.Port);

                    var message = Encoding.UTF8.GetBytes("auth-ok");
                    tunnel.Write(message, 0, message.Length);

                    var received = ReadExact(tunnel, message.Length);

                    Assert(Encoding.UTF8.GetString(received) == "auth-ok", "回显内容不一致");
                }
            }
            finally
            {
                proxy.Stop();
            }
        }

        /// <summary>
        /// 用户名/密码：凭据错误，认证应被拒绝
        /// </summary>
        private static void TestUserPassFailure()
        {
            var validator = new DefaultUserValidator(new System.Collections.Generic.Dictionary<string, string>
            {
                ["user"] = "pass"
            });

            var proxy = StartProxy(new[] { Socks5AuthMethod.UserPass }, validator, out var proxyPort);

            try
            {
                var client = new Socks5Client(new Socks5ClientOptions
                {
                    ProxyHost = "127.0.0.1",
                    ProxyPort = proxyPort,
                    UserName = "user",
                    Password = "wrong"
                });

                using (client)
                {
                    bool threw = false;

                    try
                    {
                        client.Connect("127.0.0.1", 9);
                    }
                    catch (InvalidOperationException)
                    {
                        threw = true;
                    }

                    Assert(threw, "错误的凭据应该被拒绝");
                }
            }
            finally
            {
                proxy.Stop();
            }
        }

        /// <summary>
        /// BIND：两阶段握手，远端连入后客户端与对端互通
        /// </summary>
        private static void TestBind()
        {
            var proxy = StartProxy(new[] { Socks5AuthMethod.NoAuth }, null, out var proxyPort);

            try
            {
                var client = new Socks5Client(new Socks5ClientOptions
                {
                    ProxyHost = "127.0.0.1",
                    ProxyPort = proxyPort
                });

                using (client)
                {
                    var bind = client.Bind("0.0.0.0", 0);

                    Assert(bind.FirstReply.ReplyCode == Socks5ReplyCode.Succeeded, "BIND 第一阶段失败");

                    // 模拟远端连入监听地址
                    var remote = new TcpClient();
                    remote.Connect(IPAddress.Parse(bind.FirstReply.Host), bind.FirstReply.Port);

                    var secondReply = bind.WaitForBindComplete();

                    Assert(secondReply.ReplyCode == Socks5ReplyCode.Succeeded, "BIND 第二阶段失败");

                    var remoteStream = remote.GetStream();

                    var message = Encoding.UTF8.GetBytes("bind-data");
                    bind.ControlStream.Write(message, 0, message.Length);

                    var received = ReadExact(remoteStream, message.Length);

                    Assert(Encoding.UTF8.GetString(received) == "bind-data", "BIND 隧道数据不一致");

                    remote.Close();
                }
            }
            finally
            {
                proxy.Stop();
            }
        }

        /// <summary>
        /// UDP ASSOCIATE：经代理发送 UDP 数据报到本地 UDP 回显服务
        /// </summary>
        private static void TestUdpAssociate()
        {
            using var echo = new UdpEchoServer();

            var proxy = StartProxy(new[] { Socks5AuthMethod.NoAuth }, null, out var proxyPort);

            try
            {
                var client = new Socks5Client(new Socks5ClientOptions
                {
                    ProxyHost = "127.0.0.1",
                    ProxyPort = proxyPort
                });

                using (client)
                using (var udp = client.UdpAssociate())
                {
                    udp.SendTo("127.0.0.1", echo.Port, "udp-ping");

                    if (!udp.TryReceive(out _, out _, out var data, 5000))
                    {
                        throw new InvalidOperationException("未收到 UDP 回显");
                    }

                    Assert(Encoding.UTF8.GetString(data) == "udp-ping", "UDP 回显内容不一致");
                }
            }
            finally
            {
                proxy.Stop();
            }
        }

        /// <summary>
        /// 回归：会话不存在时 IServerSocket.Disconnect 不应抛异常。
        /// 背景——SAEA.StreamServerSocket.Disconnect 未对 ChannelManager.Get 的 null 结果判空，
        /// 当会话已被 Stop()/Clear() 回收或重复断开时，会在 channel.ClientSocket 处抛
        /// NullReferenceException（调试器中可观察到 channel 为 null）。
        /// </summary>
        private static void TestDisconnectMissingSession()
        {
            var port = GetFreeTcpPort();

            var option = SocketOptionBuilder.Instance
                .UseStream()
                .SetIP("127.0.0.1")
                .SetPort(port)
                .Build();

            var server = SocketFactory.CreateServerSocket(option);

            server.Start();

            try
            {
                // 不存在的会话：修复前抛 NullReferenceException
                server.Disconnect("127.0.0.1:1");

                // 重复断开同一会话同样不应抛异常
                server.Disconnect("127.0.0.1:1");
            }
            finally
            {
                server.Stop();
            }
        }

        /// <summary>
        /// 创建一个连到本地代理的 SOCKS5 客户端
        /// </summary>
        private static Socks5Client CreateClient(int proxyPort, string userName = null, string password = null)
        {
            return new Socks5Client(new Socks5ClientOptions
            {
                ProxyHost = "127.0.0.1",
                ProxyPort = proxyPort,
                UserName = userName,
                Password = password
            });
        }

        /// <summary>
        /// 生成确定性的测试载荷（非平凡字节序列，便于发现内容错位或丢包）
        /// </summary>
        private static byte[] MakePayload(int size)
        {
            var buffer = new byte[size];

            for (var i = 0; i < size; i++)
            {
                buffer[i] = (byte)((i * 31 + 7) & 0xFF);
            }

            return buffer;
        }

        /// <summary>
        /// 字节级比较
        /// </summary>
        private static bool ByteEquals(byte[] a, byte[] b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            return a.AsSpan().SequenceEqual(b);
        }

        /// <summary>
        /// 经隧道发送整个 payload 并读回等长数据，返回实收字节与耗时。
        /// 读操作放在独立任务中与写并发——大包场景下若"先写满再读"，会与对端 / 中继互相阻塞导致死锁。
        /// </summary>
        private static (byte[] received, double ms) TransferAndVerify(Stream tunnel, byte[] payload, int timeoutMs = 60000)
        {
            var received = new byte[payload.Length];

            if (tunnel.CanTimeout)
            {
                tunnel.ReadTimeout = timeoutMs;
                tunnel.WriteTimeout = timeoutMs;
            }

            var sw = Stopwatch.StartNew();

            var readTask = Task.Run(() =>
            {
                var offset = 0;

                while (offset < received.Length)
                {
                    var n = tunnel.Read(received, offset, received.Length - offset);

                    if (n <= 0)
                    {
                        throw new EndOfStreamException($"读取数据不足：{offset}/{received.Length}");
                    }

                    offset += n;
                }
            });

            tunnel.Write(payload, 0, payload.Length);

            if (Task.WaitAny(new[] { readTask }, timeoutMs) < 0)
            {
                throw new TimeoutException($"数据传输超时（>{timeoutMs} ms）");
            }

            readTask.GetAwaiter().GetResult();

            sw.Stop();

            return (received, sw.Elapsed.TotalMilliseconds);
        }

        /// <summary>
        /// 输出性能指标：吞吐量（MB/s）与可选的操作速率（次/秒）
        /// </summary>
        private static void Perf(string label, long bytes, double elapsedMs, int ops = 0)
        {
            var mbps = elapsedMs > 0.0001 ? bytes / 1024.0 / 1024.0 / (elapsedMs / 1000.0) : 0;

            var line = $"       [PERF] {label}：{bytes:N0} 字节 / {elapsedMs:F1} ms，{mbps:F2} MB/s";

            if (ops > 0)
            {
                var rate = elapsedMs > 0.0001 ? ops / (elapsedMs / 1000.0) : 0;

                line += $"，{rate:F0} 次/秒";
            }

            Console.WriteLine(line);
        }

        /// <summary>
        /// 数据完整性：在一条 CONNECT 隧道上依次发送多种尺寸的载荷，
        /// 覆盖 1B、小包、恰好等于中继缓冲区（默认 64KB）、跨边界 ±1 以及多缓冲区，逐段校验回显一致。
        /// </summary>
        private static void TestDataTransferSizes()
        {
            using var echo = new TcpEchoServer();

            var proxy = StartProxy(new[] { Socks5AuthMethod.NoAuth }, null, out var proxyPort);

            try
            {
                using var client = CreateClient(proxyPort);

                var tunnel = client.Connect("127.0.0.1", echo.Port);

                var sizes = new[] { 1, 2, 1024, 65535, 65536, 65537, 131072, 200000 };

                long total = 0;

                var sw = Stopwatch.StartNew();

                foreach (var size in sizes)
                {
                    var payload = MakePayload(size);

                    var (received, _) = TransferAndVerify(tunnel, payload);

                    Assert(ByteEquals(payload, received), $"尺寸 {size} 字节的回显内容不一致");

                    total += size;
                }

                sw.Stop();

                Perf($"多尺寸回显（{string.Join("/", sizes)}）", total, sw.Elapsed.TotalMilliseconds);
            }
            finally
            {
                proxy.Stop();
            }
        }

        /// <summary>
        /// 数据传输：8MB 载荷经代理回显，校验字节级一致性并报告吞吐
        /// </summary>
        private static void TestLargeDataTransfer()
        {
            using var echo = new TcpEchoServer();

            var proxy = StartProxy(new[] { Socks5AuthMethod.NoAuth }, null, out var proxyPort);

            try
            {
                using var client = CreateClient(proxyPort);

                var tunnel = client.Connect("127.0.0.1", echo.Port);

                const int size = 8 * 1024 * 1024;

                var payload = MakePayload(size);

                var (received, ms) = TransferAndVerify(tunnel, payload, 120000);

                Assert(ByteEquals(payload, received), "8MB 回显内容不一致");

                // 回显链路为 客户端→代理→回显服务→代理→客户端，实际搬运 2×size
                Perf("CONNECT 8MB 回显（双向搬运）", size * 2L, ms);
            }
            finally
            {
                proxy.Stop();
            }
        }

        /// <summary>
        /// 性能：单连接高频往返，测量平均往返延迟与每秒往返次数
        /// </summary>
        private static void TestHighFrequencyRoundTrip()
        {
            using var echo = new TcpEchoServer();

            var proxy = StartProxy(new[] { Socks5AuthMethod.NoAuth }, null, out var proxyPort);

            try
            {
                using var client = CreateClient(proxyPort);

                var tunnel = client.Connect("127.0.0.1", echo.Port);

                const int rounds = 1000;

                var payload = Encoding.UTF8.GetBytes("ping-pong-0123456789");

                var sw = Stopwatch.StartNew();

                for (var i = 0; i < rounds; i++)
                {
                    tunnel.Write(payload, 0, payload.Length);

                    var received = ReadExact(tunnel, payload.Length, 10000);

                    if (!ByteEquals(payload, received))
                    {
                        throw new InvalidOperationException($"第 {i} 次往返内容不一致");
                    }
                }

                sw.Stop();

                var avgUs = sw.Elapsed.TotalMilliseconds * 1000.0 / rounds;

                // 回环环境单次往返应在毫秒级以内；阈值放宽，仅用于捕捉 Nagle / 缓冲导致的病态停顿
                Assert(avgUs < 20000, $"单次往返平均延迟异常：{avgUs:F0} µs");

                Perf($"CONNECT 往返 {rounds} 次（{payload.Length}B/次）", (long)rounds * payload.Length * 2, sw.Elapsed.TotalMilliseconds, rounds);

                Console.WriteLine($"       [PERF] 平均往返延迟：{avgUs:F1} µs/次");
            }
            finally
            {
                proxy.Stop();
            }
        }

        /// <summary>
        /// 性能：多客户端并发经代理建连并传输，测量聚合吞吐
        /// </summary>
        private static void TestConcurrentConnections()
        {
            using var echo = new TcpEchoServer();

            var proxy = StartProxy(new[] { Socks5AuthMethod.NoAuth }, null, out var proxyPort);

            try
            {
                const int connections = 64;

                const int size = 256 * 1024;

                var payload = MakePayload(size);

                var sw = Stopwatch.StartNew();

                var tasks = new Task[connections];

                for (var i = 0; i < connections; i++)
                {
                    tasks[i] = Task.Run(() =>
                    {
                        using var c = CreateClient(proxyPort);

                        var tunnel = c.Connect("127.0.0.1", echo.Port);

                        var (received, _) = TransferAndVerify(tunnel, payload, 30000);

                        Assert(ByteEquals(payload, received), "并发连接回显内容不一致");
                    });
                }

                bool completed;

                try
                {
                    // 健康值在百毫秒级；30s 预算既留足慢机器余量，
                    // 又能捕获"每会话占用线程池线程导致并发崩塌"这类回归（曾退化到 45s+）
                    completed = Task.WaitAll(tasks, 30000);
                }
                catch (AggregateException)
                {
                    // 统一在下方逐个抛出具体内部异常，避免只看到 "One or more errors occurred"
                    completed = true;
                }

                foreach (var task in tasks)
                {
                    if (task.IsFaulted)
                    {
                        throw task.Exception.InnerException ?? task.Exception;
                    }
                }

                Assert(completed, "并发连接未在超时内完成");

                sw.Stop();

                Perf($"CONNECT {connections} 并发连接 × {size / 1024}KB", (long)connections * size * 2, sw.Elapsed.TotalMilliseconds, connections);
            }
            finally
            {
                proxy.Stop();
            }
        }

        /// <summary>
        /// 性能：UDP ASSOCIATE 下连续收发数据报，统计回包率与吞吐
        /// </summary>
        private static void TestUdpThroughput()
        {
            using var echo = new UdpEchoServer();

            var proxy = StartProxy(new[] { Socks5AuthMethod.NoAuth }, null, out var proxyPort);

            try
            {
                var client = CreateClient(proxyPort);

                using (client)
                using (var udp = client.UdpAssociate())
                {
                    const int count = 1000;

                    const int size = 1024;

                    var payload = MakePayload(size);

                    var ok = 0;

                    var sw = Stopwatch.StartNew();

                    for (var i = 0; i < count; i++)
                    {
                        // 首字节写入序号，用于核对返回的数据报确属本次请求
                        payload[0] = (byte)(i & 0xFF);

                        udp.SendTo("127.0.0.1", echo.Port, payload);

                        if (udp.TryReceive(out _, out _, out var data, 500)
                            && data != null
                            && data.Length == size
                            && data[0] == (byte)(i & 0xFF))
                        {
                            ok++;
                        }
                    }

                    sw.Stop();

                    // 回环允许极少量丢包，但不应大面积丢失
                    Assert(ok >= count * 0.9, $"UDP 回包丢失过多：{ok}/{count}");

                    Perf($"UDP ASSOCIATE {count} 数据报 × {size}B", (long)count * size * 2, sw.Elapsed.TotalMilliseconds, count);
                }
            }
            finally
            {
                proxy.Stop();
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }

    /// <summary>
    /// 本地 TCP 回显服务
    /// </summary>
    internal class TcpEchoServer : IDisposable
    {
        private readonly TcpListener _listener;

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public int Port { get; }

        public TcpEchoServer()
        {
            Port = Program.GetFreeTcpPort();
            _listener = new TcpListener(IPAddress.Loopback, Port);
            _listener.Start();
            Task.Run(Loop);
        }

        private void Loop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    Task.Run(() => Echo(client));
                }
                catch
                {
                    break;
                }
            }
        }

        private void Echo(TcpClient client)
        {
            try
            {
                var stream = client.GetStream();
                var buffer = new byte[64 * 1024];

                while (true)
                {
                    var n = stream.Read(buffer, 0, buffer.Length);

                    if (n <= 0)
                    {
                        break;
                    }

                    stream.Write(buffer, 0, n);
                }
            }
            catch
            {
                // 连接断开
            }
            finally
            {
                try { client.Close(); } catch { }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();

            try { _listener.Stop(); } catch { }
        }
    }

    /// <summary>
    /// 本地 UDP 回显服务
    /// </summary>
    internal class UdpEchoServer : IDisposable
    {
        private readonly UdpClient _client;

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public int Port { get; }

        public UdpEchoServer()
        {
            Port = Program.GetFreeUdpPort();
            _client = new UdpClient(Port);
            Task.Run(Loop);
        }

        private void Loop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
                    var data = _client.Receive(ref endPoint);

                    if (data != null && data.Length > 0)
                    {
                        _client.Send(data, data.Length, (IPEndPoint)endPoint);
                    }
                }
                catch
                {
                    break;
                }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();

            try { _client.Close(); } catch { }
        }
    }
}
