using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// UDP 客户端/服务端的 span 接收测试，验证数据报经 span 回调逐字节投递。
    /// </summary>
    public static class UdpPipelineTest
    {
        /// <summary>
        /// 绑定 UDP 服务端与客户端，发送一个数据报并验证服务端与客户端的 span 事件。
        /// </summary>
        public static async Task RunAsync()
        {
            TestHarness.Section("UdpPipelineTest");

            int port = TestHarness.GetFreeUdpPort();

            var serverOption = SAEA.Sockets.SocketOptionBuilder.Instance
                .SetSocket(SAEASocketType.Udp)
                .UseIocp()
                .SetIP("127.0.0.1")
                .SetPort(port)
                .SetReadBufferSize(8192)
                .Build();

            var clientOption = SAEA.Sockets.SocketOptionBuilder.Instance
                .SetSocket(SAEASocketType.Udp)
                .UseIocp()
                .SetIP("127.0.0.1")
                .SetPort(port)
                .SetReadBufferSize(8192)
                .Build();

            var serverCapture = new UdpCapture();
            var clientCapture = new UdpCapture();

            SAEA.Sockets.Core.Udp.UdpServerSocket? server = null;
            SAEA.Sockets.Core.Udp.UdpClientSocket? client = null;

            try
            {
                server = new SAEA.Sockets.Core.Udp.UdpServerSocket(serverOption);
                server.OnServerReceiveSpan += serverCapture.OnServerSpan;
                server.Start();
                await Task.Delay(200);

                client = new SAEA.Sockets.Core.Udp.UdpClientSocket(clientOption);
                client.OnClientReceiveSpan += clientCapture.OnClientSpan;
                client.Connect();
                await Task.Delay(200);

                var frame = StreamDecoderTest.BuildFrame((byte)SocketProtocalType.RequestSend, Encoding.UTF8.GetBytes("udp-span"));
                client.SendAsync(new ReadOnlyMemory<byte>(frame));

                await TestHarness.WaitUntil(() => serverCapture.HasSpan, 3000);

                var serverSpan = serverCapture.Span;
                TestHarness.Expect(serverSpan != null && serverSpan.SequenceEqual(frame),
                    "UDP server span receives the exact datagram bytes");

                var token = serverCapture.Token;
                TestHarness.Expect(token != null && !string.IsNullOrEmpty(token.ID),
                    "UDP server span token carries a session id");

                if (token != null)
                {
                    var reply = StreamDecoderTest.BuildFrame((byte)SocketProtocalType.RequestSend, Encoding.UTF8.GetBytes("udp-reply"));
                    server.SendAsync(token, reply);

                    await TestHarness.WaitUntil(() => clientCapture.HasSpan, 3000);

                    var clientSpan = clientCapture.Span;
                    TestHarness.Expect(clientSpan != null && clientSpan.SequenceEqual(reply),
                        "UDP client span receives the reply datagram bytes");
                }
            }
            finally
            {
                try { client?.Dispose(); } catch { }

                var stopwatch = Stopwatch.StartNew();
                try { server?.Stop(); } catch { }
                stopwatch.Stop();

                try { server?.Dispose(); } catch { }

                TestHarness.Expect(stopwatch.ElapsedMilliseconds < 2000,
                    "UDP server Stop completes without blocking teardown");
            }
        }

        /// <summary>
        /// 线程安全的接收状态：所有字段的读写都在同一锁内，避免回调与主线程之间的竞态。
        /// </summary>
        sealed class UdpCapture
        {
            readonly object _gate = new object();
            byte[]? _span;
            IUserToken? _token;

            public void OnServerSpan(IUserToken token, ReadOnlySpan<byte> span)
            {
                lock (_gate)
                {
                    _token = token;
                    _span = span.ToArray();
                }
            }

            public void OnClientSpan(ReadOnlySpan<byte> span)
            {
                lock (_gate)
                {
                    _span = span.ToArray();
                }
            }

            public bool HasSpan
            {
                get { lock (_gate) { return _span != null; } }
            }

            public byte[]? Span
            {
                get { lock (_gate) { return _span; } }
            }

            public IUserToken? Token
            {
                get { lock (_gate) { return _token; } }
            }
        }
    }
}
