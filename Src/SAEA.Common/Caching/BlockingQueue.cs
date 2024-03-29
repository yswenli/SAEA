﻿/****************************************************************************
*项目名称：SAEA.Common
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Common.Caching
*类 名 称：BlockingQueue
*版本号： v7.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2016/1/16 9:43:28
*描述：
*=====================================================================
*修改时间：2019/1/16 9:43:28
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading;

namespace SAEA.Common.Caching
{
    /// <summary>
    /// 阻塞试队列
    /// </summary>
    /// <typeparam name="TItem"></typeparam>
    public class BlockingQueue<TItem>
    {
        private readonly object _syncRoot = new object();
        private readonly LinkedList<TItem> _items = new LinkedList<TItem>();
        private readonly ManualResetEvent _gate = new ManualResetEvent(true);


        int _readTime = 0;

        /// <summary>
        /// 长度
        /// </summary>
        public int Count
        {
            get
            {
                lock (_syncRoot)
                {
                    return _items.Count;
                }
            }
        }

        public bool IsEmpty
        {
            get
            {
                return Count == 0;
            }
        }

        /// <summary>
        /// 入队
        /// </summary>
        /// <param name="item"></param>
        public void Enqueue(TItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            lock (_syncRoot)
            {
                _items.AddLast(item);
            }
            if (Interlocked.Exchange(ref _readTime, 1) == 0)
            {
                _gate.Set();
            }
        }

        /// <summary>
        /// 出队
        /// </summary>
        /// <param name="maxTimeout"></param>
        /// <returns></returns>
        public TItem Dequeue(int maxTimeout = 10 * 1000)
        {
            while (true)
            {
                if (!_gate.WaitOne(maxTimeout))
                {
                    _gate.Set();
                    return default;
                }
                lock (_syncRoot)
                {
                    if (_items.Count > 0)
                    {
                        var item = _items.First.Value;
                        _items.RemoveFirst();
                        _gate.Set();
                        return item;
                    }
                    else
                    {
                        _gate.Reset();
                        Interlocked.Exchange(ref _readTime, 0);
                    }
                }
            }
        }

        /// <summary>
        /// 出队
        /// </summary>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public TItem Dequeue(TimeSpan timeOut)
        {
            return Dequeue((int)timeOut.TotalMilliseconds);
        }

        /// <summary>
        /// 查看
        /// </summary>
        /// <param name="maxTimeout"></param>
        /// <returns></returns>
        public TItem PeekAndWait(int maxTimeout = 10 * 1000)
        {
            while (true)
            {
                if (!_gate.WaitOne(maxTimeout))
                {
                    return default(TItem);
                }
                lock (_syncRoot)
                {
                    if (_items.Count > 0)
                    {
                        _gate.Set();
                        return _items.First.Value;
                    }
                    else
                    {
                        _gate.Reset();
                        Interlocked.Exchange(ref _readTime, 0);
                    }
                }
            }
        }

        /// <summary>
        /// 移除首元素
        /// </summary>
        /// <param name="match"></param>
        public void RemoveFirst(Predicate<TItem> match)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));

            lock (_syncRoot)
            {
                if (_items.Count > 0 && match(_items.First.Value))
                {
                    _items.RemoveFirst();
                }
            }
        }

        /// <summary>
        /// 移除首元素
        /// </summary>
        /// <returns></returns>
        public TItem RemoveFirst()
        {
            lock (_syncRoot)
            {
                var item = _items.First;
                _items.RemoveFirst();

                return item.Value;
            }
        }

        /// <summary>
        /// 清理
        /// </summary>
        public void Clear()
        {
            lock (_syncRoot)
            {
                _items.Clear();
                _gate.Set();
            }
        }
    }
}
