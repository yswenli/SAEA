using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Sockets;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Model;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// TLS 硬化测试：大包、背压、并发连接与事件模式大帧。
    /// </summary>
    public static class TlsStressTest
    {
        private const int ReadBufferSize = 8192;

        /// <summary>运行全部硬化场景。</summary>
        public static async Task RunAsync()
        {
            TestHarness.Section("TlsStressTest");

            await LargeTransferWithBackpressureAsync();
            await ConcurrentConnectionsAsync();
            await EventModeLargeFrameAsync();

            TestHarness.WriteSummary("TlsStressTest");
        }

        /// <summary>
        /// 4MB 单向回显：服务端延迟读取，让客户端写路径触发背压，
        /// 恢复读取后两端数据必须完整无损。
        /// </summary>
        private static async Task LargeTransferWithBackpressureAsync()
        {
            const int size = 4 * 1024 * 1024;
            var payload = new byte[size];
            new Random(1001).NextBytes(payload);

            using (var certificate = TlsTest.CreateCertificate("CN=localhost", false))
            {
                var port = TestHarness.GetFreeTcpPort();
                var serverOption = BuildServerOption(port, certificate);
                var server = new SaeaServerSocket(serverOption);

                server.OnAccepted += obj =>
                {
                    var channel = obj as ChannelInfo;
                    if (channel == null) return;

                    Task.Run(async () =>
                    {
                        try
                        {
                            // 延迟读取：客户端会在 Pipe/Socket 高水位处被暂停。
                            await Task.Delay(250).ConfigureAwait(false);
                            await EchoExactlyAsync(channel.Stream, size).ConfigureAwait(false);
                        }
                        catch
                        {
                        }
                    });
                };

                server.Start();

                var client = new SaeaClientSocket(BuildClientOption(port));
                try
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                    {
                        await client.ConnectAsync(cts.Token).ConfigureAwait(false);
                    }

                    var stream = client.GetStream();
                    var readTask = ReadExactlyAsync(stream, size);
                    var writeTask = stream.WriteAsync(payload, 0, payload.Length);

                    await Task.WhenAll(readTask, writeTask).ConfigureAwait(false);
                    var response = readTask.Result;

                    TestHarness.Expect(response != null && BytesEqual(response, payload),
                        "stress: 4MB TLS transfer survives backpressure", $"{response?.Length ?? 0} bytes");
                }
                finally
                {
                    try { client.Dispose(); } catch { }
                    try { server.Stop(); } catch { }
                    try { server.Dispose(); } catch { }
                }
            }
        }

        /// <summary>
        /// 20 条并发 TLS 连接，每条回显 128KB 唯一数据。
        /// </summary>
        private static async Task ConcurrentConnectionsAsync()
        {
            const int connectionCount = 20;
            const int size = 128 * 1024;

            using (var certificate = TlsTest.CreateCertificate("CN=localhost", false))
            {
                var port = TestHarness.GetFreeTcpPort();
                var server = new SaeaServerSocket(BuildServerOption(port, certificate));

                server.OnAccepted += obj =>
                {
                    var channel = obj as ChannelInfo;
                    if (channel == null) return;

                    Task.Run(async () =>
                    {
                        try
                        {
                            await EchoExactlyAsync(channel.Stream, size).ConfigureAwait(false);
                        }
                        catch
                        {
                        }
                    });
                };

                server.Start();

                var tasks = new List<Task<bool>>(connectionCount);
                try
                {
                    for (var i = 0; i < connectionCount; i++)
                    {
                        tasks.Add(RunEchoClientAsync(port, i, size));
                    }

                    var results = await Task.WhenAll(tasks).ConfigureAwait(false);
                    var passed = 0;
                    foreach (var result in results)
                    {
                        if (result) passed++;
                    }

                    TestHarness.Expect(passed == connectionCount,
                        "stress: concurrent TLS echoes are byte-exact", $"{passed}/{connectionCount}");
                }
                finally
                {
                    try { server.Stop(); } catch { }
                    try { server.Dispose(); } catch { }
                }
            }
        }

        /// <summary>
        /// 事件模式：256KB 数据经 TLS 解密后按 span 分片交付，拼接后必须完整。
        /// </summary>
        private static async Task EventModeLargeFrameAsync()
        {
            const int size = 256 * 1024;
            var payload = new byte[size];
            new Random(2024).NextBytes(payload);

            using (var certificate = TlsTest.CreateCertificate("CN=localhost", false))
            {
                var port = TestHarness.GetFreeTcpPort();

                var serverOption = SocketOptionBuilder.Instance
                    .UseIocp()
                    .SetIP("127.0.0.1")
                    .SetPort(port)
                    .SetReadBufferSize(ReadBufferSize)
                    .WithSsl(o =>
                    {
                        o.ServerCertificate = certificate;
                        o.Protocols = SslProtocols.Tls12;
                    })
                    .Build();

                var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                var chunks = new ConcurrentQueue<byte[]>();
                var total = 0;

                var server = new SaeaServerSocket(serverOption);
                server.OnServerReceiveSpan += (token, span) =>
                {
                    chunks.Enqueue(span.ToArray());
                    var current = Interlocked.Add(ref total, span.Length);
                    if (current < size) return;

                    var buffer = new byte[current];
                    var offset = 0;
                    foreach (var chunk in chunks)
                    {
                        Buffer.BlockCopy(chunk, 0, buffer, offset, chunk.Length);
                        offset += chunk.Length;
                    }
                    received.TrySetResult(buffer);
                };

                server.Start();

                var client = new SaeaClientSocket(BuildClientOption(port));
                try
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                    {
                        await client.ConnectAsync(cts.Token).ConfigureAwait(false);
                    }

                    client.Send(payload);

                    var response = await WaitAsync(received.Task, 15000).ConfigureAwait(false);
                    TestHarness.Expect(response != null && BytesEqual(response, payload),
                        "stress: event-mode TLS delivers complete 256KB frame", $"{response?.Length ?? 0} bytes");
                }
                finally
                {
                    try { client.Dispose(); } catch { }
                    try { server.Stop(); } catch { }
                    try { server.Dispose(); } catch { }
                }
            }
        }

        private static ISocketOption BuildServerOption(int port, System.Security.Cryptography.X509Certificates.X509Certificate2 certificate)
        {
            return SocketOptionBuilder.Instance
                .UseStream()
                .SetIP("127.0.0.1")
                .SetPort(port)
                .SetReadBufferSize(ReadBufferSize)
                .WithSsl(o =>
                {
                    o.ServerCertificate = certificate;
                    o.Protocols = SslProtocols.Tls12;
                })
                .Build();
        }

        private static ISocketOption BuildClientOption(int port)
        {
            return SocketOptionBuilder.Instance
                .UseStream()
                .SetIP("127.0.0.1")
                .SetPort(port)
                .SetReadBufferSize(ReadBufferSize)
                .WithSsl(o =>
                {
                    o.TargetHost = "localhost";
                    o.AllowInvalidCertificates = true;
                    o.Protocols = SslProtocols.Tls12;
                })
                .Build();
        }

        private static async Task<bool> RunEchoClientAsync(int port, int index, int size)
        {
            var payload = new byte[size];
            var random = new Random(3000 + index);
            random.NextBytes(payload);

            var client = new SaeaClientSocket(BuildClientOption(port));
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                {
                    await client.ConnectAsync(cts.Token).ConfigureAwait(false);
                }

                var stream = client.GetStream();
                var readTask = ReadExactlyAsync(stream, size);
                await stream.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
                var response = await readTask.ConfigureAwait(false);

                return response != null && BytesEqual(response, payload);
            }
            catch
            {
                return false;
            }
            finally
            {
                try { client.Dispose(); } catch { }
            }
        }

        private static async Task EchoExactlyAsync(System.IO.Stream stream, int size)
        {
            var buffer = new byte[ReadBufferSize];
            var remaining = size;

            while (remaining > 0)
            {
                var toRead = Math.Min(buffer.Length, remaining);
                var read = await stream.ReadAsync(buffer, 0, toRead).ConfigureAwait(false);
                if (read <= 0) return;

                await stream.WriteAsync(buffer, 0, read).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
                remaining -= read;
            }
        }

        private static async Task<byte[]> ReadExactlyAsync(System.IO.Stream stream, int size)
        {
            var buffer = new byte[size];
            var total = 0;

            while (total < size)
            {
                var read = await stream.ReadAsync(buffer, total, size - total).ConfigureAwait(false);
                if (read <= 0) break;
                total += read;
            }

            if (total != size) return null;
            return buffer;
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (var i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i]) return false;
            }
            return true;
        }

        private static async Task<T> WaitAsync<T>(Task<T> task, int timeoutMs)
        {
            var completed = await Task.WhenAny(task, Task.Delay(timeoutMs)).ConfigureAwait(false);
            if (completed != task) return default;
            return await task.ConfigureAwait(false);
        }
    }
}