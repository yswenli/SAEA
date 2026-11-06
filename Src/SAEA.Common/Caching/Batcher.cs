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
*命名空间：SAEA.Common.Caching
*文件名： Batcher
*版本号： v26.4.23.1
*唯一标识：87041841-52fc-4641-a0aa-b084f289cd2e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/23 17:18:02
*描述：Batcher类
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
using System.Threading.Tasks;

namespace SAEA.Common.Caching
{
    /// <summary>
    /// 批量数据回调
    /// </summary>
    /// <typeparam name="T">批次元素类型</typeparam>
    /// <param name="sender">触发回调的批处理实例</param>
    /// <param name="data">本批次的数据，条数不超过批大小</param>
    public delegate void OnBatchedHandler<T>(IBatcher sender, List<T> data);

    /// <summary>
    /// 泛型批处理队列：按条数或超时阈值成批取出元素，交给 <see cref="OnBatched"/> 回调。
    /// <para>
    /// 入队占用一个容量名额，出队时归还；容量满时 <see cref="Insert"/> 立即返回 false，
    /// <see cref="InsertWithBackpressureAsync"/> 则在超时内等待名额释放。
    /// </para>
    /// </summary>
    /// <typeparam name="T">批次元素类型</typeparam>
    public sealed class Batcher<T> : IBatcher, IDisposable
    {
        int _size, _timeout, _max;
        int _concurrencyLimit;

        ConcurrentQueue<T> _queue;
        SemaphoreSlim _sendSemaphore;
        SemaphoreSlim _capacitySemaphore;
        bool _stopped;

        /// <summary>
        /// 一批数据达到条数或超时阈值时触发；在后台任务中执行，并发度由构造参数 concurrencyLimit 限制。
        /// </summary>
        public event OnBatchedHandler<T> OnBatched;

        /// <summary>
        /// 批处理实例名称，用于日志与排查。
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// 当前队列中尚未取出的元素个数。
        /// </summary>
        public int PendingCount => _queue.Count;

        /// <summary>
        /// 队列允许积压的最大元素数，达到后入队被拒绝或等待。
        /// </summary>
        public int Capacity => _max;

        /// <summary>
        /// 队列是否已满。
        /// </summary>
        public bool IsFull => _queue.Count >= _max;

        /// <summary>
        /// 创建批处理队列并启动后台成批线程
        /// </summary>
        /// <param name="size">每批最大条数</param>
        /// <param name="timeout">凑不满一批时的最长等待时间（毫秒）</param>
        /// <param name="max">队列最大积压条数，-1 表示按 size × 10 计算</param>
        /// <param name="name">实例名称</param>
        /// <param name="concurrencyLimit">回调并发上限</param>
        public Batcher(int size = 1000, int timeout = 1000, int max = -1, string name = "", int concurrencyLimit = 10)
        {
            _size = size;
            _timeout = timeout;
            if (max == -1) _max = _size * 10;
            else _max = max;
            if (_max < _size) throw new ArgumentOutOfRangeException("max不能小于size");
            _concurrencyLimit = concurrencyLimit;
            Name = name;
            _queue = new ConcurrentQueue<T>();
            _sendSemaphore = new SemaphoreSlim(_concurrencyLimit, _concurrencyLimit);
            _capacitySemaphore = new SemaphoreSlim(_max, _max);
            _stopped = false;
            TaskHelper.LongRunning(Handler);
        }

        /// <summary>
        /// 尝试入队一个元素；队列已满或实例已停止时立即返回 false，不等待。
        /// </summary>
        /// <param name="t">待入队的元素</param>
        /// <returns>是否入队成功</returns>
        public bool Insert(T t)
        {
            if (_stopped) return false;
            if (!_capacitySemaphore.Wait(0)) return false;
            _queue.Enqueue(t);
            return true;
        }

