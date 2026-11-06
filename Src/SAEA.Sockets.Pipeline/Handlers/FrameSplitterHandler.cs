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
*文件名： FrameSplitterHandler
*版本号： v26.11.5.1
*唯一标识：a4bc6590-44a3-4bdb-9aad-b54dceae78f9
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：纯链式场景的分帧处理器：没有 executor 泵时，由它在入站链首把字节流切成帧
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：纯链式场景的分帧处理器：没有 executor 泵时，由它在入站链首把字节流切成帧
*
*****************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 纯链式场景的分帧处理器：没有 executor 泵时，由它在入站链首把字节流切成帧。
    /// <para>
    /// 标准装配把 <see cref="IFrameSplitter"/> 放在 <see cref="ChannelPipelineOptions.Splitter"/>，
    /// 由泵在入队前分帧，链上不要再装本处理器，否则同一批字节会被拆两次。
    /// </para>
    /// <para>
    /// 输入是承载任意字节片段的 <see cref="PooledMessage"/>：片段被追加到内部累加器后，
    /// 原始条目立即归还；每个完整帧复制成独立的 <see cref="PooledMessage"/> 下传
    /// （下游通常是 <see cref="CodecDecoderHandler"/>），残余半包留在累加器等下一次输入。
    /// 处理器非线程安全，只能由单一读取协程串行调用；不再使用时 <see cref="Dispose"/>
    /// 归还累加器缓冲并重置分帧器。
    /// </para>
    /// </summary>
    public sealed class FrameSplitterHandler : InboundHandlerAdapter, IDisposable
    {
        private readonly IFrameSplitter _splitter;
        private readonly FrameAccumulator _accumulator = new FrameAccumulator();
        private int _disposed;

        /// <summary>以分帧器创建纯链式分帧处理器。</summary>
        /// <param name="splitter">把连续缓冲切成完整帧的分帧器，不能为 null。</param>
        public FrameSplitterHandler(IFrameSplitter splitter)
        {
            if (splitter == null)
                throw new ArgumentNullException(nameof(splitter));

            _splitter = splitter;
        }

        /// <summary>只接受 <see cref="PooledMessage"/>；把完整帧逐条下传，半包留在内部累加器。</summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="msg">任意字节片段，必须是 <see cref="PooledMessage"/>。</param>
        /// <returns>本次输入的所有完整帧处理完成后结束的任务。</returns>
        public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
        {
            var pooled = msg as PooledMessage;
            if (pooled == null)
                throw new InvalidOperationException("FrameSplitterHandler 的入参必须是 PooledMessage。");

            try
            {
                _accumulator.Append(pooled.Frame);
            }
            finally
            {
                pooled.Release();
            }

            while (true)
            {
                int consumed;
                ReadOnlyMemory<byte> frame;
                if (!_splitter.TrySplit(_accumulator.Buffered, out consumed, out frame))
                    return;

                // 先复制再消费：下传的是独立背衬，不与累加器缓冲共享内存。
                var bytes = frame.ToArray();
                if (consumed <= 0)
                    throw new FrameProtocolException("分帧器返回成功时必须消费至少 1 字节。");

                _accumulator.Consume(consumed);

                var message = new PooledMessage(bytes, bytes, bytes.Length, false);
                try
                {
                    await ctx.FireChannelReadAsync(message).ConfigureAwait(false);
                }
                finally
                {
                    // 纯链式没有 executor 的执行包收口，由生产者归还这一份基础引用。
                    message.Release();
                }
            }
        }

        /// <summary>归还累加器缓冲并重置分帧器；可重复调用。</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            _accumulator.Dispose();
            _splitter.Reset();
        }
    }
}