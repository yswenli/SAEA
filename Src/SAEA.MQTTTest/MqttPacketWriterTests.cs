using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.Caching;
using SAEA.MQTT.Formatter;

namespace SAEA.MQTTTest
{
    [TestClass]
    public class MqttPacketWriterTests
    {
        [TestMethod]
        public void MqttPacketWriter_Constructor_RentsPooledBuffer()
        {
            // Arrange & Act
            var writer = new MqttPacketWriter();

            // Assert
            Assert.IsNotNull(writer);
            Assert.IsTrue(writer.Length >= 0);

            // Cleanup - call FreeBuffer to return pooled memory
            writer.FreeBuffer();
        }

        [TestMethod]
        public void MqttPacketWriter_Write_IncreasesLength()
        {
            // Arrange
            var writer = new MqttPacketWriter();
            var initialLength = writer.Length;

            // Act
            writer.Write((byte)0x01);

            // Assert
            Assert.AreEqual(initialLength + 1, writer.Length);

            writer.FreeBuffer();
        }

        [TestMethod]
        public void MqttPacketWriter_WriteMultipleBytes_WorksCorrectly()
        {
            // Arrange
            var writer = new MqttPacketWriter();

            // Act
            writer.Write((byte)0x01);
            writer.Write((byte)0x02);
            writer.Write((byte)0x03);

            // Assert
            Assert.AreEqual(3, writer.Length);

            var buffer = writer.GetBuffer();
            Assert.AreEqual(0x01, buffer[0]);
            Assert.AreEqual(0x02, buffer[1]);
            Assert.AreEqual(0x03, buffer[2]);

            writer.FreeBuffer();
        }

        [TestMethod]
        public void MqttPacketWriter_WriteUshort_WorksCorrectly()
        {
            // Arrange
            var writer = new MqttPacketWriter();

            // Act
            writer.Write((ushort)0x1234);

            // Assert
            Assert.AreEqual(2, writer.Length);

            var buffer = writer.GetBuffer();
            Assert.AreEqual(0x12, buffer[0]); // High byte
            Assert.AreEqual(0x34, buffer[1]); // Low byte

            writer.FreeBuffer();
        }

        [TestMethod]
        public void MqttPacketWriter_WriteWithLengthPrefix_String_WorksCorrectly()
        {
            // Arrange
            var writer = new MqttPacketWriter();
            var testString = "Hello MQTT";

            // Act
            writer.WriteWithLengthPrefix(testString);

            // Assert
            Assert.AreEqual(2 + testString.Length, writer.Length);

            writer.FreeBuffer();
        }

        [TestMethod]
        public void MqttPacketWriter_WriteWithLengthPrefix_NullString_WritesZeroLength()
        {
            // Arrange
            var writer = new MqttPacketWriter();

            // Act
            writer.WriteWithLengthPrefix((string)null);

            // Assert
            Assert.AreEqual(2, writer.Length);

            writer.FreeBuffer();
        }

        [TestMethod]
        public void MqttPacketWriter_WriteWithLengthPrefix_ByteArray_WorksCorrectly()
        {
            // Arrange
            var writer = new MqttPacketWriter();
            var testData = new byte[] { 0x01, 0x02, 0x03, 0x04 };

            // Act
            writer.WriteWithLengthPrefix(testData);

            // Assert
            Assert.AreEqual(2 + testData.Length, writer.Length);

            writer.FreeBuffer();
        }

        [TestMethod]
        public void MqttPacketWriter_WriteVariableLengthInteger_SmallValue_WorksCorrectly()
        {
            // Arrange
            var writer = new MqttPacketWriter();

            // Act
            writer.WriteVariableLengthInteger(127); // Small value

            // Assert
            Assert.IsTrue(writer.Length >= 1);

            writer.FreeBuffer();
        }

