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
*命名空间：SAEA.MQTT.Internal
*文件名： BlockingQueue
*版本号： v26.4.23.1
*唯一标识：82a2d0c6-3847-41e9-be8a-8ac51b0a8497
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：BlockingQueue接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading;

namespace SAEA.MQTT.Internal
{
    /// <summary>
    /// 基于双向链表的阻塞队列，出队与查看操作会阻塞到有元素可用为止
    /// </summary>
    /// <typeparam name="TItem">队列元素类型</typeparam>
    public sealed class BlockingQueue<TItem> : IDisposable
    {
        readonly object _syncRoot = new object();
        readonly LinkedList<TItem> _items = new LinkedList<TItem>();

        ManualResetEventSlim _gate = new ManualResetEventSlim(false);

        /// <summary>
        /// 队列中当前元素数量
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

        /// <summary>
        /// 入队元素并唤醒等待中的调用
        /// </summary>
        /// <param name="item">待入队的元素，不能为 null</param>
        public void Enqueue(TItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            lock (_syncRoot)
            {
                _items.AddLast(item);
                _gate?.Set();
            }
        }

        /// <summary>
        /// 阻塞取出队首元素并将其从队列移除，取消等待时抛出 OperationCanceledException
        /// </summary>
        /// <param name="cancellationToken">取消等待的令牌</param>
        /// <returns>取出的队首元素</returns>
        public TItem Dequeue(CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                lock (_syncRoot)
                {
                    if (_items.Count > 0)
                    {
                        var item = _items.First.Value;
                        _items.RemoveFirst();

                        return item;
                    }

                    if (_items.Count == 0)
                    {
                        _gate?.Reset();
                    }
                }

                _gate?.Wait(cancellationToken);
            }

            throw new OperationCanceledException();
        }

        /// <summary>
        /// 阻塞查看队首元素但不移除，取消等待时抛出 OperationCanceledException
        /// </summary>
        /// <param name="cancellationToken">取消等待的令牌</param>
        /// <returns>队首元素</returns>
        public TItem PeekAndWait(CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                lock (_syncRoot)
                {
                    if (_items.Count > 0)
                    {
                        return _items.First.Value;
                    }

                    if (_items.Count == 0)
                    {
                        _gate?.Reset();
                    }
                }

                _gate?.Wait(cancellationToken);
            }

            throw new OperationCanceledException();
        }

        /// <summary>
        /// 队首元素满足条件时将其移除，队列为空或首元素不匹配时不做任何操作
        /// </summary>
        /// <param name="match">队首元素的匹配条件，不能为 null</param>
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
        /// 移除并返回队首元素，队列为空时当前实现会抛出 NullReferenceException
        /// </summary>
        /// <returns>被移除的队首元素</returns>
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
        /// 移除队列中的全部元素
        /// </summary>
        public void Clear()
        {
            lock (_syncRoot)
            {
                _items.Clear();
            }
        }

        /// <summary>
        /// 释放内部等待句柄，之后等待与入队操作不再生效
        /// </summary>
        public void Dispose()
        {
            _gate?.Dispose();
            _gate = null;
        }
    }
}
