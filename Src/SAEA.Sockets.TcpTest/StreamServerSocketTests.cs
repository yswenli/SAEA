using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Sockets;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Model;

namespace SAEA.Sockets.TcpTest
{
    [TestClass]
    public class StreamServerSocketTests
    {
        [TestMethod]
        public void StreamServerSocket_Constructor_AcceptsOption()
        {
            var option = SocketOptionBuilder.Instance
                .UseStream()
                .SetIP("127.0.0.1")
                .SetPort(0)
                .SetReadBufferSize(8192)
                .Build();
            var cts = new CancellationTokenSource();

            using (var serverSocket = new StreamServerSocket(option, cts.Token))
            {
                Assert.IsNotNull(serverSocket);
                Assert.AreSame(option, serverSocket.SocketOption);
            }
        }

        [TestMethod]
        public void StreamServerSocket_MultipleDispose_DoesNotThrow()
        {
            var option = SocketOptionBuilder.Instance
                .UseStream()
                .SetIP("127.0.0.1")
                .SetPort(0)
                .SetReadBufferSize(4096)
                .Build();
            var cts = new CancellationTokenSource();

            var serverSocket = new StreamServerSocket(option, cts.Token);

            serverSocket.Dispose();
            serverSocket.Dispose();

            Assert.IsTrue(serverSocket.IsDisposed);
        }

        [TestMethod]
        public void StreamServerSocket_StartStop_CyclesCorrectly()
        {
            var option = SocketOptionBuilder.Instance
                .UseStream()
                .SetIP("127.0.0.1")
                .SetPort(GetFreeTcpPort())
                .SetReadBufferSize(4096)
                .Build();
            var cts = new CancellationTokenSource();

            using (var serverSocket = new StreamServerSocket(option, cts.Token))
            {
                Assert.IsFalse(serverSocket.IsDisposed);

                serverSocket.Start();
                Thread.Sleep(100);
                serverSocket.Stop();

                Assert.IsFalse(serverSocket.IsDisposed);
            }
        }

        [TestMethod]
        public void StreamServerSocket_UseStream_SpanReceive_AccumulatesFragments()
        {
            int port = GetFreeTcpPort();
            var option = SocketOptionBuilder.Instance
                .UseStream()
                .SetIP("127.0.0.1")
                .SetPort(port)
                .SetReadBufferSize(4096)
                .Build();
            var cts = new CancellationTokenSource();

            var serverSocket = new StreamServerSocket(option, cts.Token);
            var gate = new object();
            var received = new List<byte>();
            serverSocket.OnServerReceiveSpan += (token, span) =>
            {
                var copy = span.ToArray();
                lock (gate)
                {
                    received.AddRange(copy);
                }
            };

            var frame = BuildFrame((byte)SocketProtocalType.RequestSend, Encoding.UTF8.GetBytes("tcp-fragmented"));

            try
            {
                serverSocket.Start();
                Thread.Sleep(200);

                using (var tcp = new TcpClient())
                {
                    tcp.Connect(IPAddress.Loopback, port);
                    var stream = tcp.GetStream();

                    stream.Write(frame, 0, 4);
                    stream.Flush();
                    Thread.Sleep(100);
                    stream.Write(frame, 4, frame.Length - 4);
                    stream.Flush();

                    Assert.IsTrue(WaitUntil(() => Length(gate, received) >= frame.Length, 3000),
                        "span callback did not accumulate the full frame");

                    var delivered = Snapshot(gate, received);
                    CollectionAssert.AreEqual(frame, delivered, "accumulated span bytes must equal the full frame");
                }
            }
            finally
            {
                try { serverSocket.Stop(); } catch { }
                try { serverSocket.Dispose(); } catch { }
            }
        }

        static int Length(object gate, List<byte> buffer)
        {
            lock (gate)
            {
                return buffer.Count;
            }
        }

        static byte[] Snapshot(object gate, List<byte> buffer)
        {
            lock (gate)
            {
                return buffer.ToArray();
            }
        }

        static bool WaitUntil(Func<bool> condition, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (condition())
                {
                    return true;
                }
                Thread.Sleep(25);
            }
            return condition();
        }

        static int GetFreeTcpPort()
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

        static byte[] BuildFrame(byte type, byte[] body)
        {
            var content = body ?? new byte[0];
            var buffer = new byte[9 + content.Length];
            Buffer.BlockCopy(BitConverter.GetBytes((long)content.Length), 0, buffer, 0, 8);
            buffer[8] = type;
            Buffer.BlockCopy(content, 0, buffer, 9, content.Length);
            return buffer;
        }
    }
}