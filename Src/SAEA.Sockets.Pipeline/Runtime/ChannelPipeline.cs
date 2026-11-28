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
*文件名： ChannelPipeline
*版本号： v26.11.5.1
*唯一标识：d2dde878-ef60-4249-bad6-5a51399a1b81
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：单条连接独占的双向处理器链：入站从链首到链尾，出站从链尾到链首
*
*=====================================================================
*修改标记
*修改时间：2026/11/28 11:56:11
*修改人： yswenli
*版本号： v26.11.27.1
*描述：配置快照同步 RemoteEofDrainTimeout，供执行器按独立上限控制对端 EOF 排空
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SAEA.Common;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 单条连接独占的双向处理器链：入站从链首到链尾，出站从链尾到链首。
    /// 出站处理器必须构成链首的连续前缀，否则出站消息会跨过入站处理器。
    /// </summary>
    public sealed class ChannelPipeline : IChannelPipeline
    {
        private const int CloseOpen = 0;
        private const int CloseDrainingWrites = 1;
        private const int CloseClosing = 2;

        /// <summary>CloseAfterWriteAsync 在 DrainTimeout 为零值时使用的默认等待上限。</summary>
        internal static readonly TimeSpan CloseAfterWriteDefaultDrainTimeout = TimeSpan.FromSeconds(5);

        /// <summary>双向链表节点；头尾哨兵的 <see cref="Handler"/> 为 null。</summary>
        internal sealed class Node
        {
            public IChannelHandler Handler;
            public Node Prev;
            public Node Next;
            public DefaultChannelHandlerContext Context;
            public bool IsInbound;

            /// <summary>是否曾经挂上过链；用于区分 HandlerAdded 阶段的半初始化节点与正常摘链后的节点。</summary>
            public bool LinkedOnce;

            // 每次派发复用一个视图即可：同节点不会被同一次派发重入，槽被并发占用时退回新建，不做无界池。
            private DefaultChannelHandlerContext _pooledView;

            /// <summary>取走停放中的调用视图；槽为空时返回 null，由调用方新建。</summary>
            /// <returns>可复用的视图，或 null。</returns>
            internal DefaultChannelHandlerContext RentView()
            {
                return Interlocked.Exchange(ref _pooledView, null);
            }

            /// <summary>归还调用视图；调用方必须先使视图失效。</summary>
            /// <param name="view">本次调用租用的视图。</param>
            internal void ReturnView(DefaultChannelHandlerContext view)
            {
                // 槽已被并发占用则直接丢弃，交给 GC，避免池无界增长。
                Interlocked.CompareExchange(ref _pooledView, view, null);
            }
        }

        private readonly ChannelPipelineOptions _options;
        private readonly int _effectiveMaxBufferedBytes;
        private readonly int _effectiveResumeWatermark;
        private readonly IChannelGuard _guard;
        private readonly Node _head;
        private readonly Node _tail;
        private readonly object _gate = new object();

        // 出站视图共享同一个无状态守卫：它只把成员转发给真实链、WriteAsync 恒抛，按连接惰性建一次即可。
        private IChannelPipeline _outboundGuard;

        // 业务写计数、关闭状态与等待信号必须共用同一把锁，才能在同一个同步边界内完成
        // 「停止接纳 + 快照并等待」。
        private readonly object _outboundGate = new object();
        private readonly HashSet<OutboundOperation> _pendingOperations = new HashSet<OutboundOperation>();

        private bool _outboundPrefixClosed;
        private bool _frozen;
        private IRawByteStream _stream;
        private ChannelPipelineExecutor _executor;
        private volatile bool _closed;
        private int _disposed;
        private int _activeDispatched;
        private int _inactiveDispatched;
        private int _closeState;
        private TaskCompletionSource<bool> _drainSignal;
        private TaskCompletionSource<bool> _closeAfterWriteCompletion;
        private Task _closeAfterWriteTask;

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
            _head.LinkedOnce = true;
            _tail.LinkedOnce = true;
            _head.Context = new DefaultChannelHandlerContext(_head, this);
            _tail.Context = new DefaultChannelHandlerContext(_tail, this);
        }

        /// <summary>
        /// 创建一条空链。结构校验在创建时完成，不在运行期补做。
        /// </summary>
        /// <param name="options">链配置，分帧器等必填项在创建时校验，不能为 null。</param>
        /// <returns>已通过结构校验的空链。</returns>
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

            if (options.DispatchMode != ChannelDispatchMode.Queued && options.DispatchMode != ChannelDispatchMode.Inline)
                throw new ArgumentOutOfRangeException(nameof(options), options.DispatchMode, "DispatchMode 只支持 Queued 或 Inline。");

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
                DispatchMode = options.DispatchMode,
                EnvelopeCopyThreshold = options.EnvelopeCopyThreshold,
                Guard = guard,
                RequireTerminalHandler = options.RequireTerminalHandler,
                SplitterSwitchTimeout = options.SplitterSwitchTimeout,
                MaxPendingWrites = options.MaxPendingWrites,
                DrainOnRemoteEof = options.DrainOnRemoteEof,
                RemoteEofDrainTimeout = options.RemoteEofDrainTimeout
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

        /// <summary>
        /// 出站回调内暴露给处理器的链视图，全链共用一个实例：除业务写入口外全部转发到真实链。
        /// 守卫无状态，惰性创建；并发首次调用时以先写入者为准，落败的临时实例直接丢弃。
        /// </summary>
        internal IChannelPipeline OutboundGuard
        {
            get
            {
                var guard = Volatile.Read(ref _outboundGuard);
                if (guard != null)
                    return guard;

                var created = new OutboundGuardPipeline(this);
                return Interlocked.CompareExchange(ref _outboundGuard, created, null) ?? created;
            }
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

        /// <summary>尚未完成的业务写数量，仅供测试与诊断读取。</summary>
        internal int PendingWriteCount
        {
            get
            {
                lock (_outboundGate)
                {
                    return _pendingOperations.Count;
                }
            }
        }

        /// <summary>当前关闭状态：0 Open、1 DrainingWrites、2 Closing，仅供测试与诊断读取。</summary>
        internal int CloseState
        {
            get { return Volatile.Read(ref _closeState); }
        }

        /// <summary>连接是否仍处于 Open，尚未进入任何收口流程。</summary>
        internal bool IsOpen
        {
            get { return Volatile.Read(ref _closeState) == CloseOpen; }
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
        /// <param name="handler">待追加的处理器实例，不能为 null。</param>
        /// <returns>当前链，便于链式调用。</returns>
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
                    EnsureNotFrozen();

                    if (!isInbound && _outboundPrefixClosed)
                    {
                        throw new InvalidOperationException(
                            "出站 handler 必须构成链首连续前缀：不能在入站 handler 之后追加出站 handler，请改用 AddFirst 插到链首。");
                    }

                    var node = CreateNode(handler, isInbound);

                    // 先建节点与 ctx、调 HandlerAdded，成功后才挂链；失败时链上不会留下半挂节点。
                    handler.HandlerAdded(node.Context);
                    LinkBefore(_tail, node);
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
        /// <param name="handler">待插入的处理器实例，不能为 null。</param>
        /// <returns>当前链，便于链式调用。</returns>
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
                    EnsureNotFrozen();

                    if (isInbound && _head.Next != _tail && !_head.Next.IsInbound)
                    {
                        throw new InvalidOperationException(
                            "入站 handler 不能插到链首已有的出站 handler 之前：出站 handler 必须构成链首连续前缀。");
                    }

                    var node = CreateNode(handler, isInbound);

                    // 先建节点与 ctx、调 HandlerAdded，成功后才挂链；失败时链上不会留下半挂节点。
                    handler.HandlerAdded(node.Context);
                    LinkBefore(_head.Next, node);
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
        /// <param name="handler">待摘除的处理器实例，不能为 null。</param>
        /// <returns>当前链，便于链式调用。</returns>
        public IChannelPipeline Remove(IChannelHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Node target;

            lock (_gate)
            {
                EnsureNotFrozen();

                target = null;
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

                // 先摘链再回调：HandlerRemoved 抛异常也不能影响链结构。
                Unlink(target);
                RecalculateOutboundPrefix();
                HandlerRegistrationTable.Release(handler);
            }

            try
            {
                handler.HandlerRemoved(target.Context);
            }
            catch (Exception ex)
            {
                LogHelper.Warn("处理器摘链回调异常:" + target.Context.ChannelId, ex);
            }

            return this;
        }

        /// <summary>
        /// 入站唯一入口：从链首开始传播。消息按执行器持有处理，链上边界不代它释放；
        /// 普通异常在对应节点下游通知一次后继续冒泡，不再由链吞掉。
        /// </summary>
        /// <param name="msg">待处理的入站消息。</param>
        /// <returns>整条入站链执行完成后结束的任务。</returns>
        public Task FireChannelReadAsync(object msg)
        {
            return FireChannelReadFromAsync(_head.Next, msg, InboundDispatch.CreateExecutorOwned(null));
        }

        /// <summary>按链首到链尾的顺序逐个通知入站处理器；异常不由链吞掉，由调用方记录。</summary>
        /// <returns>所有入站处理器通知完成后结束的任务。</returns>
        public async Task FireChannelReadCompleteAsync()
        {
            var nodes = InboundNodesSnapshot();
            foreach (var node in nodes)
            {
                await ((IChannelInboundHandler)node.Handler).ChannelReadCompleteAsync(node.Context).ConfigureAwait(false);
            }
        }

        /// <summary>异常从链首开始向链尾传播。</summary>
        /// <param name="ex">待传播的异常。</param>
        /// <returns>异常处理完成后结束的任务。</returns>
        public Task FireExceptionCaughtAsync(Exception ex)
        {
            return FireExceptionCaughtFromAsync(_head.Next, ex);
        }

        /// <summary>
        /// 业务写唯一入口：领取一次操作与写计数后，从出站前缀最右侧节点启动。
        /// 关闭后或待写项超限时直接拒绝并补偿未移交的原始帧。
        /// </summary>
        /// <param name="msg">待发送的业务消息；所有权随链移交。</param>
        /// <returns>该次写完成、失败或取消后结束的公共任务。</returns>
        public Task WriteAsync(object msg)
        {
            OutboundOperation operation = null;
            Exception rejection = null;

            lock (_outboundGate)
            {
                if (_closeState != CloseOpen)
                {
                    rejection = new ChannelClosedException("连接已进入收口，不能继续发送。");
                }
                else if (Volatile.Read(ref _disposed) != 0)
                {
                    rejection = new ObjectDisposedException(nameof(ChannelPipeline));
                }
                else if (_options.MaxPendingWrites > 0 && _pendingOperations.Count >= _options.MaxPendingWrites)
                {
                    rejection = new InvalidOperationException(
                        "出站待写项数已达到上限 MaxPendingWrites=" + _options.MaxPendingWrites + "，本次写被拒绝。");
                }
                else
                {
                    operation = new OutboundOperation(this, msg);
                    _pendingOperations.Add(operation);
                }
            }

            if (rejection != null)
            {
                CompensateRawFrame(msg);
                return Task.FromException(rejection);
            }

            // 公共完成源驱动调用方等待；链执行任务本身不把异常抛给未观察方。
            _ = RunOutboundAsync(operation);
            return operation.Task;
        }

        /// <summary>
        /// 请求关闭连接，幂等。处于 draining 时加入现有等待而不是抢占；
        /// 进入 Closing 后为 no-op。返回只表示已进入收口流程，不等待资源回收。
        /// </summary>
        /// <param name="reason">关闭原因，写入日志用于区分收口来源。</param>
        /// <returns>进入收口流程后即完成的任务，不等待资源回收。</returns>
        public Task CloseAsync(string reason)
        {
            lock (_outboundGate)
            {
                if (_closeState == CloseClosing)
                    return Task.CompletedTask;

                if (_closeState == CloseDrainingWrites)
                {
                    // 已提交的写仍在交接：普通关闭只能加入等待，不能抢先置 _closed 让它们失败。
                    return _closeAfterWriteTask ?? Task.CompletedTask;
                }

                _closeState = CloseClosing;
                _closed = true;
            }

            return BeginShutdown(reason);
        }

        /// <summary>
        /// 等已提交的业务写全部完成后收口。停止接纳与快照等待在同一同步边界内完成，
        /// 重复调用复用同一完成源；进入 Closing 后为 no-op。
        /// </summary>
        /// <param name="reason">关闭原因，写入日志用于区分关闭来源。</param>
        /// <returns>已提交的写全部交接、或等待超时并强制完成未移交项后结束的任务。</returns>
        internal Task CloseAfterWriteAsync(string reason)
        {
            var executor = _executor;
            if (executor != null && !executor.IsActive)
            {
                // ChannelInactiveAsync 等收口后调用：连接已在关闭流程中，不再起第二个等待。
                lock (_outboundGate)
                {
                    _closeState = CloseClosing;
                    _closed = true;
                }

                return Task.CompletedTask;
            }

            TaskCompletionSource<bool> completion;
            lock (_outboundGate)
            {
                if (_closeState == CloseClosing)
                    return Task.CompletedTask;

                if (_closeState == CloseDrainingWrites)
                    return _closeAfterWriteTask;

                _closeState = CloseDrainingWrites;
                completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _closeAfterWriteCompletion = completion;
                _closeAfterWriteTask = completion.Task;
            }

            _ = RunCloseAfterWriteAsync(reason, completion);
            return completion.Task;
        }

        /// <summary>把链发起的关闭交给执行器；未接入传输层时只落地关闭标记。</summary>
        private Task BeginShutdown(string reason)
        {
            var executor = _executor;
            return executor != null ? executor.BeginShutdown(reason) : Task.CompletedTask;
        }

        /// <summary>
        /// 从最右侧出站节点启动一次业务写。超时或移除已完成本项时禁止再调下游；
        /// 真实链任务的正常完成、失败或取消都只通过唯一 <see cref="OutboundOperation.TryComplete"/> 落地。
        /// </summary>
        private async Task RunOutboundAsync(OutboundOperation operation)
        {
            Exception failure = null;
            operation.MarkActive();

            try
            {
                Node start;
                lock (_gate)
                {
                    start = FindPrevOutbound(_tail);
                }

                // 串行器作为最右侧出站节点时，由它在真正出队时才把操作推进到 HandedOff；
                // 这样排队期间的操作仍能按「未移交」被收口超时或摘链清理，不会误跳过 RawFrame 补偿。
                var serialized = start != null && start.Handler is OutboundSerializingHandler;
                if (!serialized && !operation.TryMarkHandedOff())
                    return;

                if (start == null)
                    await HeadSinkWriteAsync(operation.Message).ConfigureAwait(false);
                else
                    await InvokeOutboundAsync(start, operation, operation.Message).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                operation.ClearActive();
                operation.TryComplete(failure);
            }
        }

        /// <summary>
        /// 调用一个出站节点并承担本边界的所有权补偿：
        /// 正常返回或抛错时若未通过 <see cref="IChannelHandlerContext.WriteAsync"/> 移交原始帧，就在这里释放一次；
        /// 已移交的由子节点或链头/传输层负责，本节点不重复处理。
        /// </summary>
        /// <param name="node">本次要调用的出站节点。</param>
        /// <param name="operation">当前业务写操作。</param>
        /// <param name="msg">本次边界持有的消息。</param>
        internal async Task InvokeOutboundAsync(Node node, OutboundOperation operation, object msg)
        {
            var view = node.RentView();
            if (view == null)
                view = new DefaultChannelHandlerContext(node, this, operation, msg);
            else
                view.Reset(null, msg, operation, OutboundGuard);

            try
            {
                await ((IChannelOutboundHandler)node.Handler).WriteAsync(view, msg).ConfigureAwait(false);

                if (view.OutboundForwarded == 0)
                    CompensateRawFrame(msg);
            }
            catch
            {
                if (view.OutboundForwarded == 0)
                    CompensateRawFrame(msg);

                throw;
            }
            finally
            {
                // 必须在回调及其交接任务彻底结束后才失效并归还，否则下游或排队项会拿到已失效的视图。
                view.InvalidateView();
                node.ReturnView(view);
            }
        }

        /// <summary>业务写完成时撤回计数；归零时唤醒关闭等待者。</summary>
        /// <param name="operation">已完成的操作。</param>
        internal void OnOutboundCompleted(OutboundOperation operation)
        {
            lock (_outboundGate)
            {
                if (!_pendingOperations.Remove(operation))
                    return;

                if (_pendingOperations.Count == 0 && _drainSignal != null)
                    _drainSignal.TrySetResult(true);
            }
        }

        /// <summary>
        /// 等已提交写计数归零。信号必须在同一锁内先建好，避免「归零发生在建信号之前」而漏唤醒。
        /// </summary>
        private async Task<bool> WaitForPendingWritesAsync(TimeSpan timeout)
        {
            Task pending;
            lock (_outboundGate)
            {
                if (_drainSignal == null || _drainSignal.Task.IsCompleted)
                    _drainSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                if (_pendingOperations.Count == 0)
                {
                    _drainSignal.TrySetResult(true);
                    return true;
                }

                pending = _drainSignal.Task;
            }

            var delay = Task.Delay(timeout);
            var winner = await Task.WhenAny(pending, delay).ConfigureAwait(false);
            return winner == pending;
        }

        /// <summary>关闭等待超时：只强制完成仍处于 Queued 的项，已移交项交给下游迟完成清理。</summary>
        private void ForceCompleteQueuedWrites(Exception error)
        {
            OutboundOperation[] snapshot;
            lock (_outboundGate)
            {
                snapshot = new OutboundOperation[_pendingOperations.Count];
                _pendingOperations.CopyTo(snapshot);
            }

            for (var i = 0; i < snapshot.Length; i++)
                snapshot[i].TryAbortQueued(error);
        }

        /// <summary>CloseAfterWriteAsync 的后台序列：等待计数归零或超时，然后进入 Closing。</summary>
        private async Task RunCloseAfterWriteAsync(string reason, TaskCompletionSource<bool> completion)
        {
            try
            {
                var timeout = _options.DrainTimeout;
                if (timeout <= TimeSpan.Zero)
                    timeout = CloseAfterWriteDefaultDrainTimeout;

                var drained = await WaitForPendingWritesAsync(timeout).ConfigureAwait(false);
                if (!drained)
                {
                    LogHelper.Warn("出站写收口等待超时，强制完成未移交项:" + ChannelId, null);
                    ForceCompleteQueuedWrites(new ChannelClosedException("出站写在收口等待超时前未完成。"));
                }
            }
            catch (Exception ex)
            {
                LogHelper.Warn("出站写收口等待异常:" + ChannelId, ex);
            }
            finally
            {
                lock (_outboundGate)
                {
                    _closeState = CloseClosing;
                    _closed = true;
                }

                try
                {
                    await BeginShutdown(reason).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogHelper.Warn("出站写收口启动失败:" + ChannelId, ex);
                }

                completion.TrySetResult(true);
            }
        }

        /// <summary>未成功移交时释放 <see cref="RawFrame"/>；普通业务对象不代管。</summary>
        private static void CompensateRawFrame(object msg)
        {
            var raw = msg as RawFrame;
            if (raw == null)
                return;

            try
            {
                raw.Dispose();
            }
            catch (Exception ex)
            {
                LogHelper.Warn("原始帧补偿释放失败:" + (msg == null ? "(null)" : msg.GetType().FullName), ex);
            }
        }

        /// <summary>
        /// 摘除全部节点并归还注册计数，幂等。由收口流程在末端调用。
        /// 节点先摘链再回调 <see cref="IChannelHandler.HandlerRemoved"/>，回调异常只记录。
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            var removed = new List<Node>();
            lock (_gate)
            {
                var node = _head.Next;
                while (node != _tail)
                {
                    var next = node.Next;
                    Unlink(node);
                    HandlerRegistrationTable.Release(node.Handler);
                    removed.Add(node);
                    node = next;
                }

                RecalculateOutboundPrefix();
            }

            foreach (var node in removed)
            {
                try
                {
                    node.Handler.HandlerRemoved(node.Context);
                }
                catch (Exception ex)
                {
                    LogHelper.Warn("处理器摘链回调异常:" + node.Context.ChannelId, ex);
                }
            }
        }

        /// <summary>
        /// 冻结链结构：由执行器在启动接收泵之前调用，之后禁止增删节点。
        /// 结构校验依赖启动那一刻的链形状，启动后仍可改链会让校验只在一瞬间成立。
        /// </summary>
        internal void Freeze()
        {
            lock (_gate)
            {
                _frozen = true;
            }
        }

        /// <summary>
        /// 按链首到链尾的顺序逐条派发活动通知；幂等，单个处理器异常只记录，不阻断后续处理器。
        /// </summary>
        internal async Task FireChannelActiveAsync()
        {
            if (Interlocked.CompareExchange(ref _activeDispatched, 1, 0) != 0)
                return;

            var nodes = InboundNodesSnapshot();
            foreach (var node in nodes)
            {
                try
                {
                    await ((IChannelInboundHandler)node.Handler).ChannelActiveAsync(node.Context).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogHelper.Warn("处理器 ChannelActive 回调异常:" + node.Context.ChannelId, ex);
                }
            }
        }

        /// <summary>
        /// 按链首到链尾的顺序逐条派发失活通知；幂等，单个处理器异常只记录，不阻断后续处理器。
        /// </summary>
        internal async Task FireChannelInactiveAsync()
        {
            if (Interlocked.CompareExchange(ref _inactiveDispatched, 1, 0) != 0)
                return;

            var nodes = InboundNodesSnapshot();
            foreach (var node in nodes)
            {
                try
                {
                    await ((IChannelInboundHandler)node.Handler).ChannelInactiveAsync(node.Context).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogHelper.Warn("处理器 ChannelInactive 回调异常:" + node.Context.ChannelId, ex);
                }
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
        /// 从 <paramref name="start"/> 起找到下一个入站节点并执行。
        /// 普通异常从该节点下游通知一次后继续冒泡；<see cref="OperationCanceledException"/> 不通知，
        /// 但仍按「未成功转发」口径补偿并原样冒泡。调用方看到异常时必须按边界补偿规则处理消息所有权。
        /// </summary>
        /// <param name="start">开始查找入站节点的位置（含）。</param>
        /// <param name="msg">本次边界要交给下一个入站节点的消息。</param>
        /// <param name="dispatch">本次边界的所有权与异常通知状态，不能为 null。</param>
        /// <returns>该边界处理完成后结束的任务。</returns>
        internal async Task FireChannelReadFromAsync(Node start, object msg, InboundDispatch dispatch)
        {
            var node = FindNextInbound(start);
            if (node == null)
            {
                // 链尾没有下一个入站节点：链持有的消息在这里消费，记录 Debug 避免缺少 terminal 时静默泄漏。
                ReleaseChainOwned(dispatch, msg, "入站链尾没有下一个入站节点");
                return;
            }

            // 调用绑定视图：绑定当前节点与本次派发，回调结束后失效并归还节点槽，跨回调保存也不能继续转发。
            var view = node.RentView();
            if (view == null)
                view = new DefaultChannelHandlerContext(node, this, dispatch, msg);
            else
                view.Reset(dispatch, msg, null, this);

            try
            {
                await ((IChannelInboundHandler)node.Handler).ChannelReadAsync(view, msg).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                CompensateUnforwarded(dispatch, msg);

                if (!(ex is OperationCanceledException) && !dispatch.ExceptionNotified)
                {
                    dispatch.MarkExceptionNotified();
                    try
                    {
                        await FireExceptionCaughtFromAsync(node.Next, ex).ConfigureAwait(false);
                    }
                    catch (Exception notifyError)
                    {
                        // 通知自身失败只记录，不能替换或吞掉原始异常。
                        LogHelper.Warn("入站异常通知失败:" + ChannelId, notifyError);
                    }
                }

                throw;
            }
            finally
            {
                // 必须在回调彻底结束后才失效并归还：下游可能还沿着派生派发在跑，不能用同一实例。
                view.InvalidateView();
                node.ReturnView(view);
            }
        }

        /// <summary>
        /// 未成功转发即抛错或取消时，链持有的可释放消息由本边界补偿一次；
        /// 已成功交给下游的由下游边界负责，执行器持有的原始帧始终由执行器释放。
        /// </summary>
        /// <param name="dispatch">当前边界状态。</param>
        /// <param name="msg">当前边界持有的消息。</param>
        internal static void CompensateUnforwarded(InboundDispatch dispatch, object msg)
        {
            if (dispatch == null || !dispatch.ChainOwned || dispatch.Forwarded || msg == null)
                return;

            var disposable = msg as IDisposable;
            if (disposable == null)
                return;

            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                LogHelper.Warn("入站消息补偿释放失败:" + msg.GetType().FullName, ex);
            }
        }

        /// <summary>链尾释放链持有的消息并记录 Debug；执行器持有的原始帧不在此释放。</summary>
        private void ReleaseChainOwned(InboundDispatch dispatch, object msg, string where)
        {
            if (dispatch == null || !dispatch.ChainOwned || dispatch.Forwarded || msg == null)
                return;

            LogHelper.Debug(where + "，释放链持有消息:" + ChannelId + " " + msg.GetType().FullName);
            CompensateUnforwarded(dispatch, msg);
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
            var stream = _stream;
            var executor = _executor;

            // 关闭判定先于类型判定：连接已收口时任何写出站都应按关闭处理，
            // 不能因为调用方持有的是已摘链节点的旧上下文而报成"链配错"。
            // 未 Attach 时没有执行器可判定，交给下面的 Attach 校验。
            if (executor != null && (_closed || !executor.IsActive))
            {
                // 关闭判定先于类型判定：原始帧仍在链手里，必须先补偿。
                CompensateRawFrame(msg);
                throw new ChannelClosedException("连接已进入收口，不能继续发送。");
            }

            var raw = msg as RawFrame;
            if (raw != null)
            {
                if (stream == null || executor == null)
                {
                    // 尚未接管就失败：原始帧仍归链所有，先补偿再抛。
                    CompensateRawFrame(raw);
                    throw new InvalidOperationException("pipeline 尚未 Attach 到传输层。");
                }

                // CAS 必须在 SendAsync 之前；成功之后所有权归传输层，即使发送抛异常也不得再释放。
                if (!raw.TryMarkHandedOff())
                {
                    CompensateRawFrame(raw);
                    throw new InvalidOperationException(
                        "同一个 RawFrame 只能移交给传输层一次；重复移交会产生重复帧或重复释放。");
                }

                try
                {
                    await stream.SendAsync(raw.Bytes, raw.Owner).ConfigureAwait(false);
                }
                catch
                {
                    // 所有权已移交传输层，这里只通知执行器写方向失效，不做帧补偿。
                    executor.NotifyWriteFailure();
                    throw;
                }

                return;
            }

            var frame = msg as TransportFrame;
            if (frame == null)
            {
                throw new InvalidOperationException(
                    "链头只接受 TransportFrame 或 RawFrame；请确认已装配 FrameEncoderHandler，"
                    + "或由业务编码器直接产出 RawFrame。");
            }

            if (stream == null || executor == null)
                throw new InvalidOperationException("pipeline 尚未 Attach 到传输层。");

            try
            {
                await stream.SendAsync(frame.Data, frame.Owner).ConfigureAwait(false);
            }
            catch
            {
                // 所有权已移交传输层，这里只通知执行器写方向失效，不做帧补偿。
                executor.NotifyWriteFailure();
                throw;
            }
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

        private void EnsureNotFrozen()
        {
            if (_frozen)
            {
                throw new InvalidOperationException(
                    "通道链已启动并冻结，不能在运行期增删处理器；需要换链请在启动前完成装配。");
            }
        }

        /// <summary>创建未挂链的节点与上下文；此时节点尚未连入链表，不能传播消息或写出。</summary>
        private Node CreateNode(IChannelHandler handler, bool isInbound)
        {
            var node = new Node();
            node.Handler = handler;
            node.IsInbound = isInbound;
            node.Context = new DefaultChannelHandlerContext(node, this);
            return node;
        }

        /// <summary>把已初始化的节点挂到 <paramref name="next"/> 之前，并标记为曾经挂链。</summary>
        private static void LinkBefore(Node next, Node node)
        {
            node.Prev = next.Prev;
            node.Next = next;
            next.Prev.Next = node;
            next.Prev = node;
            node.LinkedOnce = true;
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

        /// <summary>
        /// 出站调用视图暴露给处理器的链视图：除业务写入口外全部转发到真实链。
        /// 当前写加工只能走 <see cref="DefaultChannelHandlerContext.WriteAsync"/>，
        /// 通过本视图发起独立业务写会排在当前写之后，直接拒绝。
        /// </summary>
        private sealed class OutboundGuardPipeline : IChannelPipeline
        {
            private readonly ChannelPipeline _pipeline;

            internal OutboundGuardPipeline(ChannelPipeline pipeline)
            {
                _pipeline = pipeline;
            }

            public IChannelPipeline AddLast(IChannelHandler handler)
            {
                return _pipeline.AddLast(handler);
            }

            public IChannelPipeline AddFirst(IChannelHandler handler)
            {
                return _pipeline.AddFirst(handler);
            }

            public IChannelPipeline Remove(IChannelHandler handler)
            {
                return _pipeline.Remove(handler);
            }

            public IReadOnlyList<IChannelHandler> Handlers
            {
                get { return _pipeline.Handlers; }
            }

            public Task FireChannelReadAsync(object msg)
            {
                return _pipeline.FireChannelReadAsync(msg);
            }

            public Task FireChannelReadCompleteAsync()
            {
                return _pipeline.FireChannelReadCompleteAsync();
            }

            public Task FireExceptionCaughtAsync(Exception ex)
            {
                return _pipeline.FireExceptionCaughtAsync(ex);
            }

            /// <summary>出站回调内禁止发起独立业务写：新写会排在当前写之后，形成串行器自死锁。</summary>
            /// <param name="msg">待发送的业务消息。</param>
            /// <returns>不会返回，总是抛出。</returns>
            public Task WriteAsync(object msg)
            {
                throw new InvalidOperationException(
                    "出站回调内不能通过 ctx.Pipeline.WriteAsync 发起独立业务写：新写会排在当前写之后。"
                    + "当前写加工请使用 ctx.WriteAsync，独立写应在当前写完成后由业务侧发起。");
            }

            public Task CloseAsync(string reason)
            {
                return _pipeline.CloseAsync(reason);
            }

            public void Dispose()
            {
                _pipeline.Dispose();
            }
        }
    }
}