using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Runtime
{
    [TestClass]
    public class PipelineOptionsTests
    {
        [TestMethod]
        public void NullOptions_ThrowsArgumentNullException()
        {
            Assert.ThrowsException<ArgumentNullException>(() => ChannelPipeline.Create(null));
        }

        [TestMethod]
        public void NullSplitter_ThrowsArgumentException()
        {
            var options = NewOptions();
            options.Splitter = null;

            Assert.ThrowsException<ArgumentException>(() => ChannelPipeline.Create(options));
        }

        [TestMethod]
        public void QueueCapacityBelowTwo_ThrowsArgumentOutOfRangeException()
        {
            var options = NewOptions();
            options.QueueCapacity = 1;

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ChannelPipeline.Create(options));
        }

        [TestMethod]
        public void MaxConcurrentMessagesBelowOne_ThrowsArgumentOutOfRangeException()
        {
            var options = NewOptions();
            options.MaxConcurrentMessages = 0;

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ChannelPipeline.Create(options));
        }

        [TestMethod]
        public void ResumeWatermarkNegative_ThrowsArgumentOutOfRangeException()
        {
            var options = NewOptions();
            options.QueueCapacity = 16;
            options.ResumeWatermark = -1;

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ChannelPipeline.Create(options));
        }

        [TestMethod]
        public void ResumeWatermarkAtOrAboveQueueCapacity_ThrowsArgumentOutOfRangeException()
        {
            var options = NewOptions();
            options.QueueCapacity = 16;
            options.ResumeWatermark = 16;

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ChannelPipeline.Create(options));
        }

        [TestMethod]
        public void MessageTimeoutNegative_ThrowsArgumentOutOfRangeException()
        {
            var options = NewOptions();
            options.MessageTimeout = TimeSpan.FromTicks(-1);

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ChannelPipeline.Create(options));
        }

        [TestMethod]
        public void DrainTimeoutNegative_ThrowsArgumentOutOfRangeException()
        {
            var options = NewOptions();
            options.DrainTimeout = TimeSpan.FromTicks(-1);

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ChannelPipeline.Create(options));
        }

        [TestMethod]
        public void SmallFrameInlineThresholdNegative_ThrowsArgumentOutOfRangeException()
        {
            var options = NewOptions();
            options.SmallFrameInlineThreshold = -1;

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ChannelPipeline.Create(options));
        }

        [TestMethod]
        public void EnvelopeCopyThresholdNegative_ThrowsArgumentOutOfRangeException()
        {
            var options = NewOptions();
            options.EnvelopeCopyThreshold = -1;

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ChannelPipeline.Create(options));
        }

        [TestMethod]
        public void ReadBufferSizeZero_ThrowsArgumentOutOfRangeException()
        {
            var options = NewOptions();
            options.ReadBufferSize = 0;

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ChannelPipeline.Create(options));
        }

        [TestMethod]
        public void ResumeWatermarkZero_NormalizesToHalfQueueCapacity()
        {
            var options = NewOptions();
            options.QueueCapacity = 10;
            options.ResumeWatermark = 0;

            using (var pipeline = ChannelPipeline.Create(options))
            {
                Assert.AreEqual(5, pipeline.EffectiveResumeWatermark);
            }

            options.QueueCapacity = 2;

            using (var pipeline = ChannelPipeline.Create(options))
            {
                Assert.AreEqual(1, pipeline.EffectiveResumeWatermark);
            }
        }

        [TestMethod]
        public void MaxBufferedBytesZero_DerivesFromMaxFrameLengthAndReadBufferSize()
        {
            const int maxFrameLength = 128;
            const int readBufferSize = 4096;

            var options = NewOptions(maxFrameLength);
            options.MaxBufferedBytes = 0;
            options.ReadBufferSize = readBufferSize;

            using (var pipeline = ChannelPipeline.Create(options))
            {
                Assert.AreEqual(maxFrameLength + 9 + readBufferSize, pipeline.EffectiveMaxBufferedBytes);
                Assert.AreEqual(
                    maxFrameLength + FrameConstants.P_Head + readBufferSize,
                    pipeline.EffectiveMaxBufferedBytes);
            }
        }

        [TestMethod]
        public void MaxBufferedBytesBelowMaximumFrame_ThrowsArgumentOutOfRangeException()
        {
            const int maxFrameLength = 1024;

            var options = NewOptions(maxFrameLength);
            options.MaxBufferedBytes = maxFrameLength + FrameConstants.P_Head - 1;

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ChannelPipeline.Create(options));
        }

        [TestMethod]
        public void MaxBufferedBytesDerivationOverflow_ThrowsArgumentOutOfRangeException()
        {
            var options = NewOptions(int.MaxValue);
            options.MaxBufferedBytes = 0;
            options.ReadBufferSize = 1;

            var exception = Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => ChannelPipeline.Create(options));
            StringAssert.Contains(exception.Message, "推导");
        }

        [TestMethod]
        public void MaxBufferedBytesAboveIntMaxValue_ThrowsArgumentOutOfRangeException()
        {
            var options = NewOptions();
            options.MaxBufferedBytes = 3_000_000_000L;

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ChannelPipeline.Create(options));
        }

        [TestMethod]
        public void ChangingOptionsAfterCreate_DoesNotAffectEffectiveValues()
        {
            var options = NewOptions();
            options.QueueCapacity = 10;
            options.ResumeWatermark = 0;
            options.Guard = null;

            using (var pipeline = ChannelPipeline.Create(options))
            {
                Assert.AreEqual(5, pipeline.EffectiveResumeWatermark);
                Assert.AreSame(NullChannelGuard.Instance, pipeline.EffectiveGuard);

                options.QueueCapacity = 4;
                options.ResumeWatermark = 1;
                options.Guard = new RejectingGuard();
                options.MaxBufferedBytes = 32;

                Assert.AreEqual(5, pipeline.EffectiveResumeWatermark);
                Assert.AreEqual(1024 * 1024, pipeline.EffectiveMaxBufferedBytes);
                Assert.AreSame(NullChannelGuard.Instance, pipeline.EffectiveGuard);
            }
        }

        private static ChannelPipelineOptions NewOptions(int maxFrameLength = 1024)
        {
            return new ChannelPipelineOptions
            {
                Splitter = LengthFieldSplitter.Default(maxFrameLength)
            };
        }

        private sealed class RejectingGuard : IChannelGuard
        {
            public bool Allow(in ChannelTraffic traffic)
            {
                return false;
            }
        }
    }
}