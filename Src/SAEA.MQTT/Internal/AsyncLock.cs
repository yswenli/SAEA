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
*文件名： AsyncLock
*版本号： v26.4.23.1
*唯一标识：885b7df6-9edc-4a3a-99e0-f69bc5bb1160
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：AsyncLock锁类
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
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.Internal
{
    /// <summary>
    /// 基于信号量的异步互斥锁，获取锁后返回可释放的占用令牌
    /// </summary>
    public sealed class AsyncLock : IDisposable
    {
        readonly object _syncRoot = new object();
        readonly Task<IDisposable> _releaser;

        SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// 创建初始未占用的异步互斥锁
        /// </summary>
        public AsyncLock()
        {
            _releaser = Task.FromResult((IDisposable)new Releaser(this));
        }

        /// <summary>
        /// 异步等待获取锁，成功时返回占用令牌，释放该令牌即释放锁
        /// </summary>
        /// <param name="cancellationToken">取消等待的令牌</param>
        /// <returns>占用期间需释放的令牌</returns>
        public Task<IDisposable> WaitAsync(CancellationToken cancellationToken)
        {
            Task task;

            // This lock is required to avoid ObjectDisposedExceptions.
            // These are fired when this lock gets disposed (and thus the semaphore)
            // and a worker thread tries to call this method at the same time.
            // Another way would be catching all ObjectDisposedExceptions but this situation happens
            // quite often when clients are disconnecting.
            lock (_syncRoot)
            {
                task = _semaphore?.WaitAsync(cancellationToken);
            }

            if (task == null)
            {
                throw new ObjectDisposedException("The AsyncLock is disposed.");
            }

            if (task.Status == TaskStatus.RanToCompletion)
            {
                return _releaser;
            }

            // Wait for the _WaitAsync_ method and return the releaser afterwards.
            return task.ContinueWith(
                (_, state) => (IDisposable)state, 
                _releaser.Result, 
                cancellationToken, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        /// <summary>
        /// 释放内部信号量，之后调用 WaitAsync 会抛出 ObjectDisposedException
        /// </summary>
        public void Dispose()
        {
            lock (_syncRoot)
            {
                _semaphore?.Dispose();
                _semaphore = null;
            }
        }

        internal void Release()
        {
            lock (_syncRoot)
            {
                _semaphore?.Release();
            }
        }

        sealed class Releaser : IDisposable
        {
            readonly AsyncLock _lock;

            internal Releaser(AsyncLock @lock)
            {
                _lock = @lock;
            }

            public void Dispose()
            {
                _lock.Release();
            }
        }
    }
}
