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
*文件名： DisorderSyncHelper
*版本号： v26.4.23.1
*唯一标识：a90cd2a9-b651-4515-a8ba-e619e7bf7ec1
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/25 17:36:47
*描述：DisorderSyncHelper接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 15:20:36
*修改人： yswenli
*版本号： v26.11.5.1
*描述：Set 先写 Data 再发布 HasVal，HasVal 改用 Volatile 读写，避免等待方读到未赋值的空结果
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Threading;

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
                // 先落结果再发布就绪标记：等待方随时可能读到 HasVal=true，
                // 顺序反了会取到还没赋值的 Data（表现为偶发返回 null）。
                v.Data = t;
                v.HasVal = true;
                return v;
            });
        }
    }

    /// <summary>
    /// 乱序同步项，记录等待起点、结果数据与结果是否就绪
    /// </summary>
    public class DisorderSyncItem<T>
    {
        private bool _hasVal;

        /// <summary>
        /// 等待起始时间，用于判定超时
        /// </summary>
        public DateTime DateTime { get; set; }

        /// <summary>
        /// 等待到的结果数据
        /// </summary>
        public T Data { get; set; }

        /// <summary>
        /// 结果是否已设置；置为 true 即表示 <see cref="Data"/> 已可读
        /// </summary>
        public bool HasVal
        {
            // 读写都走 Volatile：写入方先写 Data 再发布本标记，读取方看到 true 就必须能看到 Data，
            // 普通字段在弱内存序平台（ARM64）上不保证这个先后关系。
            get { return Volatile.Read(ref _hasVal); }
            set { Volatile.Write(ref _hasVal, value); }
        }
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
