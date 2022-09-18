/****************************************************************************
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
