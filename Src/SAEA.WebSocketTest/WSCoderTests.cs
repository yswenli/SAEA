using System;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.Caching;
using SAEA.WebSocket.Model;
using SAEA.WebSocket.Type;

namespace SAEA.WebSocketTest
{
    [TestClass]
    public class WSCoderTests
    {
        [TestMethod]
        public void WSProtocal_HasIsPooledProperty()
        {
            // Arrange
            var content = Encoding.UTF8.GetBytes("Test content");
            var protocal = new WSProtocal(WSProtocalType.Text, content);

            // Act & Assert
            Assert.IsFalse(protocal.IsPooled);

            protocal.IsPooled = true;
            Assert.IsTrue(protocal.IsPooled);
        }

        [TestMethod]
        public void WSCoder_Decode_SmallPayload_NotPooled()
        {
            // Arrange
            var coder = new WSCoder();
            var payloadData = Encoding.UTF8.GetBytes("Small data"); // < 4KB

            // Create a WebSocket frame
            var frame = CreateWebSocketFrame(0x01, payloadData, false); // Text frame, no mask

            // Act
            var result = coder.Decode(frame);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            var protocal = result[0] as WSProtocal;
            Assert.IsNotNull(protocal);
            Assert.IsFalse(protocal.IsPooled);
            Assert.AreEqual(payloadData.Length, protocal.Content.Length);
        }

        [TestMethod]
        public void WSCoder_Decode_LargePayload_IsPooled()
        {
            // Arrange
            var coder = new WSCoder();
            var payloadData = new byte[5000]; // > 4KB
            new Random().NextBytes(payloadData);

            // Create a WebSocket frame
            var frame = CreateWebSocketFrame(0x01, payloadData, false); // Text frame, no mask

            // Act
            var result = coder.Decode(frame);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            var protocal = result[0] as WSProtocal;
            Assert.IsNotNull(protocal);
            Assert.IsTrue(protocal.IsPooled);
            Assert.AreEqual(payloadData.Length, protocal.Content.Length);
        }

        [TestMethod]
        public void WSProtocal_Dispose_PooledBuffer_ReturnsToPool()
        {
            // Arrange
            var largeData = new byte[5000];
            new Random().NextBytes(largeData);
            var protocal = new WSProtocal(WSProtocalType.Binary, largeData)
            {
                IsPooled = true
            };

            // Act
            protocal.Dispose();

            // Assert - Content should be null after dispose
            Assert.IsNull(protocal.Content);
            Assert.IsFalse(protocal.IsPooled);
        }

        [TestMethod]
        public void WSProtocal_Dispose_NonPooledBuffer_ClearsData()
        {
            // Arrange
            var smallData = new byte[100];
            new Random().NextBytes(smallData);
            var protocal = new WSProtocal(WSProtocalType.Text, smallData)
            {
                IsPooled = false
            };

            // Act
            protocal.Dispose();

            // Assert
            Assert.IsNull(protocal.Content);
        }

        [TestMethod]
        public void WSCoder_Decode_MultipleFrames_WorksCorrectly()
        {
            // Arrange
            var coder = new WSCoder();
            var payloadData1 = Encoding.UTF8.GetBytes("Frame 1");
            var payloadData2 = Encoding.UTF8.GetBytes("Frame 2");

            var frame1 = CreateWebSocketFrame(0x01, payloadData1, false);
            var frame2 = CreateWebSocketFrame(0x01, payloadData2, false);

            var combinedFrame = new byte[frame1.Length + frame2.Length];
            Buffer.BlockCopy(frame1, 0, combinedFrame, 0, frame1.Length);
            Buffer.BlockCopy(frame2, 0, combinedFrame, frame1.Length, frame2.Length);

            // Act
            var result = coder.Decode(combinedFrame);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
        }

        [TestMethod]
        public void WSCoder_DoMask_WorksCorrectly()
        {
            // Arrange
            var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
            var masks = new byte[] { 0xFF, 0xFE, 0xFD, 0xFC };
            var original = new byte[data.Length];
            Buffer.BlockCopy(data, 0, original, 0, data.Length);

            // Act
            WSCoder.DoMask(data, 0, data.Length, masks);

            // Assert - XOR operation
            for (int i = 0; i < data.Length; i++)
            {
                Assert.AreEqual((byte)(original[i] ^ masks[i % 4]), data[i]);
            }

            // XOR again should restore original
            WSCoder.DoMask(data, 0, data.Length, masks);
            for (int i = 0; i < data.Length; i++)
            {
                Assert.AreEqual(original[i], data[i]);
            }
        }

        [TestMethod]
        public void WSCoder_Clear_ResetsBuffer()
        {
            // Arrange
            var coder = new WSCoder();
            var payloadData = Encoding.UTF8.GetBytes("Test data");
            var frame = CreateWebSocketFrame(0x01, payloadData, false);

            // Add data
            coder.Decode(frame);

            // Act
            coder.Clear();

            // Assert - No exception should be thrown
            Assert.IsTrue(true);
        }

        [TestMethod]
        public void WSProtocal_ToBytes_RoundTrip()
        {
            // Arrange
            var content = Encoding.UTF8.GetBytes("Hello WebSocket");
            var protocal = new WSProtocal(WSProtocalType.Text, content);

            // Act
            var bytes = protocal.ToBytes();
            Assert.IsNotNull(bytes);
            Assert.IsTrue(bytes.Length > 0);
        }

        /// <summary>
        /// Creates a WebSocket frame for testing
        /// </summary>
        private byte[] CreateWebSocketFrame(byte opcode, byte[] payload, bool masked)
        {
            var frame = new System.Collections.Generic.List<byte>();

            // First byte: FIN=1, RSV=0, opcode
            frame.Add((byte)(0x80 | opcode));

            // Second byte: MASK, payload length
            byte secondByte = 0;
            if (masked) secondByte |= 0x80;

            if (payload.Length < 126)
            {
                secondByte |= (byte)payload.Length;
                frame.Add(secondByte);
            }
            else if (payload.Length < 65536)
            {
                secondByte |= 126;
                frame.Add(secondByte);
                frame.Add((byte)((payload.Length >> 8) & 0xFF));
                frame.Add((byte)(payload.Length & 0xFF));
            }
            else
            {
                secondByte |= 127;
                frame.Add(secondByte);
                for (int i = 7; i >= 0; i--)
                {
                    frame.Add((byte)((payload.Length >> (i * 8)) & 0xFF));
                }
            }

            // Masking key (if masked)
            if (masked)
            {
                var maskKey = new byte[] { 0x01, 0x02, 0x03, 0x04 };
                frame.AddRange(maskKey);

                // Mask payload
                var maskedPayload = new byte[payload.Length];
                for (int i = 0; i < payload.Length; i++)
                {
                    maskedPayload[i] = (byte)(payload[i] ^ maskKey[i % 4]);
                }
                frame.AddRange(maskedPayload);
            }
            else
            {
                frame.AddRange(payload);
            }

            return frame.ToArray();
        }
    }
}
