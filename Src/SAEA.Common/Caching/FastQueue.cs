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
*文件名： FastQueue
*版本号： v26.4.23.1
*唯一标识：36e9756c-a773-4687-afc5-5431e8094bd2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2023/03/19 20:57:39
*描述：
*
*=====================================================================
*修改标记
*修改时间：2023/03/19 20:57:39
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SAEA.Common.Caching
{
    /// <summary>
    /// 快速队列
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class FastQueue<T> : IDisposable
    {
        Channel<T> _channel;

        long _count = 0;

        /// <summary>
        /// 长度
        /// </summary>
        public long Count { get { return _count; } }

        /// <summary>
        /// 快速队列
        /// </summary>
        /// <param name="size"></param>
        public FastQueue(int size = 0)
        {
            if (size > 0)
            {
                _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(size)
                {
                    FullMode = BoundedChannelFullMode.Wait
                });
            }
            else
            {
                _channel = Channel.CreateUnbounded<T>();
            }
        }

        /// <summary>
        /// 入队
        /// </summary>
        /// <param name="t"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async ValueTask<bool> EnqueueAsync(T t, CancellationToken cancellationToken = default)
        {
            await _channel.Writer.WaitToWriteAsync(cancellationToken);
            if (_channel.Writer.TryWrite(t))
            {
                Interlocked.Increment(ref _count);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 入队
        /// </summary>
        /// <param name="t"></param>
        /// <param name="seconds"></param>
        /// <returns></returns>
        public async ValueTask<bool> EnqueueAsync(T t, int seconds)
        {
            using (var cts = new CancellationTokenSource(seconds * 1000))
            {
                return await EnqueueAsync(t, cts.Token);
            }
        }

        /// <summary>
        /// 入队
        /// </summary>
        /// <param name="t"></param>
        /// <param name="timeSpan"></param>
        /// <returns></returns>
        public async ValueTask<bool> EnqueueAsync(T t, TimeSpan timeSpan)
        {
            return await EnqueueAsync(t, (int)timeSpan.TotalMilliseconds);
        }


        /// <summary>
        /// 出队
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async ValueTask<T> DequeueAsync(CancellationToken cancellationToken = default)
        {
            await _channel.Reader.WaitToReadAsync(cancellationToken);

            if (_channel.Reader.TryRead(out var t))
            {
                Interlocked.Decrement(ref _count);
                return t;
            }

            return default;
        }

        /// <summary>
        /// 出队
        /// </summary>
        /// <param name="seconds"></param>
        /// <returns></returns>
        public async ValueTask<T> DequeueAsync(int seconds)
        {
            using (var cts = new CancellationTokenSource(seconds * 1000))
            {
                return await DequeueAsync(cts.Token);
            }
        }

        /// <summary>
        /// 出队
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <returns></returns>
        public async ValueTask<T> DequeueAsync(TimeSpan timeSpan)
        {
            return await DequeueAsync((int)timeSpan.TotalMilliseconds);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _channel.Writer.Complete();
            _channel.Writer.Complete();
        }

    }
}
