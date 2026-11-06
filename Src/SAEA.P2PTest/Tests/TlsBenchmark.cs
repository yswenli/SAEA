using System;
using System.Diagnostics;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Sockets;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Model;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// 明文 vs TLS 的流式回显基线。仅度量端到端往返，用于版本间对比，不作为硬性阈值。
    /// </summary>
    public static class TlsBenchmark
    {
        private const int PayloadSize = 4096;
        private const int Warmup = 200;
        private const int Iterations = 2000;

        /// <summary>运行基准。</summary>
        /// <returns>异步任务，完成后该组场景与断言已全部执行</returns>
        public static async Task RunAsync()
        {
            TestHarness.Section("TLS performance baseline");

            var plain = await MeasureAsync(useTls: false).ConfigureAwait(false);
            var tls = await MeasureAsync(useTls: true).ConfigureAwait(false);

            TestHarness.Expect(plain.opsPerSecond > 0, "bench: plaintext echo completed", Describe(plain));
            TestHarness.Expect(tls.opsPerSecond > 0, "bench: TLS echo completed", Describe(tls));

            Console.WriteLine($"plaintext : {Describe(plain)}");
            Console.WriteLine($"TLS       : {Describe(tls)}");
            Console.WriteLine($"TLS/plain : {tls.opsPerSecond / plain.opsPerSecond:F3}x (ops/sec)");

            TestHarness.WriteSummary("TlsBenchmark");
        }

        private static async Task<BenchmarkResult> MeasureAsync(bool useTls)
        {
            using (var certificate = TlsTest.CreateCertificate("CN=localhost", false))
            {
                var port = TestHarness.GetFreeTcpPort();
                var server = new SaeaServerSocket(BuildServerOption(port, certificate, useTls));

                server.OnAccepted += obj =>
                {
                    var channel = obj as ChannelInfo;
                    if (channel == null) return;

                    Task.Run(async () =>
                    {
                        try
                        {
                            var buffer = new byte[PayloadSize];
                            while (true)
                            {
                                var total = 0;
                                while (total < buffer.Length)
                                {
                                    var read = await channel.Stream.ReadAsync(buffer, total, buffer.Length - total).ConfigureAwait(false);
                                    if (read <= 0) return;
                                    total += read;
                                }

                                await channel.Stream.WriteAsync(buffer, 0, total).ConfigureAwait(false);
                                await channel.Stream.FlushAsync().ConfigureAwait(false);
                            }
                        }
                        catch
                        {
                        }
                    });
                };

                server.Start();

                var client = new SaeaClientSocket(BuildClientOption(port, useTls));
                try
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                    {
                        await client.ConnectAsync(cts.Token).ConfigureAwait(false);
                    }

                    var stream = client.GetStream();
                    var payload = new byte[PayloadSize];
                    new Random(42).NextBytes(payload);
                    var response = new byte[PayloadSize];

                    for (var i = 0; i < Warmup; i++)
                    {
                        await RoundTripAsync(stream, payload, response).ConfigureAwait(false);
                    }

                    var sw = Stopwatch.StartNew();
                    for (var i = 0; i < Iterations; i++)
                    {
                        await RoundTripAsync(stream, payload, response).ConfigureAwait(false);
                    }
                    sw.Stop();

                    var ops = Iterations / sw.Elapsed.TotalSeconds;
                    return new BenchmarkResult(ops, Iterations * PayloadSize / sw.Elapsed.TotalSeconds);
                }
                finally
                {
                    try { client.Dispose(); } catch { }
                    try { server.Stop(); } catch { }
                    try { server.Dispose(); } catch { }
                }
            }
        }

        private static async Task RoundTripAsync(System.IO.Stream stream, byte[] payload, byte[] response)
        {
            await stream.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);

            var total = 0;
            while (total < response.Length)
            {
                var read = await stream.ReadAsync(response, total, response.Length - total).ConfigureAwait(false);
                if (read <= 0) throw new InvalidOperationException("连接在回显完成前关闭。");
                total += read;
            }
        }

        private static ISocketOption BuildServerOption(int port, System.Security.Cryptography.X509Certificates.X509Certificate2 certificate, bool useTls)
        {
            var builder = SocketOptionBuilder.Instance
                .UseStream()
                .SetIP("127.0.0.1")
                .SetPort(port)
                .SetReadBufferSize(8192);

            if (useTls)
            {
                builder.WithSsl(o =>
                {
                    o.ServerCertificate = certificate;
                    o.Protocols = SslProtocols.Tls12;
                });
            }

            return builder.Build();
        }

        private static ISocketOption BuildClientOption(int port, bool useTls)
        {
            var builder = SocketOptionBuilder.Instance
                .UseStream()
                .SetIP("127.0.0.1")
                .SetPort(port)
                .SetReadBufferSize(8192);

            if (useTls)
            {
                builder.WithSsl(o =>
                {
                    o.TargetHost = "localhost";
                    o.AllowInvalidCertificates = true;
                    o.Protocols = SslProtocols.Tls12;
                });
            }

            return builder.Build();
        }

        private static string Describe(BenchmarkResult result)
        {
            return $"{result.opsPerSecond:F0} ops/sec, {result.bytesPerSecond / (1024 * 1024):F2} MB/s";
        }

        private readonly struct BenchmarkResult
        {
            public BenchmarkResult(double opsPerSecond, double bytesPerSecond)
            {
                this.opsPerSecond = opsPerSecond;
                this.bytesPerSecond = bytesPerSecond;
            }

            public readonly double opsPerSecond;
            public readonly double bytesPerSecond;
        }
    }
}