/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.Commom
*文件名： KeyCache
*版本号： V3.3.3.5
*唯一标识：bf3043aa-a84d-42ab-a6b6-b3adf2ab8925
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*修改时间：2018/12/14 16:53:26
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/12/14 16:53:26
*修改人： yswenli
*版本号： V3.3.3.5
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace SAEA.Common
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
        Timer timer;

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
            timer = new Timer(new TimerCallback((k) =>
            {
                OnExpired?.Invoke((KeyCacheItem)k);
            }), this, (int)(expired - DateTime.Now).TotalMilliseconds, -1);
        }
    }
}