        /// <summary>
        /// 尝试入队一个元素，语义与 <see cref="Insert"/> 相同，返回已完成的任务。
        /// </summary>
        /// <param name="t">待入队的元素</param>
        /// <param name="cancellationToken">取消令牌，当前实现不等待容量，因此不会取消</param>
        /// <returns>是否入队成功</returns>
        public Task<bool> InsertAsync(T t, CancellationToken cancellationToken = default)
        {
            if (_stopped) return Task.FromResult(false);
            if (!_capacitySemaphore.Wait(0)) return Task.FromResult(false);
            _queue.Enqueue(t);
            return Task.FromResult(true);
        }

        /// <summary>
        /// 在超时内等待容量名额后入队；超时、外部取消或实例已停止时返回 false。
        /// </summary>
        /// <param name="t">待入队的元素</param>
        /// <param name="timeoutMs">等待容量名额的超时时间（毫秒）</param>
        /// <param name="cancellationToken">外部取消令牌</param>
        /// <returns>是否入队成功</returns>
        public async Task<bool> InsertWithBackpressureAsync(T t, int timeoutMs = 5000, CancellationToken cancellationToken = default)
        {
            if (_stopped) return false;

            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

            try
            {
                await _capacitySemaphore.WaitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            _queue.Enqueue(t);
            return true;
        }

        private void Handler()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (!_stopped)
            {
                var count = _queue.Count;
                if (count >= _size || (count > 0 && stopwatch.ElapsedMilliseconds >= _timeout))
                {
                    var list = new List<T>();
                    int takeCount = Math.Min(count, _size);
                    for (int i = 0; i < takeCount; i++)
                    {
                        if (_queue.TryDequeue(out T t))
                        {
                            list.Add(t);
                            try
                            {
                                _capacitySemaphore.Release();
                            }
                            catch (SemaphoreFullException)
                            {
                            }
                        }
                    }
                    if (list.Count > 0)
                    {
                        FireOnBatchedAsync(list);
                    }
                    stopwatch.Restart();
                }
                else
                {
                    ThreadHelper.Sleep(Math.Min(_timeout, 100));
                }
            }
        }

        private void FireOnBatchedAsync(List<T> list)
        {
            TaskHelper.Run(async () =>
            {
                try
                {
                    await _sendSemaphore.WaitAsync();
                    try
                    {
                        OnBatched?.Invoke(this, list);
                    }
                    finally
                    {
                        _sendSemaphore.Release();
                    }
                }
                catch (ObjectDisposedException)
                {
                }
            });
        }

        /// <summary>
        /// 清空队列并归还全部容量名额，不触发回调。
        /// </summary>
        public void Clear()
        {
            while (!_queue.IsEmpty)
            {
                if (_queue.TryDequeue(out T _))
                {
                    try
                    {
                        _capacitySemaphore.Release();
                    }
                    catch (SemaphoreFullException)
                    {
                    }
                }
            }
        }

        /// <summary>
        /// 停止后台成批线程，把队列中剩余元素作为最后一批同步交给回调，然后释放信号量。
        /// </summary>
        public void Dispose()
        {
            _stopped = true;
            var count = _queue.Count;
            if (count > 0)
            {
                var list = new List<T>();
                for (int i = 0; i < count; i++)
                {
                    if (_queue.TryDequeue(out T t))
                    {
                        list.Add(t);
                        try
                        {
                            _capacitySemaphore.Release();
                        }
                        catch (SemaphoreFullException)
                        {
                        }
                    }
                }
                OnBatched?.Invoke(this, list);
            }
            _sendSemaphore?.Dispose();
            _capacitySemaphore?.Dispose();
        }
    }


    /// <summary>
    /// 字节数组批量回调
    /// </summary>
    /// <param name="sender">触发回调的批处理实例</param>
    /// <param name="data">本批次按入队顺序拼接后的连续字节</param>
    public delegate void OnBatchedHandler(IBatcher sender, byte[] data);

    /// <summary>
    /// 字节数组批处理队列：把多段数据按条数或超时成批合并为一段连续字节后回调。
    /// </summary>
    public sealed class Batcher : IBatcher, IDisposable
    {
        Batcher<byte[]> _batcher;

