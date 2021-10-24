/****************************************************************************
*项目名称：SAEA.Common.Caching
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Common.Caching
*类 名 称：Batcher
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/17 11:00:17
*描述：
*=====================================================================
*修改时间：2020/12/17 11:00:17
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using SAEA.Common.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace SAEA.Common.Caching
{
    /// <summary>
    /// 批量打包委托
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="sender"></param>
    /// <param name="data"></param>
    public delegate void OnBatchedHandler<T>(IBatcher sender, List<T> data);


    /// <summary>
    /// 批量打包类
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class Batcher<T> : IBatcher, IDisposable
    {
        int _size, _timeout, _max;

        ConcurrentBag<T> _bag;

        /// <summary>
        /// 触发缓存打包事件
        /// </summary>
        public event OnBatchedHandler<T> OnBatched;

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// 批量打包类
        /// </summary>
        /// <param name="size"></param>
        /// <param name="timeout"></param>
        /// <param name="max"></param>
        /// <param name="name"></param>
        public Batcher(int size = 1000, int timeout = 1000, int max = -1, string name = "")
        {

            _size = size;
            _timeout = timeout;
            if (max == -1) _max = _size * 10;
            else _max = max;
            if (_max < _size) throw new ArgumentOutOfRangeException("max不能小于size");
            Name = name;
            _bag = new ConcurrentBag<T>();
            TaskHelper.LongRunning(Handler);
        }

        /// <summary>
        /// 插入
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="t"></param>
        public void Insert(T t)
        {
            while (_bag.Count >= _max)
            {
                ThreadHelper.Sleep(100);
            }
            _bag.Add(t);
        }

        /// <summary>
        /// 处理过期或已达上限的数据
        /// </summary>
        private void Handler()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (true)
            {
                var count = _bag.Count;
                if (count >= _size || (count > 0 && stopwatch.ElapsedMilliseconds >= _timeout))
                {
                    var list = new List<T>();
                    for (int i = 0; i < count; i++)
                    {
                        if (_bag.TryTake(out T t))
                        {
                            list.Add(t);
                        }
                    }
                    OnBatched?.Invoke(this, list);
                    stopwatch.Restart();
                }
                else
                {
                    ThreadHelper.Sleep(_timeout);
                }
            }
        }

        /// <summary>
        /// 清除数据
        /// </summary>
        public void Clear()
        {
            while (!_bag.IsEmpty)
            {
                _bag.TryTake(out T _);
            }
        }

        /// <summary>
        /// 释放批量打包缓存
        /// </summary>
        public void Dispose()
        {
            var count = _bag.Count;
            if (count > 0)
            {
                var list = new List<T>();
                for (int i = 0; i < count; i++)
                {
                    if (_bag.TryTake(out T t))
                    {
                        list.Add(t);
                    }
                }
                OnBatched?.Invoke(this, list);
            }
        }
    }


    /// <summary>
    /// 批量打包委托
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="data"></param>
    public delegate void OnBatchedHandler(IBatcher sender, byte[] data);

    /// <summary>
    /// 批量打包
    /// </summary>
    public sealed class Batcher : IBatcher, IDisposable
    {
        Batcher<byte[]> _batcher;

        /// <summary>
        /// 触发缓存打包事件
        /// </summary>
        public event OnBatchedHandler OnBatched;

        /// <summary>
        /// 名称
        /// </summary>
        public string Name
        {
            get
            {
                return _batcher.Name;
            }
        }

        /// <summary>
        /// 批量打包类
        /// </summary>
        /// <param name="size"></param>
        /// <param name="timeout"></param>
        /// <param name="max"></param>
        /// <param name="name"></param>
        public Batcher(int size = 1000, int timeout = 1000, int max = -1, string name = "")
        {
            _batcher = new Batcher<byte[]>(size, timeout, max, name);

            _batcher.OnBatched += _batcher_OnBatched;
        }

        private void _batcher_OnBatched(IBatcher sender, List<byte[]> data)
        {
            var result = new List<Byte>();
            foreach (var item in data)
            {
                result.AddRange(item);
            }
            OnBatched?.Invoke(this, result.ToArray());
            result.Clear();
        }

        /// <summary>
        /// 插入
        /// </summary>
        /// <param name="data"></param>
        public void Insert(byte[] data)
        {
            _batcher.Insert(data);
        }


        /// <summary>
        /// 清除数据
        /// </summary>
        public void Clear()
        {
            _batcher?.Clear();
        }

        public void Dispose()
        {
            _batcher?.Dispose();
        }
    }
}
