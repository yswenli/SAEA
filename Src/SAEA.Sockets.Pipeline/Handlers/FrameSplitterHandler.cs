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
        public FrameSplitterHandler(IFrameSplitter splitter)
        {
            if (splitter == null)
                throw new ArgumentNullException(nameof(splitter));

            _splitter = splitter;
        }

        /// <summary>只接受 <see cref="PooledMessage"/>；把完整帧逐条下传，半包留在内部累加器。</summary>
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