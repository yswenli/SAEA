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

namespace SAEA.Common.Caching
{
    /// <summary>
    /// 自定义过期缓存
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class MemoryCache<T> : IEnumerable<T>, IDisposable
    {
        ConcurrentDictionary<string, MemoryCacheItem<T>> _dic;

        /// <summary>
        /// 数据发生变化时事件
        /// </summary>
        public event Action<MemoryCache<T>, bool, T> OnChanged;

        object _locker = new object();

        /// <summary>
        /// 自定义过期缓存
        /// </summary>
        /// <param name="seconds"></param>
        public MemoryCache(int seconds = 10)
        {
            _dic = new ConcurrentDictionary<string, MemoryCacheItem<T>>();

            ThreadHelper.PulseAction(() =>
            {
                try
                {
                    var values = _dic.Values.Where(b => b.Expired < DateTimeHelper.Now);
                    if (values != null && values.Any())
                    {
                        foreach (var val in values)
                        {
                            if (val != null)
                            {
                                OnChanged?.Invoke(this, false, val.Value);
                            }

                        }
                    }
                }
                catch { }

            }, new TimeSpan(0, 0, seconds), false);
        }

        /// <summary>
        /// Count
        /// </summary>
        public int Count
        {
            get
            {
                return _dic.Count;
            }
        }

        /// <summary>
        /// 索引器
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public T this[string key]
        {
            get
            {
                return Get(key);
            }
        }

        /// <summary>
        /// Set
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="timeOut"></param>
        public void Set(string key, T value, TimeSpan timeOut)
        {
            _dic[key] = new MemoryCacheItem<T>() { Key = key, Value = value, Expired = DateTimeHelper.Now.AddSeconds(timeOut.TotalSeconds) };
            OnChanged?.Invoke(this, true, value);
        }

        /// <summary>
        /// Set
        /// </summary>
        /// <param name="key"></param>
        /// <param name="addFunc"></param>
        /// <param name="timeOut"></param>
        public void Set(string key, Func<string,T> addFunc, TimeSpan timeOut)
        {
            var result = addFunc.Invoke(key);
            Set(key, result, timeOut);
        }

        /// <summary>
        /// Get
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public T Get(string key)
        {
            _dic.TryGetValue(key, out MemoryCacheItem<T> mc);
            if (mc != null && mc.Value != null)
            {
                if (mc.Expired <= DateTimeHelper.Now)
                {
                    _dic.TryRemove(key, out mc);
                    OnChanged?.Invoke(this, false, mc.Value);
                }
                else
                {
                    return mc.Value;
                }
            }
            return default(T);
        }
        /// <summary>
        /// Active
        /// </summary>
        /// <param name="key"></param>
        /// <param name="timeOut"></param>
        public void Active(string key, TimeSpan timeOut)
        {
            var item = Get(key);
            if (item != null)
            {
                Set(key, item, timeOut);
            }
        }

        /// <summary>
        /// GetAndActive
        /// </summary>
        /// <param name="key"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public T GetAndActive(string key, TimeSpan timeOut)
        {
            _dic.TryGetValue(key, out MemoryCacheItem<T> mc);

            if (mc != null && mc.Value != null)
            {
                if (mc.Expired <= DateTimeHelper.Now)
                {
                    _dic.TryRemove(key, out mc);
                    OnChanged?.Invoke(this, false, mc.Value);
                }
                else
                {
                    Set(key, mc.Value, timeOut);
                    return mc.Value;
                }
            }
            return default(T);
        }


        public T GetOrAdd(string key, Func<string, T> addFunc, TimeSpan timeOut)
        {
            var mt = _dic.GetOrAdd(key, (k) =>
            {
                return new MemoryCacheItem<T>()
                {
                    Key = k,
                    Expired = DateTimeHelper.Now.AddSeconds(timeOut.TotalSeconds),
                    Value = addFunc.Invoke(k)
                };
             });
            lock (_locker)
            {
                if (mt.Expired > DateTimeHelper.Now)
                {
                    return mt.Value;
                }
                else
                {
                    var result = addFunc.Invoke(key);
                    Set(key, result, timeOut);
                    return result;
                }
            }
        }

        /// <summary>
        /// Del
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Del(string key)
        {
            var result = _dic.TryRemove(key, out MemoryCacheItem<T> mc);
            if (result)
            {
                OnChanged?.Invoke(this, false, mc.Value);
            }
            return result;
        }
        /// <summary>
        /// DelWithoutEvent
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool DelWithoutEvent(string key)
        {
            return _dic.TryRemove(key, out MemoryCacheItem<T> mc);
        }

        /// <summary>
        /// List
        /// </summary>
        public IEnumerable<T> List
        {
            get
            {
                return _dic.Values.Select(b => b.Value);
            }
        }

        /// <summary>
        /// ToList
        /// </summary>
        /// <returns></returns>
        public ICollection<MemoryCacheItem<T>> ToList()
        {
            return _dic.Values;
        }

        /// <summary>
        /// Clear
        /// </summary>
        public void Clear()
        {
            _dic.Clear();
            OnChanged?.Invoke(this, false, default(T));
        }


        public IEnumerator<T> GetEnumerator()
        {
            return List.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }


        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            Clear();
            _dic = null;
        }
    }

    /// <summary>
    /// 缓存项
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class MemoryCacheItem<T>
    {
        public string Key
        {
            get; set;
        }

        public T Value
        {
            get; set;
        }

        public DateTime Expired
        {
            get; set;
        }
    }
}
