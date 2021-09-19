/****************************************************************************
*Copyright (c) 2018-2021yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Commom
*文件名： SyncHelper
*版本号： v6.0.0.1
*唯一标识：31743e53-5af7-48fb-b248-e1b3504b9c68
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/17 17:26:35
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/17 17:26:35
*修改人： yswenli
*版本号： v6.0.0.1
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace SAEA.Common
{
    /// <summary>
    /// 有序同步工具类
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class OrderSyncHelper<T>
    {
        ConcurrentQueue<T> _queue;

        TimeSpan _timeout;

        object _locker;

        /// <summary>
        /// 有序同步工具类
        /// </summary>
        /// <param name="timeout"></param>
        public OrderSyncHelper(int timeout = 10 * 1000)
        {
            _timeout = TimeSpan.FromMilliseconds(timeout);

            _queue = new ConcurrentQueue<T>();

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
                var date = DateTimeHelper.Now;

                work?.Invoke();

                T t;

                while (!_queue.TryDequeue(out t))
                {
                    if (DateTimeHelper.Now - date > _timeout)
                    {
                        throw new TimeoutException("request is timeout");
                    }
                    Thread.Yield();
                }

                return t;
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
