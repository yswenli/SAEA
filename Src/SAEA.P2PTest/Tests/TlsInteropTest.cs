using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Sockets;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Model;
using SAEA.Sockets.Ssl;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// SAEA 内置 TLS 与 .NET <see cref="SslStream"/> 的互通测试：双向握手、SNI 证书选择与校验失败语义。
    /// </summary>
    public static class TlsInteropTest
    {
        /// <summary>运行全部互通场景。</summary>
        public static async Task RunAsync()
        {
            TestHarness.Section("TlsInteropTest");

            await SslStreamClientAsync();
            await SslStreamServerAsync();
            await SniCertificateSelectionAsync();
            await InvalidCertificateIsRejectedAsync();

            TestHarness.WriteSummary("TlsInteropTest");
        }

        /// <summary>
        /// .NET <see cref="SslStream"/> 客户端 → SAEA TLS 服务端。
        /// </summary>
        private static async Task SslStreamClientAsync()
        {
            using (var serverCertificate = TlsTest.CreateCertificate("CN=localhost", false))
            {
                var port = TestHarness.GetFreeTcpPort();
                var payload = Encoding.UTF8.GetBytes("sslstream-to-saea");

                var serverOption = SocketOptionBuilder.Instance
                    .UseStream()
                    .SetIP("127.0.0.1")
                    .SetPort(port)
                    .SetReadBufferSize(8192)
                    .WithSsl(o =>
                    {
                        o.ServerCertificate = serverCertificate;
                        o.Protocols = SslProtocols.Tls12;
                    })
                    .Build();

                var server = new SaeaServerSocket(serverOption);
                var accepted = new TaskCompletionSource<ChannelInfo>(TaskCreationOptions.RunContinuationsAsynchronously);

                server.OnAccepted += obj =>
                {
                    var channel = obj as ChannelInfo;
                    if (channel == null) return;

                    accepted.TrySetResult(channel);

                    Task.Run(async () =>
                    {
                        try
                        {
                            var buffer = new byte[payload.Length];
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
                        catch
                        {
                        }
                    });
                };

                server.Start();

                try
                {
                    using (var tcp = new TcpClient())
                    {
                        await tcp.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);

                        using (var ssl = new SslStream(tcp.GetStream(), false, (sender, certificate, chain, errors) => true))
                        {
                            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                            {
                                TargetHost = "localhost",
                                EnabledSslProtocols = SslProtocols.Tls12
                            }).ConfigureAwait(false);

                            await ssl.WriteAsync(payload).ConfigureAwait(false);
                            await ssl.FlushAsync().ConfigureAwait(false);

                            var response = new byte[payload.Length];
                            var total = 0;
                            while (total < response.Length)
                            {
                                var read = await ssl.ReadAsync(response, total, response.Length - total).ConfigureAwait(false);
                                if (read <= 0) break;
                                total += read;
                            }

                            TestHarness.Expect(total == payload.Length && BytesEqual(response, payload),
                                "interop: SslStream client receives SAEA TLS echo", $"{total} bytes");
                        }
                    }

                    var channel = await WaitAsync(accepted.Task, 5000).ConfigureAwait(false);
                    TestHarness.Expect(channel?.Tls?.IsAuthenticated == true,
                        "interop: SAEA server completes SslStream handshake");
                    TestHarness.Expect(channel?.Tls?.NegotiatedProtocol?.Contains("1.2") == true,
                        "interop: TLS 1.2 negotiated with SslStream", channel?.Tls?.NegotiatedProtocol);
                }
                finally
                {
                    try { server.Stop(); } catch { }
                    try { server.Dispose(); } catch { }
                }
            }
        }

        /// <summary>
        /// SAEA TLS 客户端 → .NET <see cref="SslStream"/> 服务端。
        /// </summary>
        private static async Task SslStreamServerAsync()
        {
            using (var serverCertificate = TlsTest.CreateCertificate("CN=localhost", false))
            {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                var payload = Encoding.UTF8.GetBytes("saea-to-sslstream");

                var serverTask = Task.Run(async () =>
                {
                    using (var tcp = await listener.AcceptTcpClientAsync().ConfigureAwait(false))
                    using (var ssl = new SslStream(tcp.GetStream(), false))
                    {
                        await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                        {
                            ServerCertificate = serverCertificate,
                            EnabledSslProtocols = SslProtocols.Tls12
                        }).ConfigureAwait(false);

                        var buffer = new byte[payload.Length];
                        var total = 0;
                        while (total < buffer.Length)
                        {
                            var read = await ssl.ReadAsync(buffer, total, buffer.Length - total).ConfigureAwait(false);
                            if (read <= 0) return false;
                            total += read;
                        }

                        await ssl.WriteAsync(buffer, 0, total).ConfigureAwait(false);
                        await ssl.FlushAsync().ConfigureAwait(false);
                        return BytesEqual(buffer, payload);
                    }
                });

                var clientOption = SocketOptionBuilder.Instance
                    .UseStream()
                    .SetIP("127.0.0.1")
                    .SetPort(port)
                    .SetReadBufferSize(8192)
                    .WithSsl(o =>
                    {
                        o.TargetHost = "localhost";
                        o.AllowInvalidCertificates = true;
                        o.Protocols = SslProtocols.Tls12;
                    })
                    .Build();

                var client = new SaeaClientSocket(clientOption);
                try
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                    {
                        await client.ConnectAsync(cts.Token).ConfigureAwait(false);
                    }

                    var stream = client.GetStream();
                    await stream.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);

                    var response = new byte[payload.Length];
                    var total = 0;
                    while (total < response.Length)
                    {
                        var read = await stream.ReadAsync(response, total, response.Length - total).ConfigureAwait(false);
                        if (read <= 0) break;
                        total += read;
                    }

                    TestHarness.Expect(total == payload.Length && BytesEqual(response, payload),
                        "interop: SAEA TLS client receives SslStream echo", $"{total} bytes");
                    TestHarness.Expect(client.IsAuthenticated, "interop: SAEA client authenticated against SslStream");
                    TestHarness.Expect(await WaitAsync(serverTask, 5000).ConfigureAwait(false),
                        "interop: SslStream server received SAEA TLS plaintext");
                }
                finally
                {
                    try { client.Dispose(); } catch { }
                    try { listener.Stop(); } catch { }
                }
            }
        }

        /// <summary>
        /// SslStream 客户端分别以 <c>a.local</c> / <c>b.local</c> 连接，验证服务端 SNI 选择器返回对应证书。
        /// </summary>
        private static async Task SniCertificateSelectionAsync()
        {
            using (var certificateA = TlsTest.CreateCertificate("CN=a.local", false, new[] { "a.local" }))
            using (var certificateB = TlsTest.CreateCertificate("CN=b.local", false, new[] { "b.local" }))
            {
                var port = TestHarness.GetFreeTcpPort();
                var sniSeen = new System.Collections.Concurrent.ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                var serverOption = SocketOptionBuilder.Instance
                    .UseStream()
                    .SetIP("127.0.0.1")
                    .SetPort(port)
                    .SetReadBufferSize(8192)
                    .WithSsl(o =>
                    {
                        o.ServerCertificate = certificateA;
                        o.Protocols = SslProtocols.Tls12;
                        o.ServerCertificateSelector = sni =>
                        {
                            sniSeen[sni ?? string.Empty] = sni;
                            if (string.Equals(sni, "a.local", StringComparison.OrdinalIgnoreCase)) return certificateA;
                            if (string.Equals(sni, "b.local", StringComparison.OrdinalIgnoreCase)) return certificateB;
                            return null;
                        };
                    })
                    .Build();

                var server = new SaeaServerSocket(serverOption);
                server.Start();

                try
                {
                    foreach (var host in new[] { "a.local", "b.local" })
                    {
                        using (var tcp = new TcpClient())
                        {
                            await tcp.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);

                            using (var ssl = new SslStream(tcp.GetStream(), false, (sender, certificate, chain, errors) => true))
                            {
                                await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                                {
                                    TargetHost = host,
                                    EnabledSslProtocols = SslProtocols.Tls12
                                }).ConfigureAwait(false);

                                var remote = ssl.RemoteCertificate as X509Certificate2;
                                TestHarness.Expect(remote?.Subject?.Contains(host) == true,
                                    $"interop: SNI {host} selects matching certificate", remote?.Subject);
                            }
                        }

                        TestHarness.Expect(await TestHarness.WaitUntil(
                                () => sniSeen.TryGetValue(host, out _), 3000).ConfigureAwait(false),
                            $"interop: server observes SNI {host}");
                    }
                }
                finally
                {
                    try { server.Stop(); } catch { }
                    try { server.Dispose(); } catch { }
                }
            }
        }

        /// <summary>
        /// 未信任的自签证书在默认校验策略下必须导致客户端握手失败。
        /// </summary>
        private static async Task InvalidCertificateIsRejectedAsync()
        {
            using (var serverCertificate = TlsTest.CreateCertificate("CN=localhost", false))
            {
                var port = TestHarness.GetFreeTcpPort();

                var serverOption = SocketOptionBuilder.Instance
                    .UseStream()
                    .SetIP("127.0.0.1")
                    .SetPort(port)
                    .SetReadBufferSize(8192)
                    .WithSsl(o =>
                    {
                        o.ServerCertificate = serverCertificate;
                        o.Protocols = SslProtocols.Tls12;
                    })
                    .Build();

                var server = new SaeaServerSocket(serverOption);
                server.Start();

                var clientOption = SocketOptionBuilder.Instance
                    .UseStream()
                    .SetIP("127.0.0.1")
                    .SetPort(port)
                    .SetReadBufferSize(8192)
                    .WithSsl(o =>
                    {
                        o.TargetHost = "localhost";
                        o.Protocols = SslProtocols.Tls12;
                        // AllowInvalidCertificates 为默认值 false，自签证书应被拒绝。
                    })
                    .Build();

                var client = new SaeaClientSocket(clientOption);
                try
                {
                    Exception error = null;
                    try
                    {
                        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                        {
                            await client.ConnectAsync(cts.Token).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                    }

                    TestHarness.Expect(error != null,
                        "interop: untrusted certificate aborts client handshake", error?.GetType().Name);
                    TestHarness.Expect(!client.IsAuthenticated,
                        "interop: client remains unauthenticated after rejection");
                }
                finally
                {
                    try { client.Dispose(); } catch { }
                    try { server.Stop(); } catch { }
                    try { server.Dispose(); } catch { }
                }
            }
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