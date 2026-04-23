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
*文件名： OrderSyncHelper
*版本号： v26.4.23.1
*唯一标识：3295d889-b659-48fe-8aca-228451101e0c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/25 17:36:47
*描述：OrderSyncHelper帮助类
*
*=====================================================================
*修改标记
*修改时间：2020/12/25 17:36:47
*修改人： yswenli
*版本号： v26.4.23.1
*描述：OrderSyncHelper帮助类
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Threading;

using SAEA.Common.Caching;

namespace SAEA.Common
{
    /// <summary>
    /// 有序同步工具类
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class OrderSyncHelper<T>
    {
        BlockingQueue<T> _queue;

        TimeSpan _timeout;

        object _locker;

        /// <summary>
        /// 有序同步工具类
        /// </summary>
        /// <param name="timeout"></param>
        public OrderSyncHelper(int timeout = 10 * 1000)
        {
            _timeout = TimeSpan.FromMilliseconds(timeout);

            _queue = new BlockingQueue<T>();

            _locker = new object();
        }

        /// <summary>
        /// 发出请求并等待
        /// </summary>
        /// <param name="work"></param>
        /// <returns></returns>
        public T Wait(Action work)
        {
            lock (_locker)
            {
                work?.Invoke();

                return _queue.Dequeue(_timeout);
            }
        }

        /// <summary>
        /// 设置回复值
        /// </summary>
        /// <param name="t"></param>
        public void Set(T t)
        {
            _queue.Enqueue(t);
        }
    }
}