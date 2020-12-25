/****************************************************************************
*项目名称：SAEA.Common
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Common
*类 名 称：DisorderSyncHelper
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/24 11:14:10
*描述：
*=====================================================================
*修改时间：2020/12/24 11:14:10
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Collections.Concurrent;

namespace SAEA.Common
{
    /// <summary>
    /// 乱序同步工具类
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DisorderSyncHelper<T>
    {
        ConcurrentDictionary<string, DisorderSyncItem<T>> _cahce;

        int _timeout = 3000;

        /// <summary>
        /// 乱序同步工具类
        /// </summary>
        /// <param name="timeout"></param>
        public DisorderSyncHelper(int timeout = 3000)
        {
            _cahce = new ConcurrentDictionary<string, DisorderSyncItem<T>>();
            _timeout = timeout;
        }

        /// <summary>
        /// 等待
        /// </summary>
        /// <param name="key"></param>
        /// <param name="work"></param>
        /// <returns></returns>
        public T Wait(string key, Action work)
        {
            var date = DateTimeHelper.Now;

            if (_cahce.TryAdd(key, new DisorderSyncItem<T>() { DateTime = date }))
            {
                work.Invoke();

                while (true)
                {
                    if (_cahce.TryGetValue(key, out DisorderSyncItem<T> item))
                    {
                        if (item.DateTime.AddSeconds(_timeout) < DateTimeHelper.Now)
                        {
                            _cahce.TryRemove(key, out DisorderSyncItem<T> _);
                            return default(T);
                        }

                        if (item.HasVal)
                        {
                            return item.Data;
                        }
                    }
                    else
                    {
                        return default(T);
                    }
                    ThreadHelper.Sleep(0);
                }
            }

            return default(T);
        }
        /// <summary>
        /// 触发解锁
        /// </summary>
        /// <param name="key"></param>
        /// <param name="t"></param>
        public void Set(string key, T t)
        {
            _cahce.AddOrUpdate(key, new DisorderSyncItem<T>(), (k, v) =>
            {
                v.HasVal = true;
                v.Data = t;
                return v;
            });
        }
    }

    public class DisorderSyncItem<T>
    {
        public DateTime DateTime { get; set; }

        public T Data { get; set; }

        public bool HasVal { get; set; }
    }

    /// <summary>
    /// 乱序同步工具类
    /// </summary>
    public class DisorderSyncHelper
    {
        DisorderSyncHelper<byte[]> _disorderSyncHelper;

        /// <summary>
        /// 乱序同步工具类
        /// </summary>
        /// <param name="timeout"></param>
        public DisorderSyncHelper(int timeout = 3000)
        {
            _disorderSyncHelper = new DisorderSyncHelper<byte[]>(timeout);
        }
        /// <summary>
        /// 等待
        /// </summary>
        /// <param name="key"></param>
        /// <param name="work"></param>
        /// <returns></returns>
        public byte[] Wait(string key, Action work)
        {
            return _disorderSyncHelper.Wait(key, work);
        }

        /// <summary>
        /// 等待
        /// </summary>
        /// <param name="key"></param>
        /// <param name="work"></param>
        /// <returns></returns>
        public byte[] Wait(long key, Action work)
        {
            return Wait(key.ToString(), work);
        }

        /// <summary>
        /// 触发解锁
        /// </summary>
        /// <param name="key"></param>
        /// <param name="t"></param>
        public void Set(string key, byte[] t)
        {
            _disorderSyncHelper.Set(key, t);
        }
        /// <summary>
        /// 触发解锁
        /// </summary>
        /// <param name="key"></param>
        /// <param name="t"></param>
        public void Set(long key, byte[] t)
        {
            Set(key.ToString(), t);
        }
    }
}
