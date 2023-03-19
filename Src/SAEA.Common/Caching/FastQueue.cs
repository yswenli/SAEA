/****************************************************************************
*项目名称：SAEA.Common.Caching
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Common.Caching
*类 名 称：FastQueue
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2023/03/19 11:00:17
*描述：快速队列
*=====================================================================
*修改时间：2023/03/19 11:00:17
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：快速队列
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
            while (await _channel.Writer.WaitToWriteAsync(cancellationToken))
            {
                if (_channel.Writer.TryWrite(t))
                {
                    Interlocked.Increment(ref _count);
                    return true;
                }
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
            while (await _channel.Reader.WaitToReadAsync(cancellationToken))
            {
                if (_channel.Reader.TryRead(out var t))
                {
                    Interlocked.Decrement(ref _count);
                    return t;
                }
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
