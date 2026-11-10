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
*修改时间：2026/11/9 11:24:35
*修改人： yswenli
*版本号： v26.11.5.1
*描述：一次业务写的状态与公共完成源，保证完成与计数只发生一次
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
    /// </summary>
    internal sealed class OutboundOperation
    {
        /// <summary>已领取但尚未交给下游节点。</summary>
        internal const int Queued = 0;

        /// <summary>已交给下游节点，等待真实链任务完成。</summary>
        internal const int HandedOff = 1;

        /// <summary>公共完成源已落地，后续状态迁移与计数均被拒绝。</summary>
        internal const int Completed = 2;

        private readonly ChannelPipeline _pipeline;
        private readonly object _message;
        private readonly TaskCompletionSource<bool> _completion =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

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

        /// <summary>调用方拿到的公共完成任务。</summary>
        internal Task Task
        {
            get { return _completion.Task; }
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
                _completion.TrySetResult(true);
            else if (error is OperationCanceledException)
                _completion.TrySetCanceled();
            else
                _completion.TrySetException(error);

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
            _completion.TrySetException(error);

            var raw = _message as RawFrame;
            if (raw != null)
                raw.Dispose();

            return true;
        }
    }
}