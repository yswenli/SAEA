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
 *命名空间：SAEA.Sockets.Pipeline.Tests.Tests.Integration
 *文件名： SendOwnershipTests
 *版本号： net10.0
 *唯一标识：c4f1d9a2-6b3e-4f7a-9d05-82a1e6b3c7d9
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/9 11:40:00
 *描述：发送重载的所有权释放次数、失败可观察与 TLS 全记录交接用例
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/9 11:40:00
 *修改人： yswenli
 *版本号： net10.0
 *描述：发送重载的所有权释放次数、失败可观察与 TLS 全记录交接用例
 *
 *****************************************************************************/
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Sockets;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Model;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Integration
{
    /// <summary>
    /// 验证带 <see cref="IDisposable"/> 所有者对象的发送重载：返回的 Task 表示底层交接完成，
    /// 失败可观察，所有者对象在可确认路径上恰好释放一次，TLS 一次应用数据的全部记录完成后才返回。
    /// </summary>
    [TestClass]
    public class SendOwnershipTests
    {
        private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 服务端发送后 await 返回，回环对端能收到完整数据，所有者对象恰好释放一次。
        /// </summary>
        [TestMethod]
        public async Task ServerSendAsync_ConnectedClient_CompletesAfterSocketSend()
        {
            var accepted = NewAcceptedSource();
            var port = GetFreePort();

            using (var server = StartServer(port, accepted))
            using (var remote = new TcpClient())
            {
                remote.Connect(IPAddress.Loopback, port);
                var sessionId = await accepted.Task.WaitAsync(TestTimeout).ConfigureAwait(false);

                var payload = Encoding.UTF8.GetBytes("server-send-completes-after-handoff");
                var owner = new CountingDisposable();

                await server.SendAsync(sessionId, new ReadOnlyMemory<byte>(payload), owner)
                    .WaitAsync(TestTimeout).ConfigureAwait(false);

                Assert.AreEqual(1, owner.Count, "底层完成后所有者对象必须恰好释放一次。");

                var received = new byte[payload.Length];
                var read = await ReadExactAsync(remote.GetStream(), received, received.Length)
                    .WaitAsync(TestTimeout).ConfigureAwait(false);

                Assert.AreEqual(payload.Length, read);
                CollectionAssert.AreEqual(payload, received);
            }
        }

        /// <summary>
        /// 会话不存在时发送同步失败并释放所有者对象一次，调用方无需再次释放。
        /// </summary>
        [TestMethod]
        public void ServerSendAsync_SessionMissing_ThrowsAndReleasesOwnerOnce()
        {
            var accepted = NewAcceptedSource();
            var port = GetFreePort();

            using (var server = StartServer(port, accepted))
            {
                var owner = new CountingDisposable();

                Assert.ThrowsException<KernelException>(
                    () => server.SendAsync("missing-session", new ReadOnlyMemory<byte>(new byte[8]), owner));

                Assert.AreEqual(1, owner.Count, "同步失败路径所有者对象必须恰好释放一次。");
            }
        }

        /// <summary>
        /// 对端断开后发送必须以失败结束，不得静默成功，且所有者对象恰好释放一次。
        /// </summary>
        [TestMethod]
        public async Task ServerSendAsync_ClientDisconnected_CompletesWithFailure()
        {
            var accepted = NewAcceptedSource();
            var disconnected = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var port = GetFreePort();

            using (var server = StartServer(port, accepted, disconnected))
            {
                using (var remote = new TcpClient())
                {
                    remote.Connect(IPAddress.Loopback, port);
                    var sessionId = await accepted.Task.WaitAsync(TestTimeout).ConfigureAwait(false);

                    remote.Close();
                    await disconnected.Task.WaitAsync(TestTimeout).ConfigureAwait(false);

                    var owner = new CountingDisposable();
                    var captured = await CaptureSendFailureAsync(server, sessionId, owner).ConfigureAwait(false);

                    Assert.IsNotNull(captured, "断开后发送必须失败，不能静默成功。");
                    Assert.AreEqual(1, owner.Count, "失败路径所有者对象必须恰好释放一次。");
                }
            }
        }

        /// <summary>
        /// 客户端未建立连接时发送同步失败并释放所有者对象一次。
        /// </summary>
        [TestMethod]
        public void ClientSendAsync_BeforeConnect_Fails()
        {
            var option = new SocketOptionBuilder()
                .UseStream()
                .SetIP(IPAddress.Loopback.ToString())
                .SetPort(GetFreePort())
                .Build();

            using (var client = new SaeaClientSocket(option))
            {
                var owner = new CountingDisposable();

                Assert.ThrowsException<KernelException>(
                    () => client.SendAsync(new ReadOnlyMemory<byte>(new byte[4]), owner));

                Assert.AreEqual(1, owner.Count, "未连接失败路径所有者对象必须恰好释放一次。");
            }
        }

        /// <summary>
        /// 成功路径每次发送各自恰好释放一次所有者对象；旧操作的迟到完成不得释放新一次发送的所有者对象。
        /// </summary>
        [TestMethod]
        public async Task SendAsync_OwnerCount()
        {
            var accepted = NewAcceptedSource();
            var port = GetFreePort();

            using (var server = StartServer(port, accepted))
            using (var remote = new TcpClient())
            {
                remote.Connect(IPAddress.Loopback, port);
                var sessionId = await accepted.Task.WaitAsync(TestTimeout).ConfigureAwait(false);

                const int sends = 3;
                var payload = Encoding.UTF8.GetBytes("owner-count");
                var expected = new byte[payload.Length * sends];

                for (var i = 0; i < sends; i++)
                {
                    var owner = new CountingDisposable();
                    await server.SendAsync(sessionId, new ReadOnlyMemory<byte>(payload), owner)
                        .WaitAsync(TestTimeout).ConfigureAwait(false);

                    Assert.AreEqual(1, owner.Count, "第 " + (i + 1) + " 次发送的所有者对象必须恰好释放一次。");
                    Buffer.BlockCopy(payload, 0, expected, i * payload.Length, payload.Length);
                }

                var received = new byte[expected.Length];
                var read = await ReadExactAsync(remote.GetStream(), received, received.Length)
                    .WaitAsync(TestTimeout).ConfigureAwait(false);
                Assert.AreEqual(expected.Length, read);
                CollectionAssert.AreEqual(expected, received);
            }

            AssertStaleCompletionDoesNotReleaseNextOwner();
        }

        /// <summary>
        /// TLS 回环发送大于单条记录长度的应用数据，await 返回时本次全部 TLS 记录都已完成底层交接。
        /// </summary>
        [TestMethod]
        public async Task TlsSend_LastRecordHandedOff()
        {
            using (var certificate = CreateServerCertificate())
            {
                var accepted = NewAcceptedSource();
                var port = GetFreePort();

                var serverOption = new SocketOptionBuilder()
                    .UseStream()
                    .SetIP(IPAddress.Loopback.ToString())
                    .SetPort(port)
                    .WithSsl(o => o.ServerCertificate = certificate)
                    .Build();

                using (var server = new SaeaServerSocket(serverOption))
                {
                    server.OnAccepted += obj =>
                    {
                        var info = obj as ChannelInfo;
                        if (info != null) accepted.TrySetResult(info.ID);
                    };
                    server.Start();

                    var clientOption = new SocketOptionBuilder()
                        .UseStream()
                        .SetIP(IPAddress.Loopback.ToString())
                        .SetPort(port)
                        .WithSsl(o =>
                        {
                            o.TargetHost = "localhost";
                            o.AllowInvalidCertificates = true;
                        })
                        .Build();

                    using (var client = new SaeaClientSocket(clientOption))
                    {
                        client.Connect();
                        Assert.IsTrue(client.IsAuthenticated, "TLS 握手未完成。");

                        var sessionId = await accepted.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                        var counters = new TlsCounters(GetConnection(server, sessionId));

                        var payload = new byte[128 * 1024];
                        new Random(7).NextBytes(payload);

                        var generatedBefore = counters.Generated;
                        var owner = new CountingDisposable();

                        await server.SendAsync(sessionId, new ReadOnlyMemory<byte>(payload), owner)
                            .WaitAsync(TimeSpan.FromSeconds(20)).ConfigureAwait(false);

                        Assert.AreEqual(1, owner.Count, "TLS 发送完成后所有者对象必须恰好释放一次。");

                        var generated = counters.Generated;
                        Assert.IsTrue(generated > generatedBefore, "本次应用数据应拆出多条 TLS 记录。");
                        Assert.AreEqual(generated, counters.HandedOff,
                            "await 返回时本次全部 TLS 记录必须已完成底层交接。");

                        var received = new byte[payload.Length];
                        var read = await ReadExactAsync(client.GetStream(), received, received.Length)
                            .WaitAsync(TimeSpan.FromSeconds(20)).ConfigureAwait(false);

                        Assert.AreEqual(payload.Length, read);
                        CollectionAssert.AreEqual(payload, received);
                    }
                }
            }
        }

        /// <summary>
        /// 直接用发送操作对象验证：重复完成与迟到完成都只影响自己的所有者对象。
        /// </summary>
        private static void AssertStaleCompletionDoesNotReleaseNextOwner()
        {
            var operationType = typeof(IServerSocket).Assembly.GetType("SAEA.Sockets.Core.SendOperation", true);
            Assert.IsNotNull(operationType);

            var constructor = operationType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(IDisposable) },
                null);
            Assert.IsNotNull(constructor);

            var complete = operationType.GetMethod("TryCompleteSuccess", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(complete);

            var firstOwner = new CountingDisposable();
            var secondOwner = new CountingDisposable();
            var first = constructor.Invoke(new object[] { firstOwner });
            var second = constructor.Invoke(new object[] { secondOwner });

            Assert.IsTrue((bool)complete.Invoke(first, null));
            Assert.AreEqual(1, firstOwner.Count);

            Assert.IsFalse((bool)complete.Invoke(first, null), "重复完成不得再次释放。");
            Assert.AreEqual(0, secondOwner.Count, "旧操作的迟到完成不得释放新一次发送的所有者对象。");

            Assert.IsTrue((bool)complete.Invoke(second, null));
            Assert.AreEqual(1, secondOwner.Count);
        }

        private static async Task<Exception> CaptureSendFailureAsync(
            SaeaServerSocket server,
            string sessionId,
            CountingDisposable owner)
        {
            Task sending;
            try
            {
                sending = server.SendAsync(sessionId, new ReadOnlyMemory<byte>(new byte[8]), owner);
            }
            catch (Exception ex)
            {
                return ex;
            }

            if (sending == null) return null;

            try
            {
                await sending.WaitAsync(TestTimeout).ConfigureAwait(false);
                return null;
            }
            catch (TimeoutException)
            {
                Assert.Fail("断开后发送任务未在限定时间内完成。");
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private static TaskCompletionSource<string> NewAcceptedSource()
        {
            return new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static SaeaServerSocket StartServer(
            int port,
            TaskCompletionSource<string> accepted,
            TaskCompletionSource<string> disconnected = null)
        {
            var option = new SocketOptionBuilder()
                .UseStream()
                .SetIP(IPAddress.Loopback.ToString())
                .SetPort(port)
                .Build();

            var server = new SaeaServerSocket(option);
            server.OnAccepted += obj =>
            {
                var info = obj as ChannelInfo;
                if (info != null) accepted.TrySetResult(info.ID);
            };
            if (disconnected != null)
            {
                server.OnDisconnected += (id, ex) => disconnected.TrySetResult(id);
            }

            server.Start();
            return server;
        }

        private static object GetConnection(SaeaServerSocket server, string sessionId)
        {
            var userToken = server.SessionManager.Get(sessionId);
            Assert.IsNotNull(userToken, "服务端会话不存在：" + sessionId);

            var getConnection = typeof(SaeaServerSocket).GetMethod(
                "GetConnection", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(getConnection);

            var connection = getConnection.Invoke(server, new object[] { userToken });
            Assert.IsNotNull(connection, "服务端连接门面不存在：" + sessionId);
            return connection;
        }

        private static int GetFreePort()
        {
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

        private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, int count)
        {
            var read = 0;
            while (read < count)
            {
                var current = await stream.ReadAsync(buffer, read, count - read).ConfigureAwait(false);
                if (current <= 0) break;
                read += current;
            }

            return read;
        }

        /// <summary>
        /// 生成仅用于本地回环测试的自签名服务端证书；私钥经 PFX 往返，保证可被 TLS 传输层稳定读取。
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

        private sealed class CountingDisposable : IDisposable
        {
            private int _count;

            public int Count
            {
                get { return Volatile.Read(ref _count); }
            }

            public void Dispose()
            {
                Interlocked.Increment(ref _count);
            }
        }

        private sealed class TlsCounters
        {
            private readonly object _connection;
            private readonly PropertyInfo _generated;
            private readonly PropertyInfo _handedOff;

            public TlsCounters(object connection)
            {
                _connection = connection;
                var type = connection.GetType();
                _generated = type.GetProperty("TlsRecordsGenerated", BindingFlags.Instance | BindingFlags.NonPublic);
                _handedOff = type.GetProperty("TlsRecordsHandedOff", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(_generated);
                Assert.IsNotNull(_handedOff);
            }

            public long Generated
            {
                get { return (long)_generated.GetValue(_connection); }
            }

            public long HandedOff
            {
                get { return (long)_handedOff.GetValue(_connection); }
            }
        }
    }
}