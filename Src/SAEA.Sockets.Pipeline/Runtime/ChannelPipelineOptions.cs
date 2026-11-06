/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.Sockets.Pipeline
*文件名： ChannelPipelineOptions
*版本号： v26.11.5.1
*唯一标识：f337194a-d2a5-4d56-b1d7-5512f4cc5bd3
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：每连接的通道链配置
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：每连接的通道链配置
*
*****************************************************************************/
using System;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 每连接的通道链配置。创建后由 <see cref="ChannelPipeline.Create"/> 校验并拷贝，
    /// 运行期修改本实例不影响已创建的链。
    /// </summary>
    public sealed class ChannelPipelineOptions
    {
        /// <summary>泵分帧使用的分帧器；必填。</summary>
        public IFrameSplitter Splitter { get; set; }

        /// <summary>每连接有界消息队列容量。</summary>
        public int QueueCapacity { get; set; } = 1024;

        /// <summary>每连接同时在途的消息上限。</summary>
        public int MaxConcurrentMessages { get; set; } = 16;

        /// <summary>背压恢复的低水位；0 表示自动取队列容量的一半。</summary>
        public int ResumeWatermark { get; set; } = 0;

        /// <summary>单条消息超时；<see cref="TimeSpan.Zero"/> 表示不超时。</summary>
        public TimeSpan MessageTimeout { get; set; } = TimeSpan.Zero;

        /// <summary>断开收口时对在途消息的处理策略。</summary>
        public DisconnectDrainPolicy DrainPolicy { get; set; } = DisconnectDrainPolicy.CancelPending;

        /// <summary>仅 <see cref="DisconnectDrainPolicy.DrainInFlight"/> 生效的等待上限；零表示无限等待。</summary>
        public TimeSpan DrainTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>单连接未成帧残余字节上限；0 表示按分帧器最大帧长与读缓冲推导。</summary>
        public long MaxBufferedBytes { get; set; } = 1024 * 1024;

        /// <summary>读取缓冲大小，用于推导未成帧残余上限；不限制单次读取返回长度。</summary>
        public int ReadBufferSize { get; set; } = 64 * 1024;

        /// <summary>小于此长度的帧按精确分配处理，不走内存池。</summary>
        public int SmallFrameInlineThreshold { get; set; } = 512;

        /// <summary>信封负载拷贝告警阈值；超过只记录告警，不改变拷贝方式。</summary>
        public int EnvelopeCopyThreshold { get; set; } = 4 * 1024;

        /// <summary>连接级流量防护钩子；未配置时放行全部流量。</summary>
        public IChannelGuard Guard { get; set; } = NullChannelGuard.Instance;
    }
}