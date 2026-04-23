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
*文件名： ClassificationBatcher
*版本号： v26.4.23.1
*唯一标识：503f9913-be90-408e-9129-e169eb000d12
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/23 17:18:02
*描述：ClassificationBatcher接口
*
*=====================================================================
*修改标记
*修改时间：2020/12/23 17:18:02
*修改人： yswenli
*版本号： v26.4.23.1
*描述：ClassificationBatcher接口
*
*****************************************************************************/
using SAEA.Common.Threading;

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Common.Caching
{
    public delegate void OnClassificationBatchedHandler(string id, byte[] data);

    public sealed class ClassificationBatcher : IDisposable
    {
        ConcurrentDictionary<string, Batcher> _dic;
        ConcurrentDictionary<string, SemaphoreSlim> _semaphores;

        int _size, _timeout, _max, _concurrencyLimit;

        public event OnClassificationBatchedHandler OnBatched;

        public ClassificationBatcher(int size = 1000, int timeout = 1000, int max = -1, int concurrencyLimit = 10)
        {
            _size = size;
            _timeout = timeout;
            _max = max;
            if (_max == -1) _max = _size * 10;
            if (_max < _size) throw new ArgumentOutOfRangeException("max不能小于size");
            _concurrencyLimit = concurrencyLimit;
            _dic = new ConcurrentDictionary<string, Batcher>();
            _semaphores = new ConcurrentDictionary<string, SemaphoreSlim>();
        }


        #region static

        volatile static ClassificationBatcher _classificationBatcher = null;

        static object _locker = new object();

        public static ClassificationBatcher GetInstance(int size = 1000, int timeout = 1000, int max = -1, int concurrencyLimit = 10)
        {
            if (_classificationBatcher == null)
            {
                lock (_locker)
                {
                    if (_classificationBatcher == null)
                    {
                        _classificationBatcher = new ClassificationBatcher(size, timeout, max, concurrencyLimit);
                    }
                }
            }
            return _classificationBatcher;
        }

        #endregion

        private SemaphoreSlim GetOrCreateSemaphore(string name)
        {
            return _semaphores.GetOrAdd(name, _ => new SemaphoreSlim(_concurrencyLimit, _concurrencyLimit));
        }

        private Batcher GetOrCreateBatcher(string name)
        {
            return _dic.GetOrAdd(name, n =>
            {
                var b = new Batcher(_size, _timeout, _max, n, _concurrencyLimit);
                b.OnBatched += Bacher_OnBatched;
                return b;
            });
        }

        public bool Insert(string name, byte[] data)
        {
            var bacher = GetOrCreateBatcher(name);
            return bacher.Insert(data);
        }

        public async Task<bool> InsertAsync(string name, byte[] data, CancellationToken cancellationToken = default)
        {
            var bacher = GetOrCreateBatcher(name);
            return await bacher.InsertAsync(data, cancellationToken);
        }

        public async Task<bool> InsertWithBackpressureAsync(string name, byte[] data, int timeoutMs = 5000, CancellationToken cancellationToken = default)
        {
            var bacher = GetOrCreateBatcher(name);
            return await bacher.InsertWithBackpressureAsync(data, timeoutMs, cancellationToken);
        }

        public bool IsFull(string name)
        {
            if (_dic.TryGetValue(name, out Batcher b))
            {
                return b.IsFull;
            }
            return false;
        }

        public int GetPendingCount(string name)
        {
            if (_dic.TryGetValue(name, out Batcher b))
            {
                return b.PendingCount;
            }
            return 0;
        }

        private void Bacher_OnBatched(IBatcher sender, byte[] data)
        {
            var bacher = (Batcher)sender;
            var semaphore = GetOrCreateSemaphore(bacher.Name);

            TaskHelper.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    OnBatched?.Invoke(bacher.Name, data);
                }
                finally
                {
                    semaphore.Release();
                }
            });
        }

        public void Clear(string name)
        {
            if (_dic.TryGetValue(name, out Batcher b))
            {
                if (b != null)
                {
                    b.OnBatched -= Bacher_OnBatched;
                    b.Dispose();
                    _dic.TryRemove(name, out _);
                }
            }
            if (_semaphores.TryGetValue(name, out SemaphoreSlim sem))
            {
                sem.Dispose();
                _semaphores.TryRemove(name, out _);
            }
        }

        public void Dispose()
        {
            foreach (var item in _dic)
            {
                item.Value.OnBatched -= Bacher_OnBatched;
                item.Value.Dispose();
            }
            _dic.Clear();

            foreach (var item in _semaphores)
            {
                item.Value.Dispose();
            }
            _semaphores.Clear();
        }
    }
}