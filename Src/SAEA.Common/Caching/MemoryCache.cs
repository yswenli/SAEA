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
*文件名： MemoryCache
*版本号： v26.4.23.1
*唯一标识：e1b28046-3911-4b2e-92c3-4ad9f7a72e13
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/04/08 20:01:30
*描述：MemoryCache缓存类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:45
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
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
    /// <typeparam name="T">缓存值的类型</typeparam>
    public class MemoryCache<T> : IEnumerable<T>, IDisposable
    {
        ConcurrentDictionary<string, MemoryCacheItem<T>> _dic;
        CancellationTokenSource _cancellationTokenSource;
        /// <summary>
        /// 缓存项新增、更新或移除时触发，第二参数为 true 表示新增或更新、false 表示移除
        /// </summary>
        public event Action<MemoryCache<T>, bool, T> OnChanged;

        /// <summary>
        /// 初始化缓存并启动过期清理任务
        /// </summary>
        /// <param name="seconds">清理任务扫描过期项的间隔秒数，默认 180</param>
        public MemoryCache(int seconds = 180)
        {
            _dic = new ConcurrentDictionary<string, MemoryCacheItem<T>>();
            _cancellationTokenSource = new CancellationTokenSource();
            StartCleanupTimer(seconds, _cancellationTokenSource.Token);
        }

        /// <summary>
        /// 当前缓存项数量，含已过期但尚未清理的项
        /// </summary>
        public int Count => _dic.Count;

        /// <summary>
        /// 按键读取缓存值，键为空或已过期时返回默认值
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <returns>缓存值，未命中为默认值</returns>
        public T this[string key] => Get(key);

        /// <summary>
        /// 写入或覆盖缓存项
        /// </summary>
        /// <param name="key">缓存键，为空时直接返回</param>
        /// <param name="value">缓存值</param>
        /// <param name="timeOut">存活时长，小于 1 秒时视为永不过期</param>
        public void Set(string key, T value, TimeSpan timeOut)
        {
            if (string.IsNullOrEmpty(key)) return;
            var expired = timeOut.TotalSeconds < 1 ? DateTime.MaxValue : DateTime.Now.AddSeconds(timeOut.TotalSeconds);
            _dic[key] = new MemoryCacheItem<T> { Key = key, Value = value, Expired = expired };
            OnChanged?.Invoke(this, true, value);
        }

        /// <summary>
        /// 用委托生成的值写入或覆盖缓存项，生成逻辑在写入前执行
        /// </summary>
        /// <param name="key">缓存键，为空时委托仍会执行但其结果被丢弃</param>
        /// <param name="addFunc">按键生成缓存值的委托</param>
        /// <param name="timeOut">存活时长，小于 1 秒时视为永不过期</param>
        public void Set(string key, Func<string, T> addFunc, TimeSpan timeOut)
        {
            Set(key, addFunc(key), timeOut);
        }

        /// <summary>
        /// 读取未过期的缓存值，命中失败时移除该项并触发变更事件
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <returns>缓存值，键为空、不存在或已过期时为默认值</returns>
        public T Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return default;
            if (_dic.TryGetValue(key, out var mc) && mc != null && mc.Expired > DateTime.Now)
            {
                return mc.Value;
            }
            // 只有当 TryRemove 成功删除时才触发 OnChanged，避免竞争条件导致重复触发
            if (_dic.TryRemove(key, out var removed) && removed != null)
            {
                OnChanged?.Invoke(this, false, removed.Value);
            }
            return default;
        }

        /// <summary>
        /// 续期已有缓存项，键不存在或已过期时会写入默认值
        /// </summary>
        /// <param name="key">缓存键，为空时直接返回</param>
        /// <param name="timeOut">新的存活时长，小于 1 秒时视为永不过期</param>
        public void Active(string key, TimeSpan timeOut)
        {
            if (string.IsNullOrEmpty(key)) return;
            var item = Get(key);
            if (item != null)
            {
                Set(key, item, timeOut);
            }
        }

        /// <summary>
        /// 读取缓存值并同步续期
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <param name="timeOut">新的存活时长，小于 1 秒时视为永不过期</param>
        /// <returns>缓存值，键为空、不存在或已过期时为默认值</returns>
        public T GetAndActive(string key, TimeSpan timeOut)
        {
            if (string.IsNullOrEmpty(key)) return default;
            if (_dic.TryGetValue(key, out var mc) && mc != null && mc.Expired > DateTime.Now)
            {
                Set(key, mc.Value, timeOut);
                return mc.Value;
            }
            return default;
        }

        /// <summary>
        /// 取已有未过期的缓存值，未命中时用委托生成并写入
        /// </summary>
        /// <param name="key">缓存键，为空时返回默认值</param>
        /// <param name="addFunc">按键生成缓存值的委托</param>
        /// <param name="timeOut">写入时的存活时长，按秒计算</param>
        /// <returns>缓存中的值</returns>
        public T GetOrAdd(string key, Func<string, T> addFunc, TimeSpan timeOut)
        {
            if (string.IsNullOrEmpty(key)) return default;
            var expired = DateTime.Now.AddSeconds(timeOut.TotalSeconds);
            var mt = _dic.AddOrUpdate(key, new MemoryCacheItem<T> { Key = key, Value = addFunc(key), Expired = expired },
                (k, v) => v.Expired > DateTime.Now ? v : new MemoryCacheItem<T> { Key = k, Value = addFunc(k), Expired = expired });
            return mt.Value;
        }

        /// <summary>
        /// 删除缓存项并触发变更事件
        /// </summary>
        /// <param name="key">缓存键，为空时返回 false</param>
        /// <returns>实际删除返回 true，键不存在返回 false</returns>
        public bool Del(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (_dic.TryRemove(key, out var mc))
            {
                OnChanged?.Invoke(this, false, mc.Value);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 删除缓存项且不触发变更事件
        /// </summary>
        /// <param name="key">缓存键，为空时返回 false</param>
        /// <returns>删除成功且值非 null 时返回 true</returns>
        public bool DelWithoutEvent(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            MemoryCacheItem<T> mc;
            return _dic.TryRemove(key, out mc) && mc != null && mc.Value != null;
        }

        /// <summary>
        /// 全部缓存值的快照，含已过期未清理的项
        /// </summary>
        public IEnumerable<T> List => _dic.Values.Select(b => b.Value);

        /// <summary>
        /// 取出全部缓存项对象，便于查看键与到期时间
        /// </summary>
        /// <returns>缓存项集合，直接引用内部字典的值集合</returns>
        public ICollection<MemoryCacheItem<T>> ToList() => _dic.Values;

        /// <summary>
        /// 清空全部缓存项并触发一次移除事件，值参数为默认值
        /// </summary>
        public void Clear()
        {
            _dic.Clear();
            OnChanged?.Invoke(this, false, default);
        }

        /// <summary>
        /// 遍历全部缓存值，含已过期未清理的项
        /// </summary>
        /// <returns>缓存值的枚举器</returns>
        public IEnumerator<T> GetEnumerator() => List.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// 清空缓存并停止过期清理任务
        /// </summary>
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
    /// <typeparam name="T">缓存值的类型</typeparam>
    public class MemoryCacheItem<T>
    {
        /// <summary>
        /// 缓存键
        /// </summary>
        public string Key { get; set; }
        /// <summary>
        /// 缓存值
        /// </summary>
        public T Value { get; set; }
        /// <summary>
        /// 到期时间，为最大值时表示永不过期
        /// </summary>
        public DateTime Expired { get; set; }
    }
}