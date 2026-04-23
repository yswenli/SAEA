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
*唯一标识：9f2e2b07-b756-4bb5-9c0a-d2d31e021b08
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading;

namespace SAEA.MQTT.Internal
{
    public sealed class BlockingQueue<TItem> : IDisposable
    {
        readonly object _syncRoot = new object();
        readonly LinkedList<TItem> _items = new LinkedList<TItem>();

        ManualResetEventSlim _gate = new ManualResetEventSlim(false);

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

        public void Enqueue(TItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            lock (_syncRoot)
            {
                _items.AddLast(item);
                _gate?.Set();
            }
        }

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

        public TItem RemoveFirst()
        {
            lock (_syncRoot)
            {
                var item = _items.First;
                _items.RemoveFirst();

                return item.Value;
            }
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                _items.Clear();
            }
        }

        public void Dispose()
        {
            _gate?.Dispose();
            _gate = null;
        }
    }
}
