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
*命名空间：SAEA.QueueSocket.Model
*文件名： MessageQueue
*版本号： v26.4.23.1
*唯一标识：cea5c224-41ca-4f86-af1a-ef40f1ba97af
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：MessageQueue队列类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:31
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Common.Caching;
using SAEA.Sockets.Interface;

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace SAEA.QueueSocket.Model
{
    /// <summary>
    /// 按主题分组的消息队列
    /// </summary>
    public class MessageQueue : ISyncBase, IDisposable
    {
        readonly ConcurrentDictionary<string, FastQueue<PooledBuffer>> _dic;

        object _syncLocker = new object();

        /// <summary>
        /// 同步锁对象
        /// </summary>
        public object SyncLocker
        {
            get
            {
                return _syncLocker;
            }
        }

        private int _maxPendingMsgCount = 10000000;

        private volatile bool _disposed;

        /// <summary>
        /// 消息队列
        /// </summary>
        /// <param name="maxPendingMsgCount">消息队列最大堆积数量，默认10000000</param>
        public MessageQueue(int maxPendingMsgCount = 10000000)
        {
            _dic = new ConcurrentDictionary<string, FastQueue<PooledBuffer>>();
            _maxPendingMsgCount = maxPendingMsgCount;
        }


        /// <summary>
        /// 异步入队，队列达到上限时等待
        /// </summary>
        /// <param name="topic">主题</param>
        /// <param name="data">消息负载</param>
        /// <returns>入队成功返回 true</returns>
        public ValueTask<bool> Enqueue(string topic, PooledBuffer data)
        {
            var queue = _dic.GetOrAdd(topic, t => new FastQueue<PooledBuffer>(_maxPendingMsgCount));
            return queue.EnqueueAsync(data);
        }

        /// <summary>
        /// 尝试入队，已释放或队列已满时立即返回 false
        /// </summary>
        /// <param name="topic">主题</param>
        /// <param name="data">消息负载</param>
        /// <returns>入队成功返回 true</returns>
        public bool TryEnqueue(string topic, PooledBuffer data)
        {
            if (_disposed) return false;
            var queue = _dic.GetOrAdd(topic, t => new FastQueue<PooledBuffer>(_maxPendingMsgCount));
            if (_disposed) return false;
            return queue.TryEnqueue(data);
        }


        /// <summary>
        /// 异步出队，队列为空时等待，主题不存在时返回 null
        /// </summary>
        /// <param name="topic">主题</param>
        /// <returns>消息负载，无此主题时为 null</returns>
        public async ValueTask<PooledBuffer> DequeueAsync(string topic)
        {
            if (_dic.TryGetValue(topic, out FastQueue<PooledBuffer> queue))
            {
                if (queue != null)
                {
                    return await queue.DequeueAsync();
                }
            }
            return null;
        }


        /// <summary>
        /// 尝试出队，主题不存在或无数据时立即返回 false
        /// </summary>
        /// <param name="topic">主题</param>
        /// <param name="data">取出的消息负载，失败时为 null</param>
        /// <returns>取到数据返回 true</returns>
        public bool TryDequeue(string topic, out PooledBuffer data)
        {
            data = null;
            if (_dic.TryGetValue(topic, out FastQueue<PooledBuffer> queue))
            {
                if (queue != null)
                {
                    return queue.TryDequeue(out data);
                }
            }
            return false;
        }



        /// <summary>
        /// 获取主题与队列的内部字典，返回的是实时视图而非副本
        /// </summary>
        /// <returns>主题到队列的字典</returns>
        public ConcurrentDictionary<string, FastQueue<PooledBuffer>> ToList()
        {
            return _dic;
        }

        /// <summary>
        /// 获取指定主题当前堆积的消息数
        /// </summary>
        /// <param name="topic">主题</param>
        /// <returns>消息数，主题不存在时为 0</returns>
        public long GetCount(string topic)
        {
            if (_dic.TryGetValue(topic, out FastQueue<PooledBuffer> queue))
            {
                if (queue != null)
                {
                    return queue.Count;
                }
            }
            return 0;
        }

        /// <summary>
        /// 释放全部主题队列并清空字典
        /// </summary>
        public void Dispose()
        {
            _disposed = true;
            foreach (var queue in _dic.Values)
            {
                try { queue.Dispose(); } catch { }
                while (queue.TryDequeue(out var payload))
                {
                    try { payload?.Dispose(); } catch { }
                }
            }
            _dic.Clear();
        }
    }
}