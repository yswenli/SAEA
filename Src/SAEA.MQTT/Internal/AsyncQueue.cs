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
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：AsyncQueue队列类
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.Internal
{
    public sealed class AsyncQueue<TItem> : IDisposable
    {
        readonly object _syncRoot = new object();
        SemaphoreSlim _semaphore = new SemaphoreSlim(0);
        ConcurrentQueue<TItem> _queue = new ConcurrentQueue<TItem>();

        public int Count => _queue.Count;

        public void Enqueue(TItem item)
        {
            lock (_syncRoot)
            {
                _queue.Enqueue(item);
                _semaphore?.Release();
            }
        }

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

        public AsyncQueueDequeueResult<TItem> TryDequeue()
        {
            if (_queue.TryDequeue(out var item))
            {
                return new AsyncQueueDequeueResult<TItem>(true, item);
            }

            return new AsyncQueueDequeueResult<TItem>(false, default);
        }

        public void Clear()
        {            
            Interlocked.Exchange(ref _queue, new ConcurrentQueue<TItem>());
        }

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