        [TestMethod]
        public void MqttPacketWriter_FreeBuffer_SmallBuffer_NotChanged()
        {
            // Arrange
            var writer = new MqttPacketWriter();
            writer.Write((byte)0x01);
            var lengthBefore = writer.Length;

            // Act
            writer.FreeBuffer();

            // Assert - FreeBuffer should not affect small buffers
            Assert.AreEqual(lengthBefore, writer.Length);
        }

        [TestMethod]
        public void MqttPacketWriter_Seek_ChangesPosition()
        {
            // Arrange
            var writer = new MqttPacketWriter();
            writer.Write((byte)0x01);
            writer.Write((byte)0x02);
            writer.Write((byte)0x03);

            // Act
            writer.Seek(1);
            writer.Write((byte)0xFF);

            // Assert
            var buffer = writer.GetBuffer();
            Assert.AreEqual(0x01, buffer[0]);
            Assert.AreEqual(0xFF, buffer[1]);
            Assert.AreEqual(0x03, buffer[2]);

            writer.FreeBuffer();
        }

        [TestMethod]
        public void MqttPacketWriter_Reset_SetsLength()
        {
            // Arrange
            var writer = new MqttPacketWriter();
            writer.Write((byte)0x01);
            writer.Write((byte)0x02);

            // Act
            writer.Reset(1);

            // Assert
            Assert.AreEqual(1, writer.Length);

            writer.FreeBuffer();
        }

        [TestMethod]
        public void MqttPacketWriter_EnsureCapacity_GrowsBuffer()
        {
            // Arrange
            var writer = new MqttPacketWriter();
            var bufferBefore = writer.GetBuffer();
            var capacityBefore = bufferBefore.Length;

            // Act - Write more data than initial capacity
            var largeData = new byte[capacityBefore + 100];
            new Random().NextBytes(largeData);
            writer.Write(largeData, 0, largeData.Length);

            // Assert
            var bufferAfter = writer.GetBuffer();
            Assert.IsTrue(bufferAfter.Length >= largeData.Length);

            writer.FreeBuffer();
        }

        [TestMethod]
        public void MqttPacketWriter_StaticBuildFixedHeader_WorksCorrectly()
        {
            // Arrange & Act
            var header = MqttPacketWriter.BuildFixedHeader(
                SAEA.MQTT.Protocol.MqttControlPacketType.Connect);

            // Assert
            Assert.AreEqual(0x10, header); // Connect = 1 << 4
        }

        [TestMethod]
        public void MqttPacketWriter_StaticEncodeVariableLengthInteger_Zero()
        {
            // Arrange & Act
            var result = MqttPacketWriter.EncodeVariableLengthInteger(0);

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(0, result.Array[result.Offset]);
        }

        [TestMethod]
        public void MqttPacketWriter_StaticEncodeVariableLengthInteger_SmallValue()
        {
            // Arrange & Act
            var result = MqttPacketWriter.EncodeVariableLengthInteger(100);

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(100, result.Array[result.Offset]);
        }

        [TestMethod]
        public void MqttPacketWriter_StaticEncodeVariableLengthInteger_LargeValue()
        {
            // Arrange & Act
            var result = MqttPacketWriter.EncodeVariableLengthInteger(1000);

            // Assert
            Assert.IsTrue(result.Count >= 2);
        }

        [TestMethod]
        public void MqttPacketWriter_StaticGetLengthOfVariableInteger_Correct()
        {
            // Arrange & Act & Assert
            Assert.AreEqual(1, MqttPacketWriter.GetLengthOfVariableInteger(0));
            Assert.AreEqual(1, MqttPacketWriter.GetLengthOfVariableInteger(127));
            Assert.AreEqual(2, MqttPacketWriter.GetLengthOfVariableInteger(128));
            Assert.AreEqual(2, MqttPacketWriter.GetLengthOfVariableInteger(16383));
            Assert.AreEqual(3, MqttPacketWriter.GetLengthOfVariableInteger(16384));
        }
    }
}