        /// <summary>
        /// 一批数据合并完成后触发，合并结果已按入队顺序拼接。
        /// </summary>
        public event OnBatchedHandler OnBatched;

        /// <summary>
        /// 批处理实例名称，转发自内部泛型批处理实例。
        /// </summary>
        public string Name => _batcher.Name;

        /// <summary>
        /// 当前队列中尚未取出的数据段个数。
        /// </summary>
        public int PendingCount => _batcher.PendingCount;

        /// <summary>
        /// 队列允许积压的最大数据段数，达到后入队被拒绝或等待。
        /// </summary>
        public int Capacity => _batcher.Capacity;

        /// <summary>
        /// 队列是否已满。
        /// </summary>
        public bool IsFull => _batcher.IsFull;

        /// <summary>
        /// 创建字节数组批处理队列并启动后台成批线程
        /// </summary>
        /// <param name="size">每批最大数据段数</param>
        /// <param name="timeout">凑不满一批时的最长等待时间（毫秒）</param>
        /// <param name="max">队列最大积压段数，-1 表示按 size × 10 计算</param>
        /// <param name="name">实例名称</param>
        /// <param name="concurrencyLimit">回调并发上限</param>
        public Batcher(int size = 1000, int timeout = 1000, int max = -1, string name = "", int concurrencyLimit = 10)
        {
            _batcher = new Batcher<byte[]>(size, timeout, max, name, concurrencyLimit);

            _batcher.OnBatched += _batcher_OnBatched;
        }

        private void _batcher_OnBatched(IBatcher sender, List<byte[]> data)
        {
            if (data == null || data.Count == 0)
            {
                OnBatched?.Invoke(this, Array.Empty<byte>());
                return;
            }

            var totalLength = 0;
            for (int i = 0; i < data.Count; i++)
            {
                var item = data[i];
                if (item != null) totalLength += item.Length;
            }

            var result = new byte[totalLength];
            var offset = 0;
            for (int i = 0; i < data.Count; i++)
            {
                var item = data[i];
                if (item == null || item.Length == 0) continue;
                Buffer.BlockCopy(item, 0, result, offset, item.Length);
                offset += item.Length;
            }

            OnBatched?.Invoke(this, result);
        }

        /// <summary>
        /// 尝试入队一段字节；队列已满或实例已停止时立即返回 false。
        /// </summary>
        /// <param name="data">待入队的数据段</param>
        /// <returns>是否入队成功</returns>
        public bool Insert(byte[] data)
        {
            return _batcher.Insert(data);
        }

        /// <summary>
        /// 尝试入队一段字节，语义与 <see cref="Insert"/> 相同，返回已完成的任务。
        /// </summary>
        /// <param name="data">待入队的数据段</param>
        /// <param name="cancellationToken">取消令牌，当前实现不等待容量，因此不会取消</param>
        /// <returns>是否入队成功</returns>
        public async Task<bool> InsertAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            return await _batcher.InsertAsync(data, cancellationToken);
        }

        /// <summary>
        /// 在超时内等待容量名额后入队；超时、外部取消或实例已停止时返回 false。
        /// </summary>
        /// <param name="data">待入队的数据段</param>
        /// <param name="timeoutMs">等待容量名额的超时时间（毫秒）</param>
        /// <param name="cancellationToken">外部取消令牌</param>
        /// <returns>是否入队成功</returns>
        public async Task<bool> InsertWithBackpressureAsync(byte[] data, int timeoutMs = 5000, CancellationToken cancellationToken = default)
        {
            return await _batcher.InsertWithBackpressureAsync(data, timeoutMs, cancellationToken);
        }

        /// <summary>
        /// 清空队列并归还容量名额，不触发回调。
        /// </summary>
        public void Clear()
        {
            _batcher?.Clear();
        }

        /// <summary>
        /// 先清空队列，再停止后台线程并释放内部批处理实例。
        /// </summary>
        public void Dispose()
        {
            Clear();
            _batcher?.Dispose();
        }
    }
}