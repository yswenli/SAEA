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
*文件名： AsyncQueue
*版本号： v26.4.23.1
*唯一标识：63235b0d-12fb-4655-adae-1f408775fa9a
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：AsyncQueue队列类
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
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.Internal
{
    /// <summary>
    /// 支持异步等待出队的线程安全队列
    /// </summary>
    /// <typeparam name="TItem">队列元素类型</typeparam>
    public sealed class AsyncQueue<TItem> : IDisposable
    {
        readonly object _syncRoot = new object();
        SemaphoreSlim _semaphore = new SemaphoreSlim(0);
        ConcurrentQueue<TItem> _queue = new ConcurrentQueue<TItem>();

        /// <summary>
        /// 队列中当前元素数量
        /// </summary>
        public int Count => _queue.Count;

        /// <summary>
        /// 入队元素并唤醒一个正在等待的出队操作
        /// </summary>
        /// <param name="item">待入队的元素</param>
        public void Enqueue(TItem item)
        {
            lock (_syncRoot)
            {
                _queue.Enqueue(item);
                _semaphore?.Release();
            }
        }

        /// <summary>
        /// 异步等待取出队首元素，队列已释放、等待被取消或内部信号量异常时返回失败结果
        /// </summary>
        /// <param name="cancellationToken">取消等待的令牌</param>
        /// <returns>出队结果，失败时 Item 为元素类型的默认值</returns>
        public async Task<AsyncQueueDequeueResult<TItem>> TryDequeueAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    Task task;
                    lock (_syncRoot)
                    {
                        if (_semaphore == null)
                        {
                            return new AsyncQueueDequeueResult<TItem>(false, default);
                        }

                        task = _semaphore.WaitAsync(cancellationToken);
                    }
                    
                    await task.ConfigureAwait(false);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return new AsyncQueueDequeueResult<TItem>(false, default);
                    }

                    if (_queue.TryDequeue(out var item))
                    {
                        return new AsyncQueueDequeueResult<TItem>(true, item);
                    }
                }
                catch (ArgumentNullException)
                {
                    // The semaphore throws this internally sometimes.
                    return new AsyncQueueDequeueResult<TItem>(false, default);
                }
                catch (OperationCanceledException)
                {
                    return new AsyncQueueDequeueResult<TItem>(false, default);
                }
            }

            return new AsyncQueueDequeueResult<TItem>(false, default);
        }

        /// <summary>
        /// 立即取出队首元素，不等待
        /// </summary>
        /// <returns>出队结果，队列为空时 IsSuccess 为 false</returns>
        public AsyncQueueDequeueResult<TItem> TryDequeue()
        {
            if (_queue.TryDequeue(out var item))
            {
                return new AsyncQueueDequeueResult<TItem>(true, item);
            }

            return new AsyncQueueDequeueResult<TItem>(false, default);
        }

        /// <summary>
        /// 清空队列，等待中的出队操作不受影响
        /// </summary>
        public void Clear()
        {            
            Interlocked.Exchange(ref _queue, new ConcurrentQueue<TItem>());
        }

        /// <summary>
        /// 释放内部信号量，之后入队不再唤醒、异步出队直接返回失败结果
        /// </summary>
        public void Dispose()
        {
            lock (_syncRoot)
            {
                _semaphore?.Dispose();
                _semaphore = null;
            }
        }
    }
}
