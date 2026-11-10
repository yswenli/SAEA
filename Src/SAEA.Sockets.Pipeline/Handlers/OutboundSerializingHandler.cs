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
*文件名： OutboundSerializingHandler
*版本号： v26.11.5.1
*唯一标识：78b9fd0e-c9cb-42ff-a8f2-4ed65406c514
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/9 10:48:29
*描述：出站写入串行器：把同一连接上的待写消息按到达顺序交给下游编码
*
*=====================================================================
*修改标记
*修改时间：2026/11/9 12:05:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：实现 lock + 队列的单写者 FIFO、待写项数预算与摘链清理
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SAEA.Common;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 出站写入串行器：把同一连接上的待写消息按到达顺序交给下游编码。
    /// 必须是出站前缀最右侧（紧贴第一个入站节点），先排队再编码，否则异步编码器会让先发起的写后到。
    /// 只保证消息到达本节点后按 FIFO 排队，不定义多线程并发调用业务写入口时的先后顺序。
    /// </summary>
    public sealed class OutboundSerializingHandler : OutboundHandlerAdapter
    {
        private const int DefaultMaxPendingWrites = 256;

        private readonly object _gate = new object();
        private readonly Queue<PendingWrite> _queue = new Queue<PendingWrite>();
        private readonly int _maxPendingWrites;

        private bool _active;
        private bool _closed;
        private PendingWrite _activeItem;

        /// <summary>以默认待写上限 256 创建串行器。</summary>
        public OutboundSerializingHandler()
            : this(DefaultMaxPendingWrites)
        {
        }

        /// <summary>以指定待写上限创建串行器。</summary>
        /// <param name="maxPendingWrites">尚未完成交接的待写项数上限；小于等于 0 表示不限制。</param>
        public OutboundSerializingHandler(int maxPendingWrites)
        {
            _maxPendingWrites = maxPendingWrites;
        }

        /// <summary>尚未完成交接的待写项数上限；小于等于 0 表示不限制。</summary>
        public int MaxPendingWrites
        {
            get { return _maxPendingWrites; }
        }

        /// <summary>
        /// 到达即入队：空闲调用方成为写者，其余调用方按到达顺序排队。
        /// 已摘链或超过待写上限时本次写直接失败；原始帧按未移交补偿，其他对象不代管。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="msg">待发送消息。</param>
        /// <returns>本项真实交接完成后结束的任务。</returns>
        public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
        {
            var view = ctx as DefaultChannelHandlerContext;
            if (view == null || view.Operation == null)
            {
                throw new InvalidOperationException(
                    "OutboundSerializingHandler 只能作为业务写出站链的第一个节点：缺少本次写的操作绑定。");
            }

            var pending = new PendingWrite(ctx, view.Operation, msg);
            var startDrain = false;
            Exception rejection = null;

            lock (_gate)
            {
                if (_closed)
                {
                    rejection = new ChannelClosedException("出站串行器已摘链，不能继续发送。");
                }
                else if (_maxPendingWrites > 0
                         && _queue.Count + (_active ? 1 : 0) >= _maxPendingWrites)
                {
                    rejection = new InvalidOperationException(
                        "出站待写项数已达到上限 MaxPendingWrites=" + _maxPendingWrites + "，本次写被拒绝。");
                }
                else
                {
                    _queue.Enqueue(pending);
                    if (!_active)
                    {
                        _active = true;
                        startDrain = true;
                    }
                }
            }

            if (rejection != null)
            {
                CompensateRawFrame(msg);
                return Task.FromException(rejection);
            }

            // 写者不让调用方等待整个队列：只有本项自己的 Task 会回到发起方。
            if (startDrain)
                _ = DrainAsync();

            return pending.Task;
        }

        /// <summary>
        /// 摘链清理：排队项与已出队未移交项禁止再移交，公共任务以
        /// <see cref="ChannelClosedException"/> 失败并补偿原始帧；已移交项只让公共任务失败，
        /// 消息仍由下游负责，迟完成不会重复减计数。
        /// </summary>
        /// <param name="ctx">已摘链节点的上下文。</param>
        public override void HandlerRemoved(IChannelHandlerContext ctx)
        {
            var closed = new ChannelClosedException("出站串行器已摘链，待写消息不再移交。");
            List<PendingWrite> queued;
            PendingWrite active;

            lock (_gate)
            {
                _closed = true;
                queued = new List<PendingWrite>(_queue);
                _queue.Clear();
                active = _activeItem;
                _activeItem = null;
                _active = false;
            }

            for (var i = 0; i < queued.Count; i++)
            {
                var item = queued[i];
                item.Operation.TryAbortQueued(closed);
                item.TrySetException(closed);
            }

            if (active == null)
                return;

            if (active.Operation.TryAbortQueued(closed))
            {
                // active-not-handed：本节点仍持有消息，按未移交补偿后解除调用方等待。
                active.TrySetException(closed);
                return;
            }

            // active-handed-off 或已被竞态完成：只让公共任务失败，消息由下游与迟完成回调清理；
            // 这里不能完成本项的交接 Task，否则上游视图会提前失效，让下游调用拿到已失效的上下文。
            active.Operation.TryComplete(closed);
        }

        /// <summary>
        /// 单写者循环：锁内取队首并推进移交，锁外调用下游，按真实结果完成操作后回锁取下一个。
        /// 单个写失败只完成自己并继续后续，不中断 FIFO。
        /// </summary>
        private async Task DrainAsync()
        {
            while (true)
            {
                PendingWrite item;
                bool handedOff;

                lock (_gate)
                {
                    if (_closed || _queue.Count == 0)
                    {
                        _activeItem = null;
                        _active = false;
                        return;
                    }

                    item = _queue.Dequeue();
                    _activeItem = item;
                    handedOff = item.Operation.TryMarkHandedOff();
                }

                if (!handedOff)
                {
                    // 收口超时或摘链已按未移交完成本项：跳过下游，只解除调用方等待。
                    item.TrySetException(new ChannelClosedException("本次写已在移交前完成，不再交给下游。"));
                    continue;
                }

                Exception error = null;
                try
                {
                    await item.Context.WriteAsync(item.Message).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                // 真实结果只驱动唯一完成路径；已由超时或摘链完成的项在这里是无操作。
                item.Operation.TryComplete(error);
                item.Complete(error);
            }
        }

        /// <summary>未成功移交时释放原始帧；普通业务对象不代管。</summary>
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

        /// <summary>一次已进入串行器队列的写；自带交接 Task，真实结果由写者落地。</summary>
        private sealed class PendingWrite
        {
            private readonly TaskCompletionSource<bool> _completion =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            internal PendingWrite(IChannelHandlerContext context, OutboundOperation operation, object message)
            {
                Context = context;
                Operation = operation;
                Message = message;
            }

            internal IChannelHandlerContext Context { get; }

            internal OutboundOperation Operation { get; }

            internal object Message { get; }

            internal Task Task
            {
                get { return _completion.Task; }
            }

            internal void Complete(Exception error)
            {
                if (error == null)
                    _completion.TrySetResult(true);
                else if (error is OperationCanceledException)
                    _completion.TrySetCanceled();
                else
                    _completion.TrySetException(error);
            }

            internal void TrySetException(Exception error)
            {
                _completion.TrySetException(error);
            }
        }
    }
}