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
*文件名： OutboundOperation
*版本号： v26.11.5.1
*唯一标识：10ef2964-152c-410b-a30d-0e59c8c76ba7
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/9 11:24:35
*描述：一次业务写的状态与公共完成源，保证完成与计数只发生一次
*
*=====================================================================
*修改标记
*修改时间：2026/11/20 14:10:09
*修改人： yswenli
*版本号： v26.11.16.1
*描述：同步完成的写不再预建完成源，按需落地完成形态，省掉每次写的完成源与其任务分配
*
*****************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 一次业务写的状态与公共完成源。状态为 <see cref="Queued"/> → <see cref="HandedOff"/> → <see cref="Completed"/>；
    /// 「已出队但尚未调下游」仍是 <see cref="Queued"/> 并带 active 标记。完成与写计数只允许发生一次。
    /// 完成形态按需落地：整条出站链在同一次调用内跑完的写直接用共享的已完成任务，
    /// 只有真要异步交付时才建完成源，因此同步写不再为每次调用分配完成源与它背后的任务对象。
    /// </summary>
    internal sealed class OutboundOperation
    {
        /// <summary>已领取但尚未交给下游节点。</summary>
        internal const int Queued = 0;

        /// <summary>已交给下游节点，等待真实链任务完成。</summary>
        internal const int HandedOff = 1;

        /// <summary>完成形态已落地，后续状态迁移与计数均被拒绝。</summary>
        internal const int Completed = 2;

        // 同步成功的写共用这一个已完成任务；属性名与类型名同名，所以不能直接写 Task.CompletedTask。
        private static readonly Task SharedCompletedTask = System.Threading.Tasks.Task.CompletedTask;

        // 完成形态只允许一次原子落地，三种取值：
        // null 表示还没有人取用过公共任务；TaskCompletionSource<bool> 表示已有等待方，完成后由它唤醒；
        // Task 表示同步落地，直接就是调用方要的已完成任务（成功为共享实例，失败为已带异常的实例）。
        private object _completion;

        private readonly ChannelPipeline _pipeline;
        private readonly object _message;

        private int _state;
        private int _active;

        internal OutboundOperation(ChannelPipeline pipeline, object message)
        {
            if (pipeline == null)
                throw new ArgumentNullException(nameof(pipeline));

            _pipeline = pipeline;
            _message = message;
        }

        /// <summary>本次业务写的原始消息；仅用于未移交时的原始帧补偿。</summary>
        internal object Message
        {
            get { return _message; }
        }

        /// <summary>调用方拿到的公共完成任务；同步完成的写直接复用已完成的公共任务，不建完成源。</summary>
        internal Task Task
        {
            get
            {
                while (true)
                {
                    var settled = Volatile.Read(ref _completion);

                    var completed = settled as Task;
                    if (completed != null)
                        return completed;

                    var completion = settled as TaskCompletionSource<bool>;
                    if (completion != null)
                        return completion.Task;

                    // 先有人取用公共任务说明这一次写可能异步交付：此处建完成源，后续完成时由它落地。
                    var created = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    if (Interlocked.CompareExchange(ref _completion, created, null) == null)
                        return created.Task;

                    // 并发方先落地了完成形态，回到循环按它的形态返回，避免漏掉已完成的任务。
                }
            }
        }

        /// <summary>当前状态，仅供测试与诊断读取。</summary>
        internal int State
        {
            get { return Volatile.Read(ref _state); }
        }

        /// <summary>是否已被写者取走但尚未交给下游。</summary>
        internal bool IsActive
        {
            get { return Volatile.Read(ref _active) != 0; }
        }

        /// <summary>标记写者已取走本项。</summary>
        internal void MarkActive()
        {
            Volatile.Write(ref _active, 1);
        }

        /// <summary>标记写者已结束本项。</summary>
        internal void ClearActive()
        {
            Volatile.Write(ref _active, 0);
        }

        /// <summary>
        /// 把仍处于 <see cref="Queued"/> 的项原子推进到 <see cref="HandedOff"/>。
        /// 已被超时或移除完成的项返回 false，调用方不得再调下游。
        /// </summary>
        /// <returns>是否成功取得移交权。</returns>
        internal bool TryMarkHandedOff()
        {
            return Interlocked.CompareExchange(ref _state, HandedOff, Queued) == Queued;
        }

        /// <summary>
        /// 从 <see cref="Queued"/> 或 <see cref="HandedOff"/> 落地公共完成源，只有第一次成功。
        /// </summary>
        /// <param name="error">失败原因；为 null 表示成功完成。</param>
        /// <returns>本次调用是否完成该项。</returns>
        internal bool TryComplete(Exception error)
        {
            while (true)
            {
                var state = Volatile.Read(ref _state);
                if (state == Completed)
                    return false;

                if (Interlocked.CompareExchange(ref _state, Completed, state) == state)
                    break;
            }

            _pipeline.OnOutboundCompleted(this);

            if (error == null)
                CompleteSuccess();
            else
                CompleteFailure(error);

            return true;
        }

        /// <summary>
        /// 仅在仍处于 <see cref="Queued"/>（含 active-not-handed）时强制完成并补偿原始帧；
        /// 已移交的项不伪造完成、不释放消息，由下游与迟完成回调继续清理。
        /// </summary>
        /// <param name="error">强制完成使用的原因。</param>
        /// <returns>本次调用是否完成该项。</returns>
        internal bool TryAbortQueued(Exception error)
        {
            if (Interlocked.CompareExchange(ref _state, Completed, Queued) != Queued)
                return false;

            _pipeline.OnOutboundCompleted(this);
            CompleteQueuedAbort(error);

            var raw = _message as RawFrame;
            if (raw != null)
                raw.Dispose();

            return true;
        }

        /// <summary>
        /// 成功落地：同步完成时把共享的已完成任务放进完成位；已经有人在等（或并发建了完成源）时唤醒它。
        /// </summary>
        private void CompleteSuccess()
        {
            while (true)
            {
                var settled = Volatile.Read(ref _completion);

                var completion = settled as TaskCompletionSource<bool>;
                if (completion != null)
                {
                    completion.TrySetResult(true);
                    return;
                }

                if (settled != null)
                    return;

                if (Interlocked.CompareExchange(ref _completion, SharedCompletedTask, null) == null)
                    return;

                // 并发方先建了完成源，回到循环交给它落地。
            }
        }

        /// <summary>
        /// 失败落地：失败与取消必须留下可等待的任务，同步失败也按原有语义交给调用方。
        /// </summary>
        /// <param name="error">失败原因。</param>
        private void CompleteFailure(Exception error)
        {
            CompleteError(error, true);
        }

        /// <summary>
        /// 强制完成落地：排队项被强制清理时历史语义是一律以异常交付，
        /// 即便原因是取消类异常也落成故障任务，不改成取消态。
        /// </summary>
        /// <param name="error">强制完成使用的原因。</param>
        private void CompleteQueuedAbort(Exception error)
        {
            CompleteError(error, false);
        }

        /// <summary>
        /// 把失败形态落到完成位：没有人取用过公共任务时新建完成源并当场失败，
        /// 已经有人在等时直接唤醒它。
        /// </summary>
        /// <param name="error">失败原因。</param>
        /// <param name="cancellationAware">取消类异常是否落成取消态。</param>
        private void CompleteError(Exception error, bool cancellationAware)
        {
            while (true)
            {
                var settled = Volatile.Read(ref _completion);

                var completion = settled as TaskCompletionSource<bool>;
                if (completion != null)
                {
                    Settle(completion, error, cancellationAware);
                    return;
                }

                if (settled != null)
                    return; // 完成形态已由成功路径落地：状态位只允许一次迁移，这里不会被走到

                var created = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                if (Interlocked.CompareExchange(ref _completion, created, null) == null)
                {
                    Settle(created, error, cancellationAware);
                    return;
                }

                // 并发方先落地了完成形态，回到循环按它的形态处理。
            }
        }

        /// <summary>按既有语义把结果交给完成源。</summary>
        /// <param name="completion">本次写的完成源。</param>
        /// <param name="error">失败原因。</param>
        /// <param name="cancellationAware">取消类异常是否落成取消态。</param>
        private static void Settle(TaskCompletionSource<bool> completion, Exception error, bool cancellationAware)
        {
            if (cancellationAware && error is OperationCanceledException)
                completion.TrySetCanceled();
            else
                completion.TrySetException(error);
        }
    }
}