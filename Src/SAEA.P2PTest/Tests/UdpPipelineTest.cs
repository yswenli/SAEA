using System;
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

            int port = TestHarness.GetFreeTcpPort();

            var serverOption = SAEA.Sockets.SocketOptionBuilder.Instance
                .SetSocket(SAEASocketType.Udp)
                .UseIocp()
                .SetIP("127.0.0.1")
                .SetPort(port)
                .SetReadBufferSize(8192)
                .Build();

            var server = new SAEA.Sockets.Core.Udp.UdpServerSocket(serverOption);
            byte[]? serverSpan = null;
            IUserToken? serverToken = null;
            server.OnServerReceiveSpan += (token, span) =>
            {
                serverToken = token;
                serverSpan = span.ToArray();
            };

            server.Start();
            await Task.Delay(200);

            var clientOption = SAEA.Sockets.SocketOptionBuilder.Instance
                .SetSocket(SAEASocketType.Udp)
                .UseIocp()
                .SetIP("127.0.0.1")
                .SetPort(port)
                .SetReadBufferSize(8192)
                .Build();

            var client = new SAEA.Sockets.Core.Udp.UdpClientSocket(clientOption);
            byte[]? clientSpan = null;
            client.OnClientReceiveSpan += span => clientSpan = span.ToArray();

            try
            {
                client.Connect();
                await Task.Delay(200);

                var frame = StreamDecoderTest.BuildFrame((byte)SocketProtocalType.RequestSend, Encoding.UTF8.GetBytes("udp-span"));
                client.SendAsync(new ReadOnlyMemory<byte>(frame));

                await TestHarness.WaitUntil(() => serverSpan != null, 3000);
                TestHarness.Expect(serverSpan != null && serverSpan.SequenceEqual(frame),
                    "UDP server span receives the exact datagram bytes");
                TestHarness.Expect(serverToken != null && !string.IsNullOrEmpty(serverToken.ID),
                    "UDP server span token carries a session id");

                if (serverToken != null)
                {
                    var reply = StreamDecoderTest.BuildFrame((byte)SocketProtocalType.RequestSend, Encoding.UTF8.GetBytes("udp-reply"));
                    server.SendAsync(serverToken, reply);

                    await TestHarness.WaitUntil(() => clientSpan != null, 3000);
                    TestHarness.Expect(clientSpan != null && clientSpan.SequenceEqual(reply),
                        "UDP client span receives the reply datagram bytes");
                }
            }
            finally
            {
                try { client.Dispose(); } catch { }
                try { server.Dispose(); } catch { }
            }
        }
    }
}