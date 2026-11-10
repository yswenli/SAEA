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
*文件名： SwitchableSplitter
*版本号： v26.11.5.1
*唯一标识：56899229-972a-4210-a000-9bb8133c37d7
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/9 11:16:22
*描述：可切换分帧器：在协议升级边界停切并等待切换，切换后把残余字节交给新分帧器
*
*=====================================================================
*修改标记
*修改时间：2026/11/9 11:16:22
*修改人： yswenli
*版本号： v26.11.5.1
*描述：可切换分帧器：在协议升级边界停切并等待切换，切换后把残余字节交给新分帧器
*
*****************************************************************************/
using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 可切换分帧器：同一连接上按协议阶段切换分帧方式（如 HTTP 升级为 WebSocket）。
    /// 分帧器在产出本协议最后一帧后把 <see cref="StopAfterCurrent"/> 置为 true，接收泵随即停切并等待
    /// <see cref="SwitchTo"/>；切换后残余字节由新分帧器继续切分。每连接必须新建一个实例。
    /// </summary>
    public sealed class SwitchableSplitter : IFrameSplitter
    {
        private readonly int _maxFrameLength;
        private readonly object _gate = new object();

        private IFrameSplitter _current;
        private int _stopAfterCurrent;
        private TaskCompletionSource<bool> _switched;

        /// <summary>
        /// 创建可切换分帧器。
        /// </summary>
        /// <param name="initial">初始分帧器，不能为 null。</param>
        /// <param name="switchableSplitters">后续可能切换到的分帧器，可为空；用于把最大帧长一次钉死。</param>
        public SwitchableSplitter(IFrameSplitter initial, params IFrameSplitter[] switchableSplitters)
        {
            if (initial == null)
                throw new ArgumentNullException(nameof(initial));

            _current = initial;

            var max = initial.MaxFrameLength;
            if (switchableSplitters != null)
            {
                for (var i = 0; i < switchableSplitters.Length; i++)
                {
                    var splitter = switchableSplitters[i];
                    if (splitter == null)
                        throw new ArgumentException("可切换分帧器列表不能包含 null。", nameof(switchableSplitters));

                    if (splitter.MaxFrameLength > max)
                        max = splitter.MaxFrameLength;
                }
            }

            // 上限在创建时固定：ChannelPipeline.Create 的缓冲下限只按初始 MaxFrameLength 算一次，
            // 运行期改上限会让背压闸门变成可变值，内存账无法核算。
            _maxFrameLength = max;
        }

        /// <summary>当前生效的分帧器。</summary>
        public IFrameSplitter Current
        {
            get { return Volatile.Read(ref _current); }
        }

        /// <summary>
        /// 所有可切换协议的最大帧长；创建后固定，<see cref="SwitchTo"/> 拒绝超过该值的分帧器。
        /// 上限与当前分帧器无关，切换不会重算接收缓冲上限。
        /// </summary>
        public int MaxFrameLength
        {
            get { return _maxFrameLength; }
        }

        /// <summary>
        /// 本帧产出后是否停止继续切帧，直到 <see cref="SwitchTo"/> 被调用。
        /// 由分帧器在识别到「本协议最后一帧」时置为 true；接收泵只在本值有效时等待切换。
        /// </summary>
        public bool StopAfterCurrent
        {
            get { return Volatile.Read(ref _stopAfterCurrent) == 1; }
            set { Volatile.Write(ref _stopAfterCurrent, value ? 1 : 0); }
        }

        /// <summary>
        /// 切换分帧器并解除屏障。只允许在屏障开启（<see cref="StopAfterCurrent"/> 为 true）时调用；
        /// 决定不升级时传入 <see cref="Current"/> 即可复位屏障。
        /// </summary>
        /// <param name="splitter">切换后生效的分帧器，不能为 null，且最大帧长不得超过构造时固定的上限。</param>
        public void SwitchTo(IFrameSplitter splitter)
        {
            if (splitter == null)
                throw new ArgumentNullException(nameof(splitter));

            if (splitter.MaxFrameLength > _maxFrameLength)
            {
                throw new InvalidOperationException(
                    "切换目标的最大帧长 " + splitter.MaxFrameLength + " 超过创建时固定的上限 "
                    + _maxFrameLength + "；接收缓冲上限不会随切换重算。");
            }

            TaskCompletionSource<bool> signal;
            lock (_gate)
            {
                if (Volatile.Read(ref _stopAfterCurrent) == 0)
                {
                    throw new InvalidOperationException(
                        "当前没有等待切换的帧：SwitchTo 只允许在 StopAfterCurrent 为 true 时调用。");
                }

                Volatile.Write(ref _current, splitter);
                Volatile.Write(ref _stopAfterCurrent, 0);
                signal = _switched;
                _switched = null;
            }

            // 唤醒在锁外执行，避免泵的续接在本线程持锁时重入切换逻辑。
            if (signal != null)
                signal.TrySetResult(true);
        }

        /// <inheritdoc />
        public bool TrySplit(ReadOnlySequence<byte> buffer, out long consumed, out ReadOnlySequence<byte> frame)
        {
            return Current.TrySplit(buffer, out consumed, out frame);
        }

        /// <inheritdoc />
        public void Reset()
        {
            Current.Reset();
        }

        /// <summary>
        /// 在屏障上等待切换完成。屏障已解除时立即返回。
        /// 超时或连接取消按分帧协议错误收口，把「漏调 SwitchTo」从永久挂起改成可观察的断开。
        /// </summary>
        /// <param name="timeout">等待上限；小于等于零表示不等待，直接按协议错误收口。</param>
        /// <param name="cancellationToken">连接级取消令牌。</param>
        /// <returns>切换完成后即完成的任务。</returns>
        internal Task WaitForSwitchAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            TaskCompletionSource<bool> signal;
            lock (_gate)
            {
                if (Volatile.Read(ref _stopAfterCurrent) == 0)
                    return Task.CompletedTask;

                // 续接必须异步执行：SwitchTo 可能由链上的处理器在任意线程调用，
                // 不能让泵的后续逻辑插进调用方的同步栈。
                signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _switched = signal;
            }

            return AwaitSwitchAsync(signal, timeout, cancellationToken);
        }

        private async Task AwaitSwitchAsync(
            TaskCompletionSource<bool> signal,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            try
            {
                if (timeout <= TimeSpan.Zero)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new FrameProtocolException("等待分帧器切换的上限为零或负数，已按协议错误收口。");
                }

                // Task.Delay 的上限是 int 毫秒，超出时截断，避免参数异常掩盖真正的等待语义。
                var bounded = timeout.TotalMilliseconds > int.MaxValue
                    ? TimeSpan.FromMilliseconds(int.MaxValue)
                    : timeout;

                var delay = Task.Delay(bounded, cancellationToken);
                var winner = await Task.WhenAny(signal.Task, delay).ConfigureAwait(false);
                if (winner == signal.Task)
                    return;

                if (cancellationToken.IsCancellationRequested)
                {
                    throw new FrameProtocolException(
                        "等待分帧器切换时连接已关闭。",
                        new OperationCanceledException(cancellationToken));
                }

                throw new FrameProtocolException(
                    "等待分帧器切换超时（" + (long)timeout.TotalMilliseconds + " 毫秒）。");
            }
            finally
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_switched, signal))
                        _switched = null;
                }
            }
        }
    }
}