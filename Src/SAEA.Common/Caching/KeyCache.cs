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
*唯一标识：73c0ebbd-f623-459a-8114-5bc8244a01da
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/16 17:50:24
*描述：
*
*=====================================================================
*修改标记
*修改时间：2020/12/16 17:50:24
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using SAEA.Common.Threading;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace SAEA.Common.Caching
{
    public class KeyCache
    {
        ConcurrentDictionary<object, KeyCacheItem> keyValuePairs = new ConcurrentDictionary<object, KeyCacheItem>();

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

        public void Remove(KeyCacheItem obj)
        {
            keyValuePairs.TryRemove(obj.Key, out KeyCacheItem kci);
        }

        public KeyCacheItem Get(object key)
        {
            if (keyValuePairs.TryGetValue(key, out KeyCacheItem kci))
            {
                return kci;
            }
            return null;
        }

        public bool Contains(object key)
        {
            return keyValuePairs.ContainsKey(key);
        }

        public void Clear()
        {
            keyValuePairs.Clear();
        }
    }

    public class KeyCacheItem
    {
        public event Action<KeyCacheItem> OnExpired;

        public object Key
        {
            get; set;
        }

        public DateTime Expired
        {
            get; set;
        }

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