/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
 *Copyright (c) yswenli All Rights Reserved.
 *CLR版本： netstandard2.0
 *机器名称：WENLI-PC
 *公司名称：yswenli
 *命名空间：SAEA.MQTT.Internal
 *文件名： MqttExpiryScheduler
 *版本号： v26.4.23.1
 *唯一标识：6f0f0f70-2a4b-4d4d-9a0e-2f0d7a0b1c04
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2021/03/11 16:46:45
 *描述：MqttExpiryScheduler类
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/05 00:00:00
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：MqttExpiryScheduler类
 *
 *****************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading;
using SAEA.MQTT.Diagnostics;

namespace SAEA.MQTT.Internal
{
    /// <summary>
    /// 到期任务调度器：手写二叉最小堆 + 单个 <see cref="Timer"/>。
    /// 取消采用代际（惰性删除）策略，回调始终在锁外执行。
    /// </summary>
    internal sealed class MqttExpiryScheduler : IDisposable
    {
        // System.Threading.Timer 允许的最大 dueTime 约为 49.7 天，超过会抛异常。
        static readonly TimeSpan MaximumTimerDueTime = TimeSpan.FromMilliseconds(4294967294d);

        readonly object _syncRoot = new object();
        readonly List<HeapNode> _heap = new List<HeapNode>();
        readonly IMqttNetScopedLogger _logger;

        Timer _timer;
        long _nextSequence;
        bool _started;
        volatile bool _disposed;

        public MqttExpiryScheduler(IMqttNetLogger logger = null)
        {
            if (logger != null)
            {
                _logger = logger.CreateScopedLogger(nameof(MqttExpiryScheduler));
            }
        }

        /// <summary>
        /// 登记一个在 <paramref name="dueAt"/> 到期执行的任务。
        /// 返回的 <see cref="IDisposable"/> 只负责取消，不会立即从堆中移除。
        /// </summary>
        public IDisposable Schedule(DateTimeOffset dueAt, Action callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));

            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return EmptyDisposable.Instance;
                }

                var handle = new CancellationHandle();
                var node = new HeapNode
                {
                    DueAt = dueAt,
                    Sequence = _nextSequence++,
                    Callback = callback,
                    Handle = handle
                };

                Push(node);

                if (_started)
                {
                    ResetTimerLocked();
                }

                return handle;
            }
        }

        /// <summary>
        /// 启动底层定时器。可重复调用。
        /// </summary>
        public void Start()
        {
            lock (_syncRoot)
            {
                if (_disposed || _started)
                {
                    return;
                }

                _started = true;
                _timer = new Timer(OnTimer, null, Timeout.Infinite, Timeout.Infinite);
                ResetTimerLocked();
            }
        }

        /// <summary>
        /// 执行所有已到期任务。测试可直接调用以配合假时钟，无需真实等待。
        /// </summary>
        internal void RunDueTasks()
        {
            List<HeapNode> dueNodes = null;

            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                var now = MqttTime.UtcNow;

                while (_heap.Count > 0)
                {
                    var node = _heap[0];
                    if (node.DueAt > now)
                    {
                        break;
                    }

                    PopRoot();

                    if (node.Handle.Cancelled)
                    {
                        continue;
                    }

                    if (dueNodes == null)
                    {
                        dueNodes = new List<HeapNode>();
                    }

                    dueNodes.Add(node);
                }

                if (_started)
                {
                    ResetTimerLocked();
                }
            }

            if (dueNodes == null)
            {
                return;
            }

            // 回调在锁外执行：回调内部可以安全地再次 Schedule/Dispose，不会死锁。
            foreach (var node in dueNodes)
            {
                if (_disposed)
                {
                    return;
                }

                if (node.Handle.Cancelled)
                {
                    continue;
                }

                try
                {
                    node.Callback();
                }
                catch (Exception exception)
                {
                    if (_logger != null)
                    {
                        _logger.Error(exception, "Unhandled exception in MQTT expiry task.");
                    }
                }
            }
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _started = false;
                _heap.Clear();

                var timer = _timer;
                _timer = null;
                if (timer != null)
                {
                    timer.Change(Timeout.Infinite, Timeout.Infinite);
                    timer.Dispose();
                }
            }
        }

        void OnTimer(object state)
        {
            try
            {
                RunDueTasks();
            }
            catch (Exception exception)
            {
                if (_logger != null)
                {
                    _logger.Error(exception, "Unhandled exception in MQTT expiry scheduler timer callback.");
                }
            }
        }

        void ResetTimerLocked()
        {
            if (!_started || _timer == null)
            {
                return;
            }

            if (_heap.Count == 0)
            {
                _timer.Change(Timeout.Infinite, Timeout.Infinite);
                return;
            }

            var now = MqttTime.UtcNow;
            var dueTime = _heap[0].DueAt - now;
            if (dueTime < TimeSpan.Zero)
            {
                dueTime = TimeSpan.Zero;
            }
            else if (dueTime > MaximumTimerDueTime)
            {
                // 定时器到期后若仍无任务到期，RunDueTasks 会重新计算下一次触发时间。
                dueTime = MaximumTimerDueTime;
            }

            _timer.Change(dueTime, Timeout.InfiniteTimeSpan);
        }

        void Push(HeapNode node)
        {
            _heap.Add(node);

            var index = _heap.Count - 1;
            while (index > 0)
            {
                var parentIndex = (index - 1) / 2;
                if (!IsLess(node, _heap[parentIndex]))
                {
                    break;
                }

                _heap[index] = _heap[parentIndex];
                index = parentIndex;
            }

            _heap[index] = node;
        }

        void PopRoot()
        {
            var lastIndex = _heap.Count - 1;
            var last = _heap[lastIndex];
            _heap.RemoveAt(lastIndex);

            if (lastIndex == 0)
            {
                return;
            }

            var index = 0;
            while (true)
            {
                var leftIndex = (index * 2) + 1;
                if (leftIndex >= _heap.Count)
                {
                    break;
                }

                var rightIndex = leftIndex + 1;
                var smallestIndex = leftIndex;
                if (rightIndex < _heap.Count && IsLess(_heap[rightIndex], _heap[leftIndex]))
                {
                    smallestIndex = rightIndex;
                }

                if (!IsLess(_heap[smallestIndex], last))
                {
                    break;
                }

                _heap[index] = _heap[smallestIndex];
                index = smallestIndex;
            }

            _heap[index] = last;
        }

        static bool IsLess(HeapNode left, HeapNode right)
        {
            if (left.DueAt < right.DueAt)
            {
                return true;
            }

            if (left.DueAt > right.DueAt)
            {
                return false;
            }

            return left.Sequence < right.Sequence;
        }

        sealed class HeapNode
        {
            public DateTimeOffset DueAt;
            public long Sequence;
            public Action Callback;
            public CancellationHandle Handle;
        }

        sealed class CancellationHandle : IDisposable
        {
            volatile bool _cancelled;

            public bool Cancelled
            {
                get { return _cancelled; }
            }

            public void Dispose()
            {
                _cancelled = true;
            }
        }

        sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new EmptyDisposable();

            public void Dispose()
            {
            }
        }
    }
}