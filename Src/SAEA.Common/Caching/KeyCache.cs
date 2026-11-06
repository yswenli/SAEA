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
*文件名： KeyCache
*版本号： v26.4.23.1
*唯一标识：a80e8262-5c29-407f-a122-6ad379f35a6d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/16 17:50:24
*描述：KeyCache缓存类
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
using System.Threading;

namespace SAEA.Common.Caching
{
    /// <summary>
    /// 以对象为键、到期自动移除的键缓存
    /// </summary>
    public class KeyCache
    {
        ConcurrentDictionary<object, KeyCacheItem> keyValuePairs = new ConcurrentDictionary<object, KeyCacheItem>();

        /// <summary>
        /// 添加缓存项并在到期后自动移除，键已存在时不覆盖
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <param name="expired">到期时间</param>
        public void Add(object key, DateTime expired)
        {
            var kci = new KeyCacheItem(key, expired);
            kci.OnExpired += Kci_OnExpired;
            keyValuePairs.TryAdd(key, kci);
        }

        private void Kci_OnExpired(KeyCacheItem obj)
        {
            Remove(obj);
        }

        /// <summary>
        /// 按缓存项的键移除，键不存在时忽略
        /// </summary>
        /// <param name="obj">待移除的缓存项</param>
        public void Remove(KeyCacheItem obj)
        {
            keyValuePairs.TryRemove(obj.Key, out KeyCacheItem kci);
        }

        /// <summary>
        /// 按键取缓存项，键不存在时返回 null
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <returns>命中的缓存项，未命中为 null</returns>
        public KeyCacheItem Get(object key)
        {
            if (keyValuePairs.TryGetValue(key, out KeyCacheItem kci))
            {
                return kci;
            }
            return null;
        }

        /// <summary>
        /// 判断键是否存在，不校验是否已到期
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <returns>存在返回 true</returns>
        public bool Contains(object key)
        {
            return keyValuePairs.ContainsKey(key);
        }

        /// <summary>
        /// 清空全部缓存项，其中未到期的项不再触发到期事件
        /// </summary>
        public void Clear()
        {
            keyValuePairs.Clear();
        }
    }

    /// <summary>
    /// 带到期时间的缓存项，构造时启动后台轮询，到期后触发到期事件
    /// </summary>
    public class KeyCacheItem
    {
        /// <summary>
        /// 到期时触发，参数为当前缓存项
        /// </summary>
        public event Action<KeyCacheItem> OnExpired;

        /// <summary>
        /// 缓存键
        /// </summary>
        public object Key
        {
            get; set;
        }

        /// <summary>
        /// 到期时间
        /// </summary>
        public DateTime Expired
        {
            get; set;
        }

        /// <summary>
        /// 初始化缓存项并启动到期轮询，每秒检查一次
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <param name="expired">到期时间</param>
        public KeyCacheItem(object key, DateTime expired)
        {
            this.Key = key;

            this.Expired = expired;

            TaskHelper.LongRunning(() =>
            {
                while (true)
                {
                    if ((expired - DateTimeHelper.Now).TotalMilliseconds <= 0)
                    {
                        OnExpired?.Invoke(this);
                        break;
                    }
                    Thread.Sleep(1000);
                }
            });
        }
    }
}