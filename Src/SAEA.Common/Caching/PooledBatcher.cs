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
*命名空间：SAEA.Common.Caching
*文件名： PooledBatcher
*版本号： v26.11.5.1
*唯一标识：ba7bd831-b79a-4f85-a7fc-60e180336ca0
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 12:40:36
*描述：基于内存池的批量处理器
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:45
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
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
    /// <param name="batch">合并后的 writer，回调返回后由本类释放，获取缓冲区需在回调内完成</param>
    /// <param name="count">本批包含的 writer 个数</param>
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

        /// <summary>
        /// 一批数据合并完成后触发，参数为合并后的 writer 与本批 writer 个数
        /// </summary>
        public event OnPooledBatchedHandler OnBatched;

        /// <summary>
        /// 批处理实例名称，用于日志与排查
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// 当前队列中尚未取出的 writer 个数
        /// </summary>
        public int PendingCount { get { return _queue.Count; } }

        /// <summary>
        /// 创建池化批处理器并启动后台成批线程
        /// </summary>
        /// <param name="size">每批最大 writer 个数，小于等于 0 时按 1000 处理</param>
        /// <param name="timeout">凑不满一批时的最长等待时间（毫秒），小于等于 0 时按 1000 处理</param>
        /// <param name="max">队列允许积压的最大 writer 个数，-1 表示按 size × 10 计算</param>
        /// <param name="name">实例名称，null 转为空字符串</param>
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

        /// <summary>
        /// 入队一个 writer，队列满或实例已停止时立即返回 false，此时 writer 仍归调用方所有
        /// </summary>
        /// <param name="writer">待入队的 writer</param>
        /// <returns>入队成功返回 true，此后 writer 由本类释放</returns>
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

        /// <summary>
        /// 清空队列并释放其中全部 writer，不触发回调
        /// </summary>
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

        /// <summary>
        /// 停止后台成批线程并释放队列内全部 writer，不触发回调；内部容量信号量不释放
        /// </summary>
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
