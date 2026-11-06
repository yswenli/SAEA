using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 单条连接独占的双向处理器链：入站从链首到链尾，出站从链尾到链首。
    /// 出站处理器必须构成链首的连续前缀，否则出站消息会跨过入站处理器。
    /// </summary>
    public sealed class ChannelPipeline : IChannelPipeline
    {
        /// <summary>双向链表节点；头尾哨兵的 <see cref="Handler"/> 为 null。</summary>
        internal sealed class Node
        {
            public IChannelHandler Handler;
            public Node Prev;
            public Node Next;
            public DefaultChannelHandlerContext Context;
            public bool IsInbound;
        }

        private readonly ChannelPipelineOptions _options;
        private readonly int _effectiveMaxBufferedBytes;
        private readonly int _effectiveResumeWatermark;
        private readonly IChannelGuard _guard;
        private readonly Node _head;
        private readonly Node _tail;
        private readonly object _gate = new object();

        private bool _outboundPrefixClosed;
        private IRawByteStream _stream;
        private ChannelPipelineExecutor _executor;
        private volatile bool _closed;
        private int _disposed;

        private ChannelPipeline(
            ChannelPipelineOptions options,
            int effectiveMaxBufferedBytes,
            int effectiveResumeWatermark,
            IChannelGuard guard)
        {
            _options = options;
            _effectiveMaxBufferedBytes = effectiveMaxBufferedBytes;
            _effectiveResumeWatermark = effectiveResumeWatermark;
            _guard = guard;

            _head = new Node();
            _tail = new Node();
            _head.Next = _tail;
            _tail.Prev = _head;
            _head.Context = new DefaultChannelHandlerContext(_head, this);
            _tail.Context = new DefaultChannelHandlerContext(_tail, this);
        }

        /// <summary>
        /// 创建一条空链。结构校验在创建时完成，不在运行期补做。
        /// </summary>
        public static ChannelPipeline Create(ChannelPipelineOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var splitter = options.Splitter;
            if (splitter == null)
                throw new ArgumentException("必须为 ChannelPipelineOptions.Splitter 指定分帧器。", nameof(options));

            if (options.QueueCapacity < 2)
                throw new ArgumentOutOfRangeException(nameof(options), options.QueueCapacity, "QueueCapacity 必须不小于 2。");

            if (options.MaxConcurrentMessages < 1)
                throw new ArgumentOutOfRangeException(nameof(options), options.MaxConcurrentMessages, "MaxConcurrentMessages 必须不小于 1。");

            var resumeWatermark = options.ResumeWatermark;
            if (resumeWatermark == 0)
            {
                resumeWatermark = Math.Max(1, options.QueueCapacity / 2);
            }
            else if (resumeWatermark < 0 || resumeWatermark >= options.QueueCapacity)
            {
                throw new ArgumentOutOfRangeException(nameof(options), resumeWatermark,
                    "ResumeWatermark 必须大于 0 且小于 QueueCapacity；取 0 表示自动使用 max(1, QueueCapacity / 2)。");
            }

            if (options.MessageTimeout < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(options), options.MessageTimeout, "MessageTimeout 不能为负。");

            if (options.DrainTimeout < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(options), options.DrainTimeout, "DrainTimeout 不能为负。");

            if (options.SmallFrameInlineThreshold < 0)
                throw new ArgumentOutOfRangeException(nameof(options), options.SmallFrameInlineThreshold, "SmallFrameInlineThreshold 不能为负。");

            if (options.EnvelopeCopyThreshold < 0)
                throw new ArgumentOutOfRangeException(nameof(options), options.EnvelopeCopyThreshold, "EnvelopeCopyThreshold 不能为负。");

            if (options.ReadBufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(options), options.ReadBufferSize, "ReadBufferSize 必须大于 0。");

            if (splitter.MaxFrameLength < 0)
                throw new ArgumentOutOfRangeException(nameof(options), splitter.MaxFrameLength, "Splitter.MaxFrameLength 不能为负。");

            var minimumBufferedBytes = (long)splitter.MaxFrameLength + FrameConstants.P_Head;
            int maxBufferedBytes;
            if (options.MaxBufferedBytes == 0)
            {
                var derived = minimumBufferedBytes + options.ReadBufferSize;
                if (derived > int.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(options), derived,
                        "MaxBufferedBytes 推导值 " + derived + " 超出 int 上限；推导公式为 Splitter.MaxFrameLength + P_Head + ReadBufferSize。");
                }

                maxBufferedBytes = (int)derived;
            }
            else
            {
                if (options.MaxBufferedBytes < minimumBufferedBytes)
                {
                    throw new ArgumentOutOfRangeException(nameof(options), options.MaxBufferedBytes,
                        "MaxBufferedBytes 必须不小于 Splitter.MaxFrameLength + P_Head（" + minimumBufferedBytes + "），否则最大合法帧无法完整缓冲。");
                }

                if (options.MaxBufferedBytes > int.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(options), options.MaxBufferedBytes,
                        "MaxBufferedBytes 超过 int 上限 " + int.MaxValue + "，有效值按 int 使用。");
                }

                maxBufferedBytes = (int)options.MaxBufferedBytes;
            }

            var guard = options.Guard ?? NullChannelGuard.Instance;

            var snapshot = new ChannelPipelineOptions
            {
                Splitter = splitter,
                QueueCapacity = options.QueueCapacity,
                MaxConcurrentMessages = options.MaxConcurrentMessages,
                ResumeWatermark = resumeWatermark,
                MessageTimeout = options.MessageTimeout,
                DrainPolicy = options.DrainPolicy,
                DrainTimeout = options.DrainTimeout,
                MaxBufferedBytes = maxBufferedBytes,
                ReadBufferSize = options.ReadBufferSize,
                SmallFrameInlineThreshold = options.SmallFrameInlineThreshold,
                EnvelopeCopyThreshold = options.EnvelopeCopyThreshold,
                Guard = guard
            };

            return new ChannelPipeline(snapshot, maxBufferedBytes, resumeWatermark, guard);
        }

        /// <summary>本链使用的配置快照；创建后与调用方传入的实例脱钩。</summary>
        internal ChannelPipelineOptions Options
        {
            get { return _options; }
        }

        /// <summary>未成帧残余字节的有效上限；<see cref="ChannelPipelineOptions.MaxBufferedBytes"/> 为 0 时已按公式推导。</summary>
        internal int EffectiveMaxBufferedBytes
        {
            get { return _effectiveMaxBufferedBytes; }
        }

        /// <summary>背压恢复低水位的有效值；0 已归一化为 max(1, QueueCapacity / 2)。</summary>
        internal int EffectiveResumeWatermark
        {
            get { return _effectiveResumeWatermark; }
        }

        /// <summary>生效的流量防护钩子；未配置时为 <see cref="NullChannelGuard.Instance"/>。</summary>
        internal IChannelGuard EffectiveGuard
        {
            get { return _guard; }
        }

        /// <summary>连接标识；未接入传输层时为 null。</summary>
        internal string ChannelId
        {
            get
            {
                var executor = _executor;
                if (executor != null)
                    return executor.Channel.Id;

                var stream = _stream;
                return stream == null ? null : stream.ChannelId;
            }
        }

        /// <summary>当前上下文的令牌来源；未接入执行器时为空令牌。</summary>
        internal CancellationToken CurrentCancellationToken
        {
            get
            {
                var executor = _executor;
                return executor == null ? CancellationToken.None : executor.CurrentToken;
            }
        }

        /// <summary>所属连接；未接入传输层时抛 <see cref="InvalidOperationException"/>。</summary>
        internal IChannel Channel
        {
            get
            {
                var executor = _executor;
                if (executor == null)
                    throw new InvalidOperationException("pipeline 尚未 Attach 到传输层。");

                return executor.Channel;
            }
        }

        /// <summary>链上处理器的快照，按链首到链尾排列。</summary>
        public IReadOnlyList<IChannelHandler> Handlers
        {
            get
            {
                lock (_gate)
                {
                    var handlers = new List<IChannelHandler>();
                    for (var node = _head.Next; node != _tail; node = node.Next)
                        handlers.Add(node.Handler);
                    return handlers;
                }
            }
        }

        /// <summary>
        /// 追加到链尾。入站处理器任何时候都能追加；出站处理器只能在链首仍为出站前缀时追加。
        /// </summary>
        public IChannelPipeline AddLast(IChannelHandler handler)
        {
            var isInbound = ClassifyHandler(handler);
            EnsureNotDisposed();
            HandlerRegistrationTable.Acquire(handler);

            try
            {
                lock (_gate)
                {
                    EnsureNotDisposed();

                    if (!isInbound && _outboundPrefixClosed)
                    {
                        throw new InvalidOperationException(
                            "出站 handler 必须构成链首连续前缀：不能在入站 handler 之后追加出站 handler，请改用 AddFirst 插到链首。");
                    }

                    LinkBefore(_tail, handler, isInbound);
                    RecalculateOutboundPrefix();
                }
            }
            catch
            {
                HandlerRegistrationTable.Release(handler);
                throw;
            }

            return this;
        }

        /// <summary>
        /// 插入链首。出站处理器总是可以插入（前缀向左扩展）；入站处理器只能在链首没有出站处理器时插入。
        /// </summary>
        public IChannelPipeline AddFirst(IChannelHandler handler)
        {
            var isInbound = ClassifyHandler(handler);
            EnsureNotDisposed();
            HandlerRegistrationTable.Acquire(handler);

            try
            {
                lock (_gate)
                {
                    EnsureNotDisposed();

                    if (isInbound && _head.Next != _tail && !_head.Next.IsInbound)
                    {
                        throw new InvalidOperationException(
                            "入站 handler 不能插到链首已有的出站 handler 之前：出站 handler 必须构成链首连续前缀。");
                    }

                    LinkBefore(_head.Next, handler, isInbound);
                    RecalculateOutboundPrefix();
                }
            }
            catch
            {
                HandlerRegistrationTable.Release(handler);
                throw;
            }

            return this;
        }

        /// <summary>
        /// 摘除首个与 <paramref name="handler"/> 引用相等的节点并归还注册计数。
        /// 不在链上时抛 <see cref="InvalidOperationException"/>。
        /// </summary>
        public IChannelPipeline Remove(IChannelHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_gate)
            {
                Node target = null;
                for (var node = _head.Next; node != _tail; node = node.Next)
                {
                    if (ReferenceEquals(node.Handler, handler))
                    {
                        target = node;
                        break;
                    }
                }

                if (target == null)
                {
                    throw new InvalidOperationException(
                        "处理器 " + handler.GetType().FullName + " 不在当前通道链上。");
                }

                Unlink(target);
                RecalculateOutboundPrefix();
                HandlerRegistrationTable.Release(handler);
            }

            return this;
        }

        /// <summary>入站唯一入口：从链首开始传播。</summary>
        public Task FireChannelReadAsync(object msg)
        {
            return FireChannelReadFromAsync(_head.Next, msg);
        }

        /// <summary>按链首到链尾的顺序逐个通知入站处理器；异常不由链吞掉，由调用方记录。</summary>
        public async Task FireChannelReadCompleteAsync()
        {
            var nodes = InboundNodesSnapshot();
            foreach (var node in nodes)
            {
                await ((IChannelInboundHandler)node.Handler).ChannelReadCompleteAsync(node.Context).ConfigureAwait(false);
            }
        }

        /// <summary>异常从链首开始向链尾传播。</summary>
        public Task FireExceptionCaughtAsync(Exception ex)
        {
            return FireExceptionCaughtFromAsync(_head.Next, ex);
        }

        /// <summary>
        /// 请求关闭连接，幂等。未接入执行器时只标记链已关闭；
        /// 接入后交给执行器执行完整收口，返回只表示已进入收口。
        /// </summary>
        public Task CloseAsync(string reason)
        {
            _closed = true;

            var executor = _executor;
            if (executor != null)
                return executor.BeginShutdown(reason);

            return Task.CompletedTask;
        }

        /// <summary>
        /// 摘除全部节点并归还注册计数，幂等。由收口流程在末端调用。
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            lock (_gate)
            {
                var node = _head.Next;
                while (node != _tail)
                {
                    var next = node.Next;
                    node.Prev = null;
                    node.Next = null;
                    HandlerRegistrationTable.Release(node.Handler);
                    node = next;
                }

                _head.Next = _tail;
                _tail.Prev = _head;
                _outboundPrefixClosed = false;
            }
        }

        /// <summary>由执行器在创建后调用，注入链头出口与收口入口。</summary>
        internal void Attach(IRawByteStream stream, ChannelPipelineExecutor executor)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (executor == null)
                throw new ArgumentNullException(nameof(executor));

            _stream = stream;
            _executor = executor;
        }

        /// <summary>
        /// 从 <paramref name="start"/> 起找到下一个入站节点并执行；普通异常转成 ExceptionCaught 后吞掉，
        /// 取消异常原样冒泡给执行包以便区分超时与收口。
        /// </summary>
        internal async Task FireChannelReadFromAsync(Node start, object msg)
        {
            var node = FindNextInbound(start);
            if (node == null)
                return;

            try
            {
                await ((IChannelInboundHandler)node.Handler).ChannelReadAsync(node.Context, msg).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                await FireExceptionCaughtFromAsync(node.Next, ex).ConfigureAwait(false);
            }
        }

        /// <summary>从 <paramref name="start"/> 起找到下一个入站节点并传递异常；不绕回已抛异常的处理器。</summary>
        internal Task FireExceptionCaughtFromAsync(Node start, Exception ex)
        {
            var node = FindNextInbound(start);
            if (node == null)
                return Task.CompletedTask;

            return ((IChannelInboundHandler)node.Handler).ChannelExceptionCaughtAsync(node.Context, ex);
        }

        /// <summary>沿 Next 方向找下一个入站节点；到链尾返回 null。</summary>
        internal static Node FindNextInbound(Node start)
        {
            for (var node = start; node != null; node = node.Next)
            {
                if (node.Handler != null && node.IsInbound)
                    return node;
            }

            return null;
        }

        /// <summary>沿 Prev 方向找下一个出站节点；越过链首返回 null。</summary>
        internal static Node FindPrevOutbound(Node start)
        {
            for (var node = start; node != null; node = node.Prev)
            {
                if (node.Handler != null && !node.IsInbound)
                    return node;
            }

            return null;
        }

        /// <summary>链头出站终点：唯一的发送出口，正常返回表示所有权已交给传输层。</summary>
        internal async Task HeadSinkWriteAsync(object msg)
        {
            var frame = msg as TransportFrame;
            if (frame == null)
            {
                throw new InvalidOperationException(
                    "出站链缺少 FrameEncoderHandler：链头只接受 TransportFrame。");
            }

            var stream = _stream;
            var executor = _executor;
            if (stream == null || executor == null)
                throw new InvalidOperationException("pipeline 尚未 Attach 到传输层。");

            if (_closed || !executor.IsActive)
                throw new ChannelClosedException("连接已进入收口，不能继续发送。");

            await stream.SendAsync(frame.Data, frame.Owner).ConfigureAwait(false);
        }

        private void EnsureNotDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(ChannelPipeline));
        }

        private static bool ClassifyHandler(IChannelHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var inbound = handler is IChannelInboundHandler;
            var outbound = handler is IChannelOutboundHandler;

            if (inbound && outbound)
            {
                throw new InvalidOperationException(
                    "处理器 " + handler.GetType().FullName + " 同时实现了入站与出站接口，一个实例只能挂在一个方向上。");
            }

            if (!inbound && !outbound)
            {
                throw new InvalidOperationException(
                    "处理器 " + handler.GetType().FullName + " 未实现 IChannelInboundHandler 或 IChannelOutboundHandler。");
            }

            return inbound;
        }

        private void LinkBefore(Node next, IChannelHandler handler, bool isInbound)
        {
            var node = new Node();
            node.Handler = handler;
            node.IsInbound = isInbound;
            node.Context = new DefaultChannelHandlerContext(node, this);
            node.Prev = next.Prev;
            node.Next = next;
            next.Prev.Next = node;
            next.Prev = node;
        }

        private static void Unlink(Node node)
        {
            node.Prev.Next = node.Next;
            node.Next.Prev = node.Prev;
            node.Prev = null;
            node.Next = null;
        }

        private void RecalculateOutboundPrefix()
        {
            var node = _head.Next;
            while (node != _tail && !node.IsInbound)
                node = node.Next;

            _outboundPrefixClosed = node != _tail;
        }

        private List<Node> InboundNodesSnapshot()
        {
            var nodes = new List<Node>();
            lock (_gate)
            {
                for (var node = _head.Next; node != _tail; node = node.Next)
                {
                    if (node.IsInbound)
                        nodes.Add(node);
                }
            }

            return nodes;
        }
    }
}