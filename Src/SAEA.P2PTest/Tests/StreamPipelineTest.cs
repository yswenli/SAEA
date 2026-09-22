using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// StreamServerSocket 的 PipeReader span 接收测试，覆盖真实令牌负载、原始回写与分片投递。
    /// </summary>
    public static class StreamPipelineTest
    {
        /// <summary>
        /// 启动 Stream 服务端，验证 span 回调负载、令牌身份、原始发送与分片累积。
        /// </summary>
        public static async Task RunAsync()
        {
            TestHarness.Section("StreamPipelineTest");

            int port = TestHarness.GetFreeTcpPort();
            var option = SAEA.Sockets.SocketOptionBuilder.Instance
                .UseStream()
                .SetIP("127.0.0.1")
                .SetPort(port)
                .SetReadBufferSize(8192)
                .Build();

            var server = new SAEA.Sockets.Core.Tcp.StreamServerSocket(option, CancellationToken.None);
            var capture = new StreamCapture();
            server.OnServerReceiveSpan += capture.OnSpan;
            server.OnAccepted += capture.OnAccepted;

            var frame = StreamDecoderTest.BuildFrame((byte)SocketProtocalType.RequestSend, Encoding.UTF8.GetBytes("stream-span"));
            var firstFragmentLength = 10;

            TcpClient? tcp = null;

            try
            {
                server.Start();

                tcp = await ConnectAsync(port, 3000);
                TestHarness.Expect(tcp != null, "stream server accepts a client connection within the readiness window");

                if (tcp != null)
                {
                    var stream = tcp.GetStream();
                    await stream.WriteAsync(frame, 0, frame.Length);
                    await stream.FlushAsync();

                    await TestHarness.WaitUntil(() => capture.IsReady(frame.Length), 3000);

                    var delivered = capture.Snapshot();
                    TestHarness.Expect(delivered.SequenceEqual(frame), "stream span delivers exact frame bytes");

                    var token = capture.Token;
                    var accepted = capture.Accepted;

                    TestHarness.Expect(token != null, "stream span token is non-null");

                    var streamToken = token as StreamUserToken;
                    TestHarness.Expect(streamToken != null && streamToken.Input != null,
                        "stream span token is StreamUserToken with non-null Input");

                    TestHarness.Expect(accepted != null, "OnAccepted delivers a ChannelInfo");

                    TestHarness.Expect(token != null && accepted != null && token.ID == accepted.ID,
                        "stream span token.ID matches ChannelInfo.ID");

                    TestHarness.Expect(token != null && accepted != null && ReferenceEquals(accepted.UserToken, token),
                        "ChannelInfo.UserToken is the same instance as the span token");

                    if (accepted != null)
                    {
                        server.SendAsync(accepted.ID, new ReadOnlyMemory<byte>(frame));

                        var buffer = new byte[frame.Length];
                        var read = await ReadExactAsync(stream, buffer, frame.Length, 3000);
                        TestHarness.Expect(read == frame.Length && buffer.SequenceEqual(frame),
                            "stream SendAsync writes the raw frame bytes");
                    }

                    capture.Reset();

                    await stream.WriteAsync(frame, 0, firstFragmentLength);
                    await stream.FlushAsync();

                    await TestHarness.WaitUntil(() => capture.Count >= firstFragmentLength, 3000);

                    await stream.WriteAsync(frame, firstFragmentLength, frame.Length - firstFragmentLength);
                    await stream.FlushAsync();

                    await TestHarness.WaitUntil(() => capture.Count >= frame.Length, 3000);

                    var fragmented = capture.Snapshot();
                    TestHarness.Expect(fragmented.SequenceEqual(frame),
                        "stream span accumulates fragmented writes into the full frame");
                    TestHarness.Expect(capture.Callbacks >= 2,
                        "stream span invokes the callback once per received segment");

                    var disconnects = new List<string>();
                    var disconnectGate = new object();
                    server.OnDisconnected += (id, ex) =>
                    {
                        lock (disconnectGate) { disconnects.Add(id); }
                    };

                    tcp.Dispose();
                    tcp = null;

                    await TestHarness.WaitUntil(() =>
                    {
                        lock (disconnectGate) { return disconnects.Count >= 1; }
                    }, 3000);

                    lock (disconnectGate)
                    {
                        TestHarness.Expect(disconnects.Count == 1,
                            "stream half-open close triggers OnDisconnected exactly once");
                    }

                    await Task.Delay(300);

                    lock (disconnectGate)
                    {
                        TestHarness.Expect(disconnects.Count == 1,
                            "stream half-open close does not double-fire OnDisconnected");
                    }
                }
            }
            finally
            {
                tcp?.Dispose();
                try { server.Stop(); } catch { }
                try { server.Dispose(); } catch { }
            }
        }

        /// <summary>
        /// 有界重试连接，替代原有的固定启动延时，直到服务端可接受连接或超时。
        /// </summary>
        static async Task<TcpClient?> ConnectAsync(int port, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var tcp = new TcpClient();
                try
                {
                    await tcp.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
                    return tcp;
                }
                catch (SocketException)
                {
                    tcp.Dispose();
                    await Task.Delay(25).ConfigureAwait(false);
                }
            }

            return null;
        }

        static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, int count, int timeoutMs)
        {
            var total = 0;
            using (var cts = new CancellationTokenSource(timeoutMs))
            {
                while (total < count)
                {
                    int read;
                    try
                    {
                        read = await stream.ReadAsync(buffer, total, count - total, cts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    if (read == 0) break;
                    total += read;
                }
            }

            return total;
        }

        /// <summary>
        /// 线程安全的接收状态：所有字段的读写都在同一锁内，避免回调与主线程之间的竞态。
        /// </summary>
        sealed class StreamCapture
        {
            readonly object _gate = new object();
            readonly List<byte> _received = new List<byte>();
            IUserToken? _token;
            ChannelInfo? _accepted;
            int _callbacks;

            public void OnSpan(IUserToken token, ReadOnlySpan<byte> span)
            {
                lock (_gate)
                {
                    _received.AddRange(span.ToArray());
                    _token = token;
                    _callbacks++;
                }
            }

            public void OnAccepted(object obj)
            {
                lock (_gate)
                {
                    _accepted = obj as ChannelInfo;
                }
            }

            public bool IsReady(int minBytes)
            {
                lock (_gate)
                {
                    return _token != null && _accepted != null && _received.Count >= minBytes;
                }
            }

            public byte[] Snapshot()
            {
                lock (_gate)
                {
                    return _received.ToArray();
                }
            }

            public int Count
            {
                get { lock (_gate) { return _received.Count; } }
            }

            public int Callbacks
            {
                get { lock (_gate) { return _callbacks; } }
            }

            public IUserToken? Token
            {
                get { lock (_gate) { return _token; } }
            }

            public ChannelInfo? Accepted
            {
                get { lock (_gate) { return _accepted; } }
            }

            public void Reset()
            {
                lock (_gate)
                {
                    _received.Clear();
                    _token = null;
                    _callbacks = 0;
                }
            }
        }
    }
}
