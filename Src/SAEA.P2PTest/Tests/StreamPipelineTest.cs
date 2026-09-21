using System;
using System.Collections.Generic;
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
            var gate = new object();
            var received = new List<byte>();
            IUserToken? receivedToken = null;
            ChannelInfo? accepted = null;
            Exception? serverError = null;

            server.OnServerReceiveSpan += (token, span) =>
            {
                lock (gate)
                {
                    received.AddRange(span.ToArray());
                }
                receivedToken = token;
            };
            server.OnAccepted += obj => accepted = obj as ChannelInfo;
            server.OnError += (id, ex) => serverError = ex;

            server.Start();
            await Task.Delay(200);

            var frame = StreamDecoderTest.BuildFrame((byte)SocketProtocalType.RequestSend, Encoding.UTF8.GetBytes("stream-span"));

            try
            {
                using (var tcp = new TcpClient())
                {
                    await tcp.ConnectAsync(IPAddress.Loopback, port);
                    var stream = tcp.GetStream();
                    await stream.WriteAsync(frame, 0, frame.Length);
                    await stream.FlushAsync();

                    await TestHarness.WaitUntil(() => Snapshot(gate, received).Length >= frame.Length, 3000);

                    var delivered = Snapshot(gate, received);
                    TestHarness.Expect(delivered.SequenceEqual(frame), "stream span delivers exact frame bytes");

                    var token = receivedToken;
                    TestHarness.Expect(token != null, "stream span token is non-null");

                    var streamToken = token as StreamUserToken;
                    TestHarness.Expect(streamToken != null && streamToken.Input != null,
                        "stream span token is StreamUserToken with non-null Input");

                    TestHarness.Expect(accepted != null, "OnAccepted delivers a ChannelInfo");

                    TestHarness.Expect(token != null && accepted != null && token.ID == accepted.ID,
                        "stream span token.ID matches ChannelInfo.ID");

                    TestHarness.Expect(accepted != null && accepted.UserToken != null && accepted.UserToken.ID == accepted.ID,
                        "ChannelInfo.UserToken.ID matches ChannelInfo.ID");

                    if (accepted != null)
                    {
                        server.SendAsync(accepted.ID, new ReadOnlyMemory<byte>(frame));

                        var buffer = new byte[frame.Length];
                        var read = await ReadExactAsync(stream, buffer, frame.Length, 3000);
                        TestHarness.Expect(read == frame.Length && buffer.SequenceEqual(frame),
                            "stream SendAsync writes the raw frame bytes");
                    }

                    received.Clear();
                    receivedToken = null;

                    await stream.WriteAsync(frame, 0, 4);
                    await stream.FlushAsync();
                    await Task.Delay(100);
                    await stream.WriteAsync(frame, 4, frame.Length - 4);
                    await stream.FlushAsync();

                    await TestHarness.WaitUntil(() => Snapshot(gate, received).Length >= frame.Length, 3000);

                    var fragmented = Snapshot(gate, received);
                    TestHarness.Expect(fragmented.SequenceEqual(frame),
                        "stream span accumulates fragmented writes into the full frame");
                }
            }
            finally
            {
                try { server.Stop(); } catch { }
                try { server.Dispose(); } catch { }
            }

            TestHarness.Expect(serverError == null, "stream server raised no error during the test");
        }

        static byte[] Snapshot(object gate, List<byte> buffer)
        {
            lock (gate)
            {
                return buffer.ToArray();
            }
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
    }
}