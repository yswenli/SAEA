using SAEA.Common.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace SAEA.Common.Caching
{
    /// <summary>
    /// 池化批处理器：收集 PooledBufferWriter，满 size 或超时后合并为一个 writer 并回调。
    /// 所有权约定：Insert 返回 true 后由本类持有 writer；返回 false 时调用方仍持有并须自行 Dispose。
    /// </summary>
    public delegate void OnPooledBatchedHandler(PooledBufferWriter batch, int count);

    /// <summary>
    /// 池化批处理器。与旧 <see cref="Batcher"/> 不同，承载的是 <see cref="PooledBufferWriter"/> 而非 byte[]；
    /// Dispose/Clear 会 Dispose 队列内所有 writer 且不触发回调（避免所有权悬空与泄漏）。
    /// 注意：_capacitySemaphore 永不 Dispose——后台 Handler 的 Flush 可能与 Dispose 并发 Release，
    /// 释放信号量会抛 ObjectDisposedException；SemaphoreSlim 不释放仅占用极小托管内存。
    /// </summary>
    public sealed class PooledBatcher : IDisposable
    {
        readonly int _size;
        readonly int _timeout;
        readonly int _max;
        readonly ConcurrentQueue<PooledBufferWriter> _queue;
        readonly SemaphoreSlim _capacitySemaphore;
        readonly object _sync = new object();
        volatile bool _stopped;

        public event OnPooledBatchedHandler OnBatched;

        public string Name { get; private set; }

        public int PendingCount { get { return _queue.Count; } }

        public PooledBatcher(int size = 1000, int timeout = 1000, int max = -1, string name = "")
        {
            _size = size > 0 ? size : 1000;
            _timeout = timeout > 0 ? timeout : 1000;
            _max = max == -1 ? _size * 10 : max;
            if (_max < _size) throw new ArgumentOutOfRangeException(nameof(max), "max不能小于size");
            Name = name ?? string.Empty;
            _queue = new ConcurrentQueue<PooledBufferWriter>();
            _capacitySemaphore = new SemaphoreSlim(_max, _max);
            _stopped = false;
            TaskHelper.LongRunning(Handler);
        }

        public bool Insert(PooledBufferWriter writer)
        {
            if (writer == null) return false;
            lock (_sync)
            {
                if (_stopped) return false;
                if (!_capacitySemaphore.Wait(0)) return false;
                _queue.Enqueue(writer);
                return true;
            }
        }

        void Handler()
        {
            var stopwatch = Stopwatch.StartNew();
            while (!_stopped)
            {
                var count = _queue.Count;
                if (count >= _size || (count > 0 && stopwatch.ElapsedMilliseconds >= _timeout))
                {
                    try { Flush(); } catch { }
                    stopwatch.Restart();
                }
                else
                {
                    ThreadHelper.Sleep(Math.Min(_timeout, 100));
                }
            }
        }

        void Flush()
        {
            var list = new List<PooledBufferWriter>();
            var take = Math.Min(_queue.Count, _size);
            for (var i = 0; i < take; i++)
            {
                if (_queue.TryDequeue(out var w))
                {
                    list.Add(w);
                    try { _capacitySemaphore.Release(); } catch (SemaphoreFullException) { }
                }
            }
            if (list.Count == 0) return;

            long total = 0;
            for (var i = 0; i < list.Count; i++) total += list[i].WrittenCount;

            PooledBufferWriter merged = null;
            try
            {
                if (total <= 0 || total > int.MaxValue) return;
                merged = new PooledBufferWriter((int)total);
                for (var i = 0; i < list.Count; i++)
                {
                    var span = list[i].WrittenSpan;
                    span.CopyTo(merged.GetSpan(span.Length));
                    merged.Advance(span.Length);
                }
                var handler = OnBatched;
                if (handler != null)
                {
                    handler(merged, list.Count);
                    merged = null;
                }
            }
            finally
            {
                if (merged != null) merged.Dispose();
                for (var i = 0; i < list.Count; i++) { try { list[i].Dispose(); } catch { } }
            }
        }

        public void Clear()
        {
            lock (_sync)
            {
                DrainLocked();
            }
        }

        void DrainLocked()
        {
            while (_queue.TryDequeue(out var w))
            {
                try { w.Dispose(); } catch { }
                try { _capacitySemaphore.Release(); } catch (SemaphoreFullException) { }
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                _stopped = true;
                DrainLocked();
            }
        }
    }
}
