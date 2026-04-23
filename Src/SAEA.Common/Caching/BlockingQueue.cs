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
*文件名： BlockingQueue
*版本号： v26.4.23.1
*唯一标识：dd1158c8-2348-45de-9bfb-662c7a051a4a
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/16 17:50:24
*描述：BlockingQueue接口
*
*=====================================================================
*修改标记
*修改时间：2020/12/16 17:50:24
*修改人： yswenli
*版本号： v26.4.23.1
*描述：BlockingQueue接口
*
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
