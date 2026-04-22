/****************************************************************************
*项目名称：SAEA.Common.Caching
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Common.Caching
*类 名 称：Batcher
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/17 11:00:17
*描述：
*=====================================================================
*修改时间：2020/12/17 11:00:17
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
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
    public delegate void OnBatchedHandler<T>(IBatcher sender, List<T> data);

    public sealed class Batcher<T> : IBatcher, IDisposable
    {
        int _size, _timeout, _max;
        int _concurrencyLimit;
        int _highWatermark;

        ConcurrentQueue<T> _queue;
        SemaphoreSlim _sendSemaphore;
        SemaphoreSlim _capacitySemaphore;
        bool _stopped;

        public event OnBatchedHandler<T> OnBatched;

        public string Name { get; private set; }

        public int PendingCount => _queue.Count;

        public int Capacity => _max;

        public int HighWatermark => _highWatermark;

        public bool IsFull => _queue.Count >= _max;

        public bool IsAboveHighWatermark => _queue.Count >= _highWatermark;

        public Batcher(int size = 1000, int timeout = 1000, int max = -1, string name = "", int concurrencyLimit = 10, double highWatermarkRatio = 0.8)
        {
            _size = size;
            _timeout = timeout;
            if (max == -1) _max = _size * 10;
            else _max = max;
            if (_max < _size) throw new ArgumentOutOfRangeException("max不能小于size");
            _highWatermark = (int)(_max * highWatermarkRatio);
            if (_highWatermark < _size) _highWatermark = _size;
            _concurrencyLimit = concurrencyLimit;
            Name = name;
            _queue = new ConcurrentQueue<T>();
            _sendSemaphore = new SemaphoreSlim(_concurrencyLimit, _concurrencyLimit);
            _capacitySemaphore = new SemaphoreSlim(_max, _max);
            _stopped = false;
            TaskHelper.LongRunning(Handler);
        }

        public bool Insert(T t)
        {
            if (_queue.Count >= _max)
            {
                return false;
            }
            _queue.Enqueue(t);
            return true;
        }

        public async Task<bool> InsertAsync(T t, CancellationToken cancellationToken = default)
        {
            if (_queue.Count >= _max)
            {
                return false;
            }
            _queue.Enqueue(t);
            return true;
        }

        public async Task<bool> InsertWithBackpressureAsync(T t, int timeoutMs = 5000, CancellationToken cancellationToken = default)
        {
            if (_stopped) return false;

            var currentCount = _queue.Count;
            if (currentCount >= _max)
            {
                using var timeoutCts = new CancellationTokenSource(timeoutMs);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);
                
                try
                {
                    await WaitForCapacityAsync(1, linkedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            }

            _queue.Enqueue(t);
            return true;
        }

        public async Task WaitForCapacityAsync(int requiredSpace, CancellationToken cancellationToken = default)
        {
            if (_stopped) return;

            var currentCount = _queue.Count;
            var availableSpace = _max - currentCount;

            if (availableSpace >= requiredSpace)
            {
                return;
            }

            var permitsNeeded = requiredSpace - availableSpace;

            for (int i = 0; i < permitsNeeded; i++)
            {
                await _capacitySemaphore.WaitAsync(cancellationToken);
            }
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
                    int actuallyTaken = 0;
                    for (int i = 0; i < takeCount; i++)
                    {
                        if (_queue.TryDequeue(out T t))
                        {
                            list.Add(t);
                            actuallyTaken++;
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
                await _sendSemaphore.WaitAsync();
                try
                {
                    OnBatched?.Invoke(this, list);
                }
                finally
                {
                    _sendSemaphore.Release();
                }
            });
        }

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
                    }
                }
                OnBatched?.Invoke(this, list);
            }
            _sendSemaphore?.Dispose();
            _capacitySemaphore?.Dispose();
        }
    }


    public delegate void OnBatchedHandler(IBatcher sender, byte[] data);

    public sealed class Batcher : IBatcher, IDisposable
    {
        Batcher<byte[]> _batcher;

        public event OnBatchedHandler OnBatched;

        public string Name => _batcher.Name;

        public int PendingCount => _batcher.PendingCount;

        public int Capacity => _batcher.Capacity;

        public int HighWatermark => _batcher.HighWatermark;

        public bool IsFull => _batcher.IsFull;

        public bool IsAboveHighWatermark => _batcher.IsAboveHighWatermark;

        public Batcher(int size = 1000, int timeout = 1000, int max = -1, string name = "", int concurrencyLimit = 10, double highWatermarkRatio = 0.8)
        {
            _batcher = new Batcher<byte[]>(size, timeout, max, name, concurrencyLimit, highWatermarkRatio);

            _batcher.OnBatched += _batcher_OnBatched;
        }

        private void _batcher_OnBatched(IBatcher sender, List<byte[]> data)
        {
            var result = new List<Byte>();
            foreach (var item in data)
            {
                result.AddRange(item);
            }
            OnBatched?.Invoke(this, result.ToArray());
            result.Clear();
        }

        public bool Insert(byte[] data)
        {
            return _batcher.Insert(data);
        }

        public async Task<bool> InsertAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            return await _batcher.InsertAsync(data, cancellationToken);
        }

        public async Task<bool> InsertWithBackpressureAsync(byte[] data, int timeoutMs = 5000, CancellationToken cancellationToken = default)
        {
            return await _batcher.InsertWithBackpressureAsync(data, timeoutMs, cancellationToken);
        }

        public async Task WaitForCapacityAsync(int requiredSpace, CancellationToken cancellationToken = default)
        {
            await _batcher.WaitForCapacityAsync(requiredSpace, cancellationToken);
        }

        public void Clear()
        {
            _batcher?.Clear();
        }

        public void Dispose()
        {
            Clear();
            _batcher?.Dispose();
        }
    }
}