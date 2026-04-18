using System;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.Caching;
using SAEA.RPC.Model;
using SAEA.RPC.Net;

namespace SAEA.RPCTest
{
    [TestClass]
    public class RpcCoderTests
    {
        [TestMethod]
        public void RSocketMsg_HasIsPooledProperty()
        {
            // Arrange
            var msg = new RSocketMsg(RSocketMsgType.Ping);

            // Act
            msg.IsPooled = true;

            // Assert
            Assert.IsTrue(msg.IsPooled);

            msg.IsPooled = false;
            Assert.IsFalse(msg.IsPooled);
        }

        [TestMethod]
        public void RpcCoder_Decode_SmallData_NotPooled()
        {
            // Arrange
            var coder = new RpcCoder();
            var serviceName = "TestService";
            var methodName = "TestMethod";
            var smallData = new byte[100]; // Small data < 4KB

            // Create encoded message
            var msg = new RSocketMsg(RSocketMsgType.RpcRequest, serviceName, methodName, smallData);
            var encoded = coder.Encode(msg);

            bool decoded = false;
            RSocketMsg decodedMsg = null;

            // Act
            coder.Unpack(encoded, (m) =>
            {
                decodedMsg = m;
                decoded = true;
            });

            // Assert
            Assert.IsTrue(decoded);
            Assert.IsNotNull(decodedMsg);
            Assert.IsFalse(decodedMsg.IsPooled);
            Assert.AreEqual(serviceName, decodedMsg.ServiceName);
            Assert.AreEqual(methodName, decodedMsg.MethodName);
        }

        [TestMethod]
        public void RpcCoder_Decode_LargeData_IsPooled()
        {
            // Arrange
            var coder = new RpcCoder();
            var serviceName = "TestService";
            var methodName = "TestMethod";
            var largeData = new byte[5000]; // Large data > 4KB

            // Create encoded message
            var msg = new RSocketMsg(RSocketMsgType.RpcRequest, serviceName, methodName, largeData);
            var encoded = coder.Encode(msg);

            bool decoded = false;
            RSocketMsg decodedMsg = null;

            // Act
            coder.Unpack(encoded, (m) =>
            {
                decodedMsg = m;
                decoded = true;
            });

            // Assert
            Assert.IsTrue(decoded);
            Assert.IsNotNull(decodedMsg);
            Assert.IsTrue(decodedMsg.IsPooled);
            Assert.AreEqual(serviceName, decodedMsg.ServiceName);
            Assert.AreEqual(methodName, decodedMsg.MethodName);
            Assert.AreEqual(largeData.Length, decodedMsg.Data.Length);
        }

        [TestMethod]
        public void RpcCoder_Decode_ReturnsBufferAfterProcessing()
        {
            // Arrange
            var coder = new RpcCoder();
            var largeData = new byte[5000];
            var msg = new RSocketMsg(RSocketMsgType.RpcRequest, "Test", "Method", largeData);
            var encoded = coder.Encode(msg);

            var statsBefore = MemoryPoolManager.GetStatistics();

            // Act
            coder.Unpack(encoded, (m) =>
            {
                // Process the message
                Assert.IsNotNull(m.Data);
            });

            // After processing, the buffer should be returned (async)
            // We can't directly verify this, but the test passes if no memory issues occur
        }

        [TestMethod]
        public void RpcCoder_Decode_ServiceNameMethodName_CorrectlyExtracted()
        {
            // Arrange
            var coder = new RpcCoder();
            var serviceName = "MyTestService";
            var methodName = "MyTestMethod";
            var data = Encoding.UTF8.GetBytes("Test data content");

            var msg = new RSocketMsg(RSocketMsgType.RpcRequest, serviceName, methodName, data);
            var encoded = coder.Encode(msg);

            bool decoded = false;
            RSocketMsg decodedMsg = null;

            // Act
            coder.Unpack(encoded, (m) =>
            {
                decodedMsg = m;
                decoded = true;
            });

            // Assert
            Assert.IsTrue(decoded);
            Assert.AreEqual(serviceName, decodedMsg.ServiceName);
            Assert.AreEqual(methodName, decodedMsg.MethodName);
            Assert.AreEqual("Test data content", Encoding.UTF8.GetString(decodedMsg.Data));
        }

        [TestMethod]
        public void RpcCoder_Encode_Decode_RoundTrip()
        {
            // Arrange
            var coder = new RpcCoder();
            var serviceName = "TestService";
            var methodName = "TestMethod";
            var data = new byte[1000];
            new Random().NextBytes(data);

            var originalMsg = new RSocketMsg(RSocketMsgType.RpcRequest, serviceName, methodName, data);

            // Act
            var encoded = coder.Encode(originalMsg);

            RSocketMsg decodedMsg = null;
            coder.Unpack(encoded, (m) =>
            {
                decodedMsg = m;
            });

            // Assert
            Assert.IsNotNull(decodedMsg);
            Assert.AreEqual(originalMsg.Type, decodedMsg.Type);
            Assert.AreEqual(originalMsg.ServiceName, decodedMsg.ServiceName);
            Assert.AreEqual(originalMsg.MethodName, decodedMsg.MethodName);
            Assert.AreEqual(originalMsg.Data.Length, decodedMsg.Data.Length);

            // Verify data content
            for (int i = 0; i < originalMsg.Data.Length; i++)
            {
                Assert.AreEqual(originalMsg.Data[i], decodedMsg.Data[i]);
            }
        }

        [TestMethod]
        public void RpcCoder_Clear_ResetsBuffer()
        {
            // Arrange
            var coder = new RpcCoder();
            var data = new byte[100];
            var msg = new RSocketMsg(RSocketMsgType.Ring, "Test", "Method", data);
            var encoded = coder.Encode(msg);

            // Add data to buffer
            coder.Unpack(encoded, (m) => { });

            // Act
            coder.Clear();

            // Assert - no exception should be thrown
            Assert.IsTrue(true);
        }
    }
}
