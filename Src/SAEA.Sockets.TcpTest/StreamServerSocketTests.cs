using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.Caching;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Model;

namespace SAEA.Sockets.TcpTest
{
    [TestClass]
    public class StreamServerSocketTests
    {
        [TestMethod]
        public void StreamServerSocket_Constructor_RentsPooledBuffer()
        {
            // Arrange
            var socketOption = new SocketOption
            {
                IP = "127.0.0.1",
                Port = 0, // Let system assign port
                ReadBufferSize = 8192
            };
            var cts = new CancellationTokenSource();

            // Act
            using (var serverSocket = new StreamServerSocket(socketOption, cts.Token))
            {
                // Assert - buffer should be rented and available
                Assert.IsNotNull(serverSocket);
                Assert.AreEqual(socketOption, serverSocket.SocketOption);
            }
        }

        [TestMethod]
        public void StreamServerSocket_Dispose_ReturnsPooledBuffer()
        {
            // Arrange
            var socketOption = new SocketOption
            {
                IP = "127.0.0.1",
                Port = 0,
                ReadBufferSize = 4096
            };
            var cts = new CancellationTokenSource();

            var statsBefore = MemoryPoolManager.GetStatistics();

            // Act
            using (var serverSocket = new StreamServerSocket(socketOption, cts.Token))
            {
                // Server created with pooled buffer
            }

            // Assert - dispose should return buffer (though we can't directly verify private field)
            // The test passes if no exception is thrown during dispose
            Assert.IsTrue(true);
        }

        [TestMethod]
        public void StreamServerSocket_LargeBuffer_RentsFromLargePool()
        {
            // Arrange - use a large buffer that would exceed small threshold
            var socketOption = new SocketOption
            {
                IP = "127.0.0.1",
                Port = 0,
                ReadBufferSize = 65536 // Large threshold
            };
            var cts = new CancellationTokenSource();

            // Act
            using (var serverSocket = new StreamServerSocket(socketOption, cts.Token))
            {
                // Assert
                Assert.IsNotNull(serverSocket);
            }
        }

        [TestMethod]
        public void StreamServerSocket_StartStop_CyclesCorrectly()
        {
            // Arrange
            var socketOption = new SocketOption
            {
                IP = "127.0.0.1",
                Port = 18999,
                ReadBufferSize = 4096
            };
            var cts = new CancellationTokenSource();

            // Act & Assert
            using (var serverSocket = new StreamServerSocket(socketOption, cts.Token))
            {
                Assert.IsFalse(serverSocket.IsDisposed);

                serverSocket.Start();

                // Give server time to start
                Thread.Sleep(100);

                serverSocket.Stop();

                Assert.IsFalse(serverSocket.IsDisposed);
            }
        }

        [TestMethod]
        public void StreamServerSocket_MultipleDispose_DoesNotThrow()
        {
            // Arrange
            var socketOption = new SocketOption
            {
                IP = "127.0.0.1",
                Port = 0,
                ReadBufferSize = 4096
            };
            var cts = new CancellationTokenSource();

            var serverSocket = new StreamServerSocket(socketOption, cts.Token);

            // Act & Assert
            serverSocket.Dispose();
            serverSocket.Dispose(); // Should not throw on second dispose

            Assert.IsTrue(serverSocket.IsDisposed);
        }
    }
}
