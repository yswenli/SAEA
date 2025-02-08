/****************************************************************************
*Copyright (c) 2018-2022yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Commom.Caching
*文件名： MemoryCache
*版本号： v7.0.0.1
*唯一标识：13ed79e2-020d-45aa-a67f-ec00cf30da2d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/13 9:31:54
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/13 9:31:54
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace SAEA.Common.Caching
{
    /// <summary>
    /// 自定义过期缓存
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class MemoryCache<T> : IEnumerable<T>, IDisposable
    {
        ConcurrentDictionary<string, MemoryCacheItem<T>> _dic;
        CancellationTokenSource _cancellationTokenSource;
        public event Action<MemoryCache<T>, bool, T> OnChanged;

        public MemoryCache(int seconds = 180)
        {
            _dic = new ConcurrentDictionary<string, MemoryCacheItem<T>>();
            _cancellationTokenSource = new CancellationTokenSource();
            StartCleanupTimer(seconds, _cancellationTokenSource.Token);
        }

        public int Count => _dic.Count;

        public T this[string key] => Get(key);

        public void Set(string key, T value, TimeSpan timeOut)
        {
            var expired = timeOut.TotalSeconds < 1 ? DateTime.MaxValue : DateTime.Now.AddSeconds(timeOut.TotalSeconds);
            _dic[key] = new MemoryCacheItem<T> { Key = key, Value = value, Expired = expired };
            OnChanged?.Invoke(this, true, value);
        }

        public void Set(string key, Func<string, T> addFunc, TimeSpan timeOut)
        {
            Set(key, addFunc(key), timeOut);
        }

        public T Get(string key)
        {
            if (_dic.TryGetValue(key, out var mc) && mc != null && mc.Expired > DateTime.Now)
            {
                return mc.Value;
            }
            _dic.TryRemove(key, out _);
            if (mc != null)
                OnChanged?.Invoke(this, false, mc.Value);
            return default;
        }

        public void Active(string key, TimeSpan timeOut)
        {
            var item = Get(key);
            if (item != null)
            {
                Set(key, item, timeOut);
            }
        }

        public T GetAndActive(string key, TimeSpan timeOut)
        {
            if (_dic.TryGetValue(key, out var mc) && mc != null && mc.Expired > DateTime.Now)
            {
                Set(key, mc.Value, timeOut);
                return mc.Value;
            }
            return default;
        }

        public T GetOrAdd(string key, Func<string, T> addFunc, TimeSpan timeOut)
        {
            var expired = DateTime.Now.AddSeconds(timeOut.TotalSeconds);
            var mt = _dic.AddOrUpdate(key, new MemoryCacheItem<T> { Key = key, Value = addFunc(key), Expired = expired },
                (k, v) => v.Expired > DateTime.Now ? v : new MemoryCacheItem<T> { Key = k, Value = addFunc(k), Expired = expired });
            return mt.Value;
        }

        public bool Del(string key)
        {
            if (_dic.TryRemove(key, out var mc))
            {
                OnChanged?.Invoke(this, false, mc.Value);
                return true;
            }
            return false;
        }

        public bool DelWithoutEvent(string key)
        {
            MemoryCacheItem<T> mc;
            return _dic.TryRemove(key, out mc) && mc != null && mc.Value != null;
        }

        public IEnumerable<T> List => _dic.Values.Select(b => b.Value);

        public ICollection<MemoryCacheItem<T>> ToList() => _dic.Values;

        public void Clear()
        {
            _dic.Clear();
            OnChanged?.Invoke(this, false, default);
        }

        public IEnumerator<T> GetEnumerator() => List.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public void Dispose()
        {
            Clear();
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
        }

        void StartCleanupTimer(int seconds, CancellationToken token)
        {
            Task.Factory.StartNew(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(seconds * 1000, token);
                    var keysToRemove = _dic.Where(kvp => kvp.Value.Expired <= DateTime.Now).Select(kvp => kvp.Key).ToList();
                    foreach (var key in keysToRemove)
                    {
                        Del(key);
                    }
                }
            }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
    }

    /// <summary>
    /// 缓存项
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class MemoryCacheItem<T>
    {
        public string Key { get; set; }
        public T Value { get; set; }
        public DateTime Expired { get; set; }
    }
}
