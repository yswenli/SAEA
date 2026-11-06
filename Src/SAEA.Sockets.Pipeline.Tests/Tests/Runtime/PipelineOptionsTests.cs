/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.Sockets.Pipeline.Tests.Tests.Runtime
*文件名： PipelineOptionsTests
*版本号： v26.11.5.1
*唯一标识：62046554-1463-4b90-abde-7102444b26ac
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：PipelineOptionsTests
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：PipelineOptionsTests
*
*****************************************************************************/
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Runtime
{
    /// <summary>
    /// 通道链配置校验：字段合法区间、派生值计算与创建后快照。
    /// </summary>
    [TestClass]
    public class PipelineOptionsTests
    {
        /// <summary>
        /// 传入 null 配置时 Create 抛 ArgumentNullException
        /// </summary>
        [TestMethod]
        public void NullOptions_ThrowsArgumentNullException()
        {
            Assert.ThrowsException<ArgumentNullException>(() => ChannelPipeline.Create(null));
        }

        /// <summary>
        /// 分帧器为 null 时 Create 抛 ArgumentException
        /// </summary>
        [TestMethod]
        public void NullSplitter_ThrowsArgumentException()
        {
            var options = NewOptions();
            options.Splitter = null;

            Assert.ThrowsException<ArgumentException>(() => ChannelPipeline.Create(options));
        }

        /// <summary>
        /// 队列容量小于 2 时 Create 抛 ArgumentOutOfRangeException
        /// </summary>
        [TestMethod]
        public void QueueCapacityBelowTwo_ThrowsArgumentOutOfRangeException()
        {
            var options = NewOptions();
            options.QueueCapacity = 1;

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ChannelPipeline.Create(options));
        }

        /// <summary>
        /// 最大并发消息数为 0 时 Create 抛 ArgumentOutOfRangeException
        /// </summary>
        [TestMethod]
        public void MaxConcurrentMessagesBelowOne_ThrowsArgumentOutOfRangeException()
        {
            var options = NewOptions();
            options.MaxConcurrentMessages = 0;

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ChannelPipeline.Create(options));
        }

        /// <summary>
        /// 恢复水位为负（-1）时 Create 抛 ArgumentOutOfRangeException
        /// </summary>
        [TestMethod]
        public void ResumeWatermarkNegative_ThrowsArgumentOutOfRangeException()
        {
            var options = NewOptions();
            options.QueueCapacity = 16;
            options.ResumeWatermark = -1;

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ChannelPipeline.Create(options));
        }

        /// <summary>
        /// 恢复水位等于队列容量（16）时不满足小于容量的约束，Create 抛 ArgumentOutOfRangeException
        /// </summary>
        [TestMethod]
        public void ResumeWatermarkAtOrAboveQueueCapacity_ThrowsArgumentOutOfRangeException()
        {
            var options = NewOptions();
            options.QueueCapacity = 16;
            options.ResumeWatermark = 16;

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ChannelPipeline.Create(options));
        }

        /// <summary>
        /// 单消息超时为负值时 Create 抛 ArgumentOutOfRangeException
        /// </summary>
        [TestMethod]
        public void MessageTimeoutNegative_ThrowsArgumentOutOfRangeException()
        {
            var options = NewOptions();
            options.MessageTimeout = TimeSpan.FromTicks(-1);

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ChannelPipeline.Create(options));
        }

        /// <summary>
        /// 排空超时为负值时 Create 抛 ArgumentOutOfRangeException
        /// </summary>
        [TestMethod]
        public void DrainTimeoutNegative_ThrowsArgumentOutOfRangeException()
        {
            var options = NewOptions();
            options.DrainTimeout = TimeSpan.FromTicks(-1);

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ChannelPipeline.Create(options));
        }

        /// <summary>
        /// 小帧内联阈值为负时 Create 抛 ArgumentOutOfRangeException
        /// </summary>
        [TestMethod]
        public void SmallFrameInlineThresholdNegative_ThrowsArgumentOutOfRangeException()
        {
            var options = NewOptions();
            options.SmallFrameInlineThreshold = -1;

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ChannelPipeline.Create(options));
        }

        /// <summary>
        /// 信封拷贝阈值为负时 Create 抛 ArgumentOutOfRangeException
        /// </summary>
        [TestMethod]
        public void EnvelopeCopyThresholdNegative_ThrowsArgumentOutOfRangeException()
        {
            var options = NewOptions();
            options.EnvelopeCopyThreshold = -1;

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ChannelPipeline.Create(options));
        }

        /// <summary>
        /// 读缓冲大小为 0 时 Create 抛 ArgumentOutOfRangeException
        /// </summary>
        [TestMethod]
        public void ReadBufferSizeZero_ThrowsArgumentOutOfRangeException()
        {
            var options = NewOptions();
            options.ReadBufferSize = 0;

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ChannelPipeline.Create(options));
        }

        /// <summary>
        /// 恢复水位为 0 时归一化为队列容量的一半（容量 10 得 5、容量 2 得 1）
        /// </summary>
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

        /// <summary>
        /// 最大缓冲字节为 0 时按最大帧长加包头与读缓冲大小推导（128 + P_Head + 4096）
        /// </summary>
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

        /// <summary>
        /// 最大缓冲字节小于「最大帧长 + 包头」时 Create 抛 ArgumentOutOfRangeException
        /// </summary>
        [TestMethod]
        public void MaxBufferedBytesBelowMaximumFrame_ThrowsArgumentOutOfRangeException()
        {
            const int maxFrameLength = 1024;

            var options = NewOptions(maxFrameLength);
            options.MaxBufferedBytes = maxFrameLength + FrameConstants.P_Head - 1;

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ChannelPipeline.Create(options));
        }

        /// <summary>
        /// 推导最大缓冲字节时发生溢出则抛 ArgumentOutOfRangeException，且异常信息包含「推导」
        /// </summary>
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

        /// <summary>
        /// 最大缓冲字节超过 int 上限（30 亿）时 Create 抛 ArgumentOutOfRangeException
        /// </summary>
        [TestMethod]
        public void MaxBufferedBytesAboveIntMaxValue_ThrowsArgumentOutOfRangeException()
        {
            var options = NewOptions();
            options.MaxBufferedBytes = 3_000_000_000L;

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ChannelPipeline.Create(options));
        }

        /// <summary>
        /// Create 时对配置做快照：之后修改队列容量、恢复水位、守卫与最大缓冲字节都不影响已生效值（守卫缺省为 NullChannelGuard、最大缓冲默认 1MB）
        /// </summary>
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