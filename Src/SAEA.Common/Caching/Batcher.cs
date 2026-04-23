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
*唯一标识：748f3051-b5b0-4c3d-b25b-1ff348489e13
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/23 17:18:02
*描述：
*
*=====================================================================
*修改标记
*修改时间：2020/12/23 17:18:02
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
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
    public delegate void OnBatchedHandler<T>(IBatcher sender, List<T> data);

    public sealed class Batcher<T> : IBatcher, IDisposable
    {
        int _size, _timeout, _max;
        int _concurrencyLimit;

        ConcurrentQueue<T> _queue;
        SemaphoreSlim _sendSemaphore;
        SemaphoreSlim _capacitySemaphore;
        bool _stopped;

        public event OnBatchedHandler<T> OnBatched;

        public string Name { get; private set; }

        public int PendingCount => _queue.Count;

        public int Capacity => _max;

        public bool IsFull => _queue.Count >= _max;

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


    public delegate void OnBatchedHandler(IBatcher sender, byte[] data);

    public sealed class Batcher : IBatcher, IDisposable
    {
        Batcher<byte[]> _batcher;

        public event OnBatchedHandler OnBatched;

        public string Name => _batcher.Name;

        public int PendingCount => _batcher.PendingCount;

        public int Capacity => _batcher.Capacity;

        public bool IsFull => _batcher.IsFull;

        public Batcher(int size = 1000, int timeout = 1000, int max = -1, string name = "", int concurrencyLimit = 10)
        {
            _batcher = new Batcher<byte[]>(size, timeout, max, name, concurrencyLimit);

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