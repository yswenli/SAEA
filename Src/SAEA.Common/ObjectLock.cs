/****************************************************************************
*Copyright (c) 2023 RiverLand All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WALLE
*公司名称：河之洲
*命名空间：SAEA.Common
*文件名： ObjectLock
*版本号： V1.0.0.0
*唯一标识：58023d8b-99a4-4b95-81a2-70beba529540
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：walle.wen@tjingcai.com
*创建时间：2023/10/13 16:42:22
*描述：对象锁
*
*=================================================
*修改标记
*修改时间：2023/10/13 16:42:22
*修改人： yswenli
*版本号： V1.0.0.0
*描述：对象锁
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading;

namespace SAEA.Common
{
    /// <summary>
    /// 对象锁
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ObjectLock<T> : IDisposable where T : notnull
    {
        object _obj;

        /// <summary>
        /// 对象锁
        /// </summary>
        /// <param name="key"></param>
        /// <param name="timeOut"></param>
        public ObjectLock(T key, int timeOut = 0)
        {
            _obj = VariableLockCache.GetOrAnd(key);

            if (timeOut < 1)
            {
                Monitor.Enter(_obj);
            }
            else
            {
                bool _lockTaken = false;

                Monitor.TryEnter(_obj, timeOut, ref _lockTaken);

                if (!_lockTaken)
                {
                    throw new TimeoutException("Acquire lock timeout");
                }
            }
        }

        /// <summary>
        /// 释放锁
        /// </summary>
        public void Dispose()
        {
            Monitor.Exit(_obj);
        }

        /// <summary>
        /// 创建对象锁
        /// </summary>
        /// <param name="key"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static ObjectLock<T> Create(T key, int timeOut = 0) => new ObjectLock<T>(key, timeOut);

        /// <summary>
        /// 返回一个默认的全局锁
        /// </summary>
        public static ObjectLock<string> Global
        {
            get
            {
                return new ObjectLock<string>("yswenli");
            }
        }
    }

    /// <summary>
    /// 对象锁
    /// </summary>
    public class ObjectLock : ObjectLock<string>
    {
        /// <summary>
        /// 对象锁
        /// </summary>
        /// <param name="key"></param>
        /// <param name="timeOut"></param>
        public ObjectLock(string key, int timeOut = 0) : base(key, timeOut)
        {

        }

        /// <summary>
        /// 创建对象锁
        /// </summary>
        /// <param name="key"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static new ObjectLock Create(string key, int timeOut = 0) => new ObjectLock(key, timeOut);
    }

    /// <summary>
    /// 可变锁缓存
    /// </summary>
    public static class VariableLockCache
    {
        static Dictionary<object, object> _cache;
        static object _lockObj;

        /// <summary>
        /// 可变锁缓存
        /// </summary>
        static VariableLockCache()
        {
            _cache = new Dictionary<object, object>();
            _lockObj = new object();
        }

        /// <summary>
        /// 添加或获取
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static object GetOrAnd(object key)
        {
            lock (_lockObj)
            {
                object obj;
                if (_cache.ContainsKey(key))
                {
                    obj = _cache[key];
                }
                else
                {
                    obj = new object();
                    _cache.Add(key, obj);
                }
                return obj;
            }
        }
    }
}
