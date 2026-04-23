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
*命名空间：SAEA.Common
*文件名： ObjectLock
*版本号： v26.4.23.1
*唯一标识：a1b2c3d4-e5f6-7890-abcd-ef1234567890
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2023/10/13 16:42:22
*描述：ObjectLock锁类，提供泛型对象锁功能
*
*=====================================================================
*修改标记
*修改时间：2023/10/13 16:42:22
*修改人： yswenli
*版本号： v26.4.23.1
*描述：ObjectLock锁类，提供泛型对象锁功能
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
