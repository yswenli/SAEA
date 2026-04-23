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
*文件名： ThreadQueue
*版本号： v26.4.23.1
*唯一标识：1d215821-6e7e-4180-88d7-8df68bdcafb2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2022/09/18 23:08:16
*描述：
*
*=====================================================================
*修改标记
*修改时间：2022/09/18 23:08:16
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;

namespace SAEA.Common.Caching
{
    /// <summary>
    /// 线程队列
    /// </summary>
    public class ThreadQueue<T> : IDisposable
    {
        BlockingCollection<T>[] _collection;

        /// <summary>
        /// 线程队列
        /// </summary>
        public ThreadQueue()
        {
            int count = Environment.ProcessorCount / 2;
            if (count < 1) count = 1;
            _collection = new BlockingCollection<T>[count];
            for (int i = 0; i < count; i++)
            {
                _collection[i]= new BlockingCollection<T>();
            }
        }

        /// <summary>
        /// 数量
        /// </summary>
        public int Count
        {
            get
            {
                var count = 0;
                _collection.Each(c => count += c.Count);
                return count;
            }
        }

        /// <summary>
        /// 入队
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public bool Enqueue(T t)
        {
            return BlockingCollection<T>.TryAddToAny(_collection, t) >= 0;
        }

        /// <summary>
        /// 出队
        /// </summary>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public T Dequeue(int timeOut = 1000)
        {
            if (BlockingCollection<T>.TryTakeFromAny(_collection, out T t, timeOut) >= 0)
            {
                return t;
            }
            return default;
        }

        /// <summary>
        /// Clear
        /// </summary>
        public void Clear()
        {
            _collection.Clear();
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            Clear();
        }
    }
}
