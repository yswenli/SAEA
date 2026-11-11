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
*文件名： RemoteFinTests
*版本号： net10.0
*唯一标识：b178266f-3ce4-4161-8667-3570e37f34f5
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 17:21:26
*描述：对端 FIN 在流模式下按入站结束处理的端到端用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 17:21:26
*修改人： yswenli
*版本号： net10.0
*描述：对端 FIN 在流模式下按入站结束处理的端到端用例
*
*****************************************************************************/
using System;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Sockets;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Model;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Integration
{
    /// <summary>
    /// 配置输入处理器（流模式）时，对端 FIN 视为「入站结束」而不是「连接断开」：
    /// 处理器读到全部字节并看到结束标记，收尾断开由框架在处理器返回后完成。
    /// </summary>
    [TestClass]
    public class RemoteFinTests
    {
        private const int PayloadSize = 256 * 1024;

        private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 服务端：对端写入 256KB 后半关发送方向，输入处理器读满全部字节并通过 IsCompleted 看到入站结束。
        /// </summary>
        [TestMethod]
        public async Task Server_PeerFin_InputHandlerReadsAllBytesThenSeesEof()
        {
            var observed = NewObservationSource();
            var accepted = NewAcceptedSource();

            using (var server = StartServer(accepted, observed))
            {
                var port = server.LocalEndPoint.Port;

                using (var remote = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    remote.Connect(new IPEndPoint(IPAddress.Loopback, port));
                    await accepted.Task.WaitAsync(TestTimeout).ConfigureAwait(false);

                    var payload = CreatePayload(11);
                    await SendAllAsync(remote, payload).ConfigureAwait(false);
                    remote.Shutdown(SocketShutdown.Send);

                    var result = await observed.Task.WaitAsync(TestTimeout).ConfigureAwait(false);

                    Assert.IsTrue(result.IsCompleted, "输入处理器必须通过 ReadResult.IsCompleted 看到入站结束。");
                    Assert.IsFalse(result.IsCanceled, "对端 FIN 不得取消输入处理器。");
                    Assert.AreEqual((long)PayloadSize, result.TotalBytes, "输入处理器必须读到 FIN 之前的全部字节。");
                }
            }
        }

        /// <summary>
        /// 服务端：对端半关之后仍能回写数据，且客户端完整收到（数据连接 FIN 之后回 226 依赖此语义）。
        /// </summary>
        [TestMethod]
        public async Task Server_PeerFin_CanStillSendBack()
        {
            var observed = NewObservationSource();
            var accepted = NewAcceptedSource();
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using (var server = StartServer(accepted, observed, handlerTail: async () =>
            {
                // 等回写完成后再让出连接所有权，避免框架提前收口。
                await release.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
            }))
            {
                var port = server.LocalEndPoint.Port;

                using (var remote = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    remote.Connect(new IPEndPoint(IPAddress.Loopback, port));
                    var sessionId = await accepted.Task.WaitAsync(TestTimeout).ConfigureAwait(false);

                    var payload = CreatePayload(12);
                    await SendAllAsync(remote, payload).ConfigureAwait(false);
                    remote.Shutdown(SocketShutdown.Send);

                    var result = await observed.Task.WaitAsync(TestTimeout).ConfigureAwait(false);
                    Assert.IsTrue(result.IsCompleted, "回写前必须先观察到入站结束。");

                    var reply = CreatePayload(13, 4096);
                    await server.SendAsync(sessionId, new ReadOnlyMemory<byte>(reply), null)
                        .WaitAsync(TestTimeout).ConfigureAwait(false);

                    // 半关只关闭发送方向，接收方向仍可用；半关后 Socket.Connected 不再可靠，直接按字节读取。
                    var received = new byte[reply.Length];
                    var read = await ReceiveExactAsync(remote, received, received.Length)
                        .WaitAsync(TestTimeout).ConfigureAwait(false);

                    Assert.AreEqual(reply.Length, read, "对端半关后服务端回写的数据必须完整送达。");
                    CollectionAssert.AreEqual(reply, received);

                    release.TrySetResult(true);
                }
            }
        }

        /// <summary>
        /// 服务端：OnDisconnected 在输入处理器读完剩余字节之后触发，断开不早于入站读尽。
        /// </summary>
        [TestMethod]
        public async Task Server_PeerFin_DisconnectRaisedAfterHandlerCompletes()
        {
            var accepted = NewAcceptedSource();
            var disconnected = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            var totalBytes = 0L;
            var eofSeen = 0;
            var totalAtDisconnect = -1L;
            var eofAtDisconnect = 0;

            var option = new SocketOptionBuilder()
                .UseStream()
                .SetIP(IPAddress.Loopback.ToString())
                .SetPort(0)
                .UseConnectionOpened(_ => { })
                .UseInputHandler(async (userToken, input, flow, token) =>
                {
                    long total = 0;
                    while (true)
                    {
                        var result = await input.ReadAsync(token).ConfigureAwait(false);
                        total += result.Buffer.Length;
                        input.AdvanceTo(result.Buffer.End);

                        if (result.IsCanceled) break;
                        if (result.IsCompleted)
                        {
                            // 先落字节数再置结束标志，事件侧读到的顺序即为「读尽 → 断开」。
                            Volatile.Write(ref totalBytes, total);
                            Volatile.Write(ref eofSeen, 1);
                            break;
                        }
                    }
                })
                .Build();

            using (var server = new SaeaServerSocket(option))
            {
                server.OnAccepted += obj =>
                {
                    var info = obj as ChannelInfo;
                    if (info != null) accepted.TrySetResult(info.ID);
                };
                server.OnDisconnected += (id, ex) =>
                {
                    eofAtDisconnect = Volatile.Read(ref eofSeen);
                    totalAtDisconnect = Volatile.Read(ref totalBytes);
                    disconnected.TrySetResult(id);
                };

                server.Start();

                using (var remote = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    remote.Connect(new IPEndPoint(IPAddress.Loopback, server.LocalEndPoint.Port));
                    await accepted.Task.WaitAsync(TestTimeout).ConfigureAwait(false);

                    var payload = CreatePayload(14);
                    await SendAllAsync(remote, payload).ConfigureAwait(false);
                    remote.Shutdown(SocketShutdown.Send);

                    await disconnected.Task.WaitAsync(TestTimeout).ConfigureAwait(false);

                    Assert.AreEqual(1, eofAtDisconnect, "OnDisconnected 必须晚于输入处理器看到入站结束。");
                    Assert.AreEqual((long)PayloadSize, totalAtDisconnect, "OnDisconnected 触发时输入处理器已读完全部字节。");
                }
            }
        }

        /// <summary>
        /// 客户端：对端写入 256KB 后半关发送方向，输入处理器读满全部字节并通过 IsCompleted 看到入站结束。
        /// </summary>
        [TestMethod]
        public async Task Client_PeerFin_InputHandlerReadsAllBytesThenSeesEof()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            try
            {
                var observed = NewObservationSource();
                var option = NewClientOption(((IPEndPoint)listener.LocalEndpoint).Port, observed);

                using (var client = new SaeaClientSocket(option))
                {
                    client.Connect();
                    Assert.IsTrue(client.Connected, "客户端未建立连接。");

                    using (var remote = await listener.AcceptTcpClientAsync().WaitAsync(TestTimeout).ConfigureAwait(false))
                    {
                        var payload = CreatePayload(15);
                        await SendAllAsync(remote.GetStream(), payload).ConfigureAwait(false);
                        remote.Client.Shutdown(SocketShutdown.Send);

                        var result = await observed.Task.WaitAsync(TestTimeout).ConfigureAwait(false);

                        Assert.IsTrue(result.IsCompleted, "输入处理器必须通过 ReadResult.IsCompleted 看到入站结束。");
                        Assert.IsFalse(result.IsCanceled, "对端 FIN 不得取消输入处理器。");
                        Assert.AreEqual((long)PayloadSize, result.TotalBytes, "输入处理器必须读到 FIN 之前的全部字节。");
                    }
                }
            }
            finally
            {
                listener.Stop();
            }
        }

        /// <summary>
        /// 客户端：FIN 后接收循环不得空转，断开事件必须在限定时间内触发，且输入处理器只接管一次。
        /// </summary>
        [TestMethod]
        public async Task Client_PeerFin_NoSpin_DisconnectRaised()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            try
            {
                var disconnected = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                var handlerRounds = 0;
                var readRounds = 0;
                var totalBytes = 0L;

                var option = new SocketOptionBuilder()
                    .UseStream()
                    .SetIP(IPAddress.Loopback.ToString())
                    .SetPort(((IPEndPoint)listener.LocalEndpoint).Port)
                    .UseConnectionOpened(_ => { })
                    .UseInputHandler(async (userToken, input, flow, token) =>
                    {
                        Interlocked.Increment(ref handlerRounds);

                        while (true)
                        {
                            var result = await input.ReadAsync(token).ConfigureAwait(false);
                            Interlocked.Increment(ref readRounds);
                            totalBytes += result.Buffer.Length;
                            input.AdvanceTo(result.Buffer.End);

                            if (result.IsCanceled) break;
                            if (result.IsCompleted) break;
                        }
                    })
                    .Build();

                using (var client = new SaeaClientSocket(option))
                {
                    client.OnDisconnected += (id, ex) => disconnected.TrySetResult(id);
                    client.Connect();
                    Assert.IsTrue(client.Connected, "客户端未建立连接。");

                    using (var remote = await listener.AcceptTcpClientAsync().WaitAsync(TestTimeout).ConfigureAwait(false))
                    {
                        // 只发一次数据：接收轮次有上限才能证明 FIN 之后没有反复空转。
                        var payload = CreatePayload(16, 1024);
                        await SendAllAsync(remote.GetStream(), payload).ConfigureAwait(false);
                        remote.Client.Shutdown(SocketShutdown.Send);

                        await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

                        Assert.AreEqual(1, Volatile.Read(ref handlerRounds), "输入处理器只应被启动一次。");
                        Assert.AreEqual((long)payload.Length, totalBytes, "断开前必须读完已到达的全部字节。");
                        Assert.IsTrue(Volatile.Read(ref readRounds) <= 64,
                            "一次发送的数据不应用超过 64 个接收轮次读取，接收轮次应为有限值。");
                    }
                }
            }
            finally
            {
                listener.Stop();
            }
        }

        private static ISocketOption NewClientOption(int port, TaskCompletionSource<FinObservation> observed)
        {
            return new SocketOptionBuilder()
                .UseStream()
                .SetIP(IPAddress.Loopback.ToString())
                .SetPort(port)
                .UseConnectionOpened(_ => { })
                .UseInputHandler((userToken, input, flow, token) => RunObservationAsync(input, observed, token))
                .Build();
        }

        private static SaeaServerSocket StartServer(
            TaskCompletionSource<string> accepted,
            TaskCompletionSource<FinObservation> observed,
            Func<Task> handlerTail = null)
        {
            var option = new SocketOptionBuilder()
                .UseStream()
                .SetIP(IPAddress.Loopback.ToString())
                .SetPort(0)
                .UseConnectionOpened(_ => { })
                .UseInputHandler(async (userToken, input, flow, token) =>
                {
                    await RunObservationAsync(input, observed, token).ConfigureAwait(false);
                    if (handlerTail != null) await handlerTail().ConfigureAwait(false);
                })
                .Build();

            var server = new SaeaServerSocket(option);
            server.OnAccepted += obj =>
            {
                var info = obj as ChannelInfo;
                if (info != null) accepted.TrySetResult(info.ID);
            };

            server.Start();
            Assert.IsNotNull(server.LocalEndPoint, "服务端未绑定本地端点。");
            return server;
        }

        private static async Task RunObservationAsync(
            PipeReader input,
            TaskCompletionSource<FinObservation> observed,
            CancellationToken token)
        {
            try
            {
                observed.TrySetResult(await DrainUntilEofAsync(input, token).ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                observed.TrySetException(ex);
            }
        }

        private static async Task<FinObservation> DrainUntilEofAsync(PipeReader input, CancellationToken token)
        {
            long total = 0;

            while (true)
            {
                var result = await input.ReadAsync(token).ConfigureAwait(false);
                total += result.Buffer.Length;
                input.AdvanceTo(result.Buffer.End);

                if (result.IsCanceled) return new FinObservation(total, false, true);
                if (result.IsCompleted) return new FinObservation(total, true, false);
            }
        }

        private static TaskCompletionSource<string> NewAcceptedSource()
        {
            return new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static TaskCompletionSource<FinObservation> NewObservationSource()
        {
            return new TaskCompletionSource<FinObservation>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static byte[] CreatePayload(int seed, int size = PayloadSize)
        {
            var payload = new byte[size];
            new Random(seed).NextBytes(payload);
            return payload;
        }

        private static async Task SendAllAsync(Socket socket, byte[] data)
        {
            var sent = 0;
            while (sent < data.Length)
            {
                var current = await socket.SendAsync(
                    new ReadOnlyMemory<byte>(data, sent, data.Length - sent),
                    SocketFlags.None).ConfigureAwait(false);
                if (current <= 0) throw new IOException("对端未接收数据。");
                sent += current;
            }
        }

        private static async Task SendAllAsync(Stream stream, byte[] data)
        {
            await stream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
        }

        private static async Task<int> ReceiveExactAsync(Socket socket, byte[] buffer, int count)
        {
            var read = 0;
            while (read < count)
            {
                var current = await socket.ReceiveAsync(
                    new Memory<byte>(buffer, read, count - read), SocketFlags.None).ConfigureAwait(false);
                if (current <= 0) break;
                read += current;
            }

            return read;
        }

        private readonly struct FinObservation
        {
            public FinObservation(long totalBytes, bool isCompleted, bool isCanceled)
            {
                TotalBytes = totalBytes;
                IsCompleted = isCompleted;
                IsCanceled = isCanceled;
            }

            public long TotalBytes { get; }

            public bool IsCompleted { get; }

            public bool IsCanceled { get; }
        }
    }
}