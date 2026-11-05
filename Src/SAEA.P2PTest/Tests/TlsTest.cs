using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography;
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
    /// SAEA.Sockets 内置 TLS（BouncyCastle）互通测试：流模式、事件模式与双向证书。
    /// </summary>
    public static class TlsTest
    {
        /// <summary>
        /// 运行全部 TLS 场景。
        /// </summary>
        public static async Task RunAsync()
        {
            TestHarness.Section("TlsTest");

            await StreamEchoAsync();
            await EventModeEchoAsync();
            await MutualTlsAsync(SslProtocols.Tls12, "mtls");
            await MutualTlsAsync(SaeaTlsProtocols.Tls13, "mtls13");

            TestHarness.WriteSummary("TlsTest");
        }

        /// <summary>
        /// 流模式：客户端与服务端都使用 <c>UseStream</c>，验证握手、ChannelInfo.Tls 与双工读写。
        /// </summary>
        private static async Task StreamEchoAsync()
        {
            using (var serverCertificate = CreateCertificate("CN=localhost", false))
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
                    })
                    .Build();

                var server = new SaeaServerSocket(serverOption);

                var accepted = new TaskCompletionSource<ChannelInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
                var serverHandshake = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                serverOption.Tls.HandshakeCompleted = context => serverHandshake.TrySetResult(true);

                server.OnAccepted += obj =>
                {
                    var channel = obj as ChannelInfo;
                    if (channel == null) return;

                    accepted.TrySetResult(channel);

                    // 流模式消费者独占 Stream：读取一段明文后原样回写。
                    Task.Run(async () =>
                    {
                        try
                        {
                            var buffer = new byte[64];
                            var read = await channel.Stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                            if (read > 0)
                            {
                                await channel.Stream.WriteAsync(buffer, 0, read).ConfigureAwait(false);
                                await channel.Stream.FlushAsync().ConfigureAwait(false);
                            }
                        }
                        catch
                        {
                        }
                    });
                };

                server.Start();

                var clientOption = SocketOptionBuilder.Instance
                    .UseStream()
                    .SetIP("127.0.0.1")
                    .SetPort(port)
                    .SetReadBufferSize(8192)
                    .WithSsl(o =>
                    {
                        o.TargetHost = "localhost";
                        o.AllowInvalidCertificates = true;
                    })
                    .Build();

                var client = new SaeaClientSocket(clientOption);
                try
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                    {
                        await client.ConnectAsync(cts.Token).ConfigureAwait(false);
                    }

                    TestHarness.Expect(client.IsAuthenticated, "stream: client IsAuthenticated");
                    TestHarness.Expect(await TestHarness.WaitUntil(() => serverHandshake.Task.IsCompleted, 5000),
                        "stream: server HandshakeCompleted callback");

                    var channel = await WaitAsync(accepted.Task, 5000).ConfigureAwait(false);
                    TestHarness.Expect(channel != null, "stream: server OnAccepted after handshake");
                    TestHarness.Expect(channel?.Tls != null, "stream: ChannelInfo.Tls populated");
                    TestHarness.Expect(channel?.Tls?.IsAuthenticated == true, "stream: ChannelInfo.Tls authenticated");
                    TestHarness.Expect(!string.IsNullOrEmpty(channel?.Tls?.NegotiatedProtocol),
                        "stream: negotiated protocol", channel?.Tls?.NegotiatedProtocol);
                    TestHarness.Expect(channel?.Tls?.NegotiatedProtocol?.Contains("1.3") == true,
                        "stream: default protocols negotiate TLS 1.3", channel?.Tls?.NegotiatedProtocol);
                    TestHarness.Expect(!string.IsNullOrEmpty(channel?.Tls?.CipherSuite),
                        "stream: negotiated cipher suite", channel?.Tls?.CipherSuite);
                    TestHarness.Expect(channel?.Tls?.RemoteCertificate == null,
                        "stream: no client certificate when not required");

                    var stream = client.GetStream();
                    var payload = Encoding.UTF8.GetBytes("tls-stream-echo");
                    await stream.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);

                    var response = new byte[payload.Length];
                    var total = 0;
                    while (total < response.Length)
                    {
                        var read = await stream.ReadAsync(response, total, response.Length - total).ConfigureAwait(false);
                        if (read <= 0) break;
                        total += read;
                    }

                    TestHarness.Expect(total == payload.Length && Encoding.UTF8.GetString(response) == "tls-stream-echo",
                        "stream: plaintext echo over TLS", $"{total} bytes");
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
        /// 事件模式：不使用 <c>UseStream</c>，TLS 解密后的数据经 <c>OnServerReceiveSpan</c> 交付。
        /// </summary>
        private static async Task EventModeEchoAsync()
        {
            using (var serverCertificate = CreateCertificate("CN=localhost", false))
            {
                var port = TestHarness.GetFreeTcpPort();

                var serverOption = SocketOptionBuilder.Instance
                    .UseIocp()
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
                var serverReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

                server.OnServerReceiveSpan += (token, span) =>
                {
                    var data = span.ToArray();
                    serverReceived.TrySetResult(data);
                    // 事件模式回写：服务端发送路径内部会完成 TLS 加密。
                    server.Send(token.ID, data);
                };

                server.Start();

                var clientOption = SocketOptionBuilder.Instance
                    .UseIocp()
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
                var clientReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                client.OnClientReceiveSpan += span => clientReceived.TrySetResult(span.ToArray());

                try
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                    {
                        await client.ConnectAsync(cts.Token).ConfigureAwait(false);
                    }

                    TestHarness.Expect(client.IsAuthenticated, "event: client IsAuthenticated");

                    var payload = Encoding.UTF8.GetBytes("tls-event-echo");
                    client.Send(payload);

                    var serverData = await WaitAsync(serverReceived.Task, 5000).ConfigureAwait(false);
                    TestHarness.Expect(serverData != null && Encoding.UTF8.GetString(serverData) == "tls-event-echo",
                        "event: server receives decrypted plaintext");

                    var clientData = await WaitAsync(clientReceived.Task, 5000).ConfigureAwait(false);
                    TestHarness.Expect(clientData != null && Encoding.UTF8.GetString(clientData) == "tls-event-echo",
                        "event: client receives decrypted plaintext");
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
        /// 双向证书：服务端要求客户端证书，验证客户端凭据与 <c>RemoteCertificate</c>。
        /// </summary>
        private static async Task MutualTlsAsync(SslProtocols protocols, string prefix)
        {
            using (var serverCertificate = CreateCertificate("CN=localhost", false))
            using (var clientCertificate = CreateCertificate("CN=saea-client", true))
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
                        o.Protocols = protocols;
                        o.ClientCertificateRequired = true;
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
                            var buffer = new byte[64];
                            var read = await channel.Stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                            if (read > 0)
                            {
                                await channel.Stream.WriteAsync(buffer, 0, read).ConfigureAwait(false);
                                await channel.Stream.FlushAsync().ConfigureAwait(false);
                            }
                        }
                        catch
                        {
                        }
                    });
                };

                server.Start();

                var clientOption = SocketOptionBuilder.Instance
                    .UseStream()
                    .SetIP("127.0.0.1")
                    .SetPort(port)
                    .SetReadBufferSize(8192)
                    .WithSsl(o =>
                    {
                        o.TargetHost = "localhost";
                        o.AllowInvalidCertificates = true;
                        o.Protocols = protocols;
                        o.ClientCertificates = new X509Certificate2Collection(clientCertificate);
                    })
                    .Build();

                var client = new SaeaClientSocket(clientOption);
                try
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                    {
                        await client.ConnectAsync(cts.Token).ConfigureAwait(false);
                    }

                    var channel = await WaitAsync(accepted.Task, 5000).ConfigureAwait(false);
                    TestHarness.Expect(channel != null, prefix + ": handshake succeeds with client certificate");
                    TestHarness.Expect(channel?.Tls?.RemoteCertificate != null, prefix + ": server sees client certificate");
                    TestHarness.Expect(channel?.Tls?.NegotiatedProtocol?.Contains(
                            protocols == SaeaTlsProtocols.Tls13 ? "1.3" : "1.2") == true,
                        prefix + ": negotiated protocol", channel?.Tls?.NegotiatedProtocol);
                    TestHarness.Expect(channel?.Tls?.RemoteCertificate?.Subject?.Contains("saea-client") == true,
                        prefix + ": client certificate subject", channel?.Tls?.RemoteCertificate?.Subject);

                    var stream = client.GetStream();
                    var payload = Encoding.UTF8.GetBytes("mtls-ok");
                    await stream.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);

                    var buffer = new byte[payload.Length];
                    var read = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                    TestHarness.Expect(read == payload.Length, prefix + ": application data flows", $"{read} bytes");
                }
                finally
                {
                    try { client.Dispose(); } catch { }
                    try { server.Stop(); } catch { }
                    try { server.Dispose(); } catch { }
                }
            }
        }

        private static async Task<T> WaitAsync<T>(Task<T> task, int timeoutMs)
        {
            var completed = await Task.WhenAny(task, Task.Delay(timeoutMs)).ConfigureAwait(false);
            if (completed != task) return default;
            return await task.ConfigureAwait(false);
        }

        internal static X509Certificate2 CreateCertificate(string subject, bool client, string[] dnsNames = null)
        {
            using (var rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));

                var usages = new OidCollection
                {
                    new Oid(client ? "1.3.6.1.5.5.7.3.2" : "1.3.6.1.5.5.7.3.1")
                };
                request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usages, false));

                if (!client)
                {
                    var san = new SubjectAlternativeNameBuilder();
                    foreach (var dnsName in dnsNames ?? new[] { "localhost" })
                    {
                        san.AddDnsName(dnsName);
                    }
                    san.AddIpAddress(IPAddress.Loopback);
                    request.CertificateExtensions.Add(san.Build());
                }

                using (var certificate = request.CreateSelfSigned(
                    DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30)))
                {
                    // 通过 PFX 往返，确保私钥可被 GetRSAPrivateKey 稳定访问。
                    var pfx = certificate.Export(X509ContentType.Pfx, "saea");
                    return new X509Certificate2(pfx, "saea");
                }
            }
        }
    }
}