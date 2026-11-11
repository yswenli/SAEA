/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.FTP
*文件名： ChannelStreamAdapter
*版本号： v26.11.5.1
*唯一标识：89db0201-8e6b-4eb2-a5b9-7081363b28c4
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 16:37:14
*描述：把数据通道链上的分块消息包成同步 Stream 的有界队列适配器
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 16:37:14
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增数据通道 Stream 适配层
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SAEA.Sockets.Pipeline;

namespace SAEA.FTP
{
    /// <summary>
    /// 把数据通道链上的分块消息包成同步 <see cref="Stream"/>：写走链的业务写入口，读走有界队列。
    /// <para>
    /// 线程与收尾契约：
    /// </para>
    /// <list type="bullet">
    /// <item><description>入站解码器在本适配器队列上阻塞等待空间（背压），消费者是独立的 <see cref="Read"/> 调用线程；
    /// 因此双方都不能在持锁期间回头等待对方线程。</description></item>
    /// <item><description><see cref="Read"/> 在对端 EOF（<see cref="Complete"/>）、链上异常（<see cref="Fail"/>）
    /// 与本对象释放（<see cref="Dispose(bool)"/>）时都会被唤醒：EOF 返回 0、异常抛 <see cref="IOException"/>、释放返回 0。</description></item>
    /// <item><description><see cref="Write"/> 只表示「已交给链」，不表示对端已收到；一次写入的全部字节都已交接，没有额外缓冲。</description></item>
    /// </list>
    /// <para>
    /// 池化缓冲所有权：入队的数据块归队列所有，被 <see cref="Read"/> 取完、被丢弃或本对象释放时归还；
    /// 出队时超时的那一块可能仍被链持有，本类绝不代为释放。
    /// </para>
    /// </summary>
    public sealed class ChannelStreamAdapter : Stream
    {
        private const int DefaultCapacity = 8;

        private const int DefaultMaxBytes = 512 * 1024;

        /// <summary>无限等待的截止时间哨兵值。</summary>
        private const long InfiniteDeadline = long.MaxValue;

        private readonly object _gate = new object();

        private readonly Queue<FtpDataChunk> _queue;

        private readonly int _capacity;

        private readonly int _maxBytes;

        private FtpDataChunk _current;

        private int _currentOffset;

        private long _queuedBytes;

        private bool _completed;

        private Exception _fault;

        private bool _disposed;

        private IChannelPipeline _pipeline;

        private int _readTimeout;

        private int _writeTimeout;

        /// <summary>
        /// 以队列块数上限与总字节上限创建适配器。
        /// </summary>
        /// <param name="capacity">队列可缓存的块数上限，必须大于 0</param>
        /// <param name="maxBytes">队列内待消费块的总字节上限，必须大于 0</param>
        public ChannelStreamAdapter(int capacity = DefaultCapacity, int maxBytes = DefaultMaxBytes)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "队列块数上限必须大于 0。");
            if (maxBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxBytes), maxBytes, "队列字节上限必须大于 0。");

            _capacity = capacity;
            _maxBytes = maxBytes;
            _queue = new Queue<FtpDataChunk>(capacity);
        }

        /// <summary>本适配器永远不支持定位。</summary>
        public override bool CanSeek
        {
            get { return false; }
        }

        /// <summary>读方向可用：有界队列取数据。</summary>
        public override bool CanRead
        {
            get { return true; }
        }

        /// <summary>写方向可用：经链的业务写入口发出。</summary>
        public override bool CanWrite
        {
            get { return true; }
        }

        /// <summary>读超时由 <see cref="ReadTimeout"/> 控制，因此支持超时语义。</summary>
        public override bool CanTimeout
        {
            get { return true; }
        }

        /// <summary>
        /// 单次 <see cref="Read"/> 等待数据的上限（毫秒）；小于等于 0 表示无限等待。
        /// 超时抛 <see cref="IOException"/>，与 <see cref="System.Net.Sockets.NetworkStream"/> 一致。
        /// </summary>
        public override int ReadTimeout
        {
            get { return _readTimeout; }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "读超时不能为负。");

                _readTimeout = value;
            }
        }

        /// <summary>
        /// 单次 <see cref="Write"/> 等待链交接的上限（毫秒）；小于等于 0 表示无限等待。
        /// 超时抛 <see cref="IOException"/>。
        /// </summary>
        public override int WriteTimeout
        {
            get { return _writeTimeout; }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "写超时不能为负。");

                _writeTimeout = value;
            }
        }

        /// <summary>
        /// 不支持定位；数据通道是一次性的顺序字节流。
        /// </summary>
        public override long Length
        {
            get { throw new NotSupportedException("数据通道适配器不支持定位，没有长度概念。"); }
        }

        /// <summary>
        /// 不支持定位；读写位置只按已消费字节推进。
        /// </summary>
        public override long Position
        {
            get { throw new NotSupportedException("数据通道适配器不支持定位。"); }
            set { throw new NotSupportedException("数据通道适配器不支持定位。"); }
        }

        /// <summary>
        /// 绑定出站写入口；只能在启动收口前绑定一次。
        /// </summary>
        /// <param name="pipeline">本数据连接独占的链，为 null 抛 <see cref="ArgumentNullException"/></param>
        public void AttachPipeline(IChannelPipeline pipeline)
        {
            if (pipeline == null)
                throw new ArgumentNullException(nameof(pipeline));

            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(ChannelStreamAdapter));
                if (_pipeline != null)
                    throw new InvalidOperationException("数据通道适配器已经绑定过链写入口，重复绑定会让写出走错连接。");

                _pipeline = pipeline;
            }
        }

        /// <summary>
        /// 入队一个数据块；队列按块数与总字节双上限，满了阻塞等待消费者出队。
        /// <para>
        /// 通道已收口（释放、EOF 或失败）时放弃入队并直接归还本块。
        /// 单块超过字节上限时只在队列为空时放行，否则永远等不到空间、泵线程会被自己阻塞。
        /// </para>
        /// </summary>
        /// <param name="chunk">待入队的数据块，所有权随入队移交；为 null 抛 <see cref="ArgumentNullException"/></param>
        internal void Enqueue(FtpDataChunk chunk)
        {
            if (chunk == null)
                throw new ArgumentNullException(nameof(chunk));

            lock (_gate)
            {
                while (true)
                {
                    if (_disposed || _completed || _fault != null)
                    {
                        // 通道已收口，没有人会再消费；在这里归还，避免池化缓冲挂在一个死队列上。
                        chunk.Dispose();
                        return;
                    }

                    var fits = _queue.Count < _capacity
                        && (_queue.Count == 0 || _queuedBytes + chunk.Length <= _maxBytes);
                    if (fits)
                    {
                        _queue.Enqueue(chunk);
                        _queuedBytes += chunk.Length;

                        // 唤醒可能正在等数据或等出队空间的对手方。
                        Monitor.PulseAll(_gate);
                        return;
                    }

                    Monitor.Wait(_gate);
                }
            }
        }

        /// <summary>
        /// 标记对端 EOF：唤醒所有等待者，队列里已有的块仍可读完，读空后 <see cref="Read"/> 返回 0。
        /// </summary>
        internal void Complete()
        {
            lock (_gate)
            {
                if (_disposed) return;

                _completed = true;
                Monitor.PulseAll(_gate);
            }
        }

        /// <summary>
        /// 标记链上异常：唤醒所有等待者，未消费的块立即归还，此后 <see cref="Read"/> 抛
        /// <see cref="IOException"/> 并带上 <paramref name="ex"/> 作内层异常。
        /// </summary>
        /// <param name="ex">链上原始异常，为 null 抛 <see cref="ArgumentNullException"/></param>
        internal void Fail(Exception ex)
        {
            if (ex == null)
                throw new ArgumentNullException(nameof(ex));

            var abandoned = new List<FtpDataChunk>();

            lock (_gate)
            {
                if (_disposed) return;

                if (_fault == null)
                    _fault = ex;

                DrainLocked(abandoned);
                Monitor.PulseAll(_gate);
            }

            ReleaseAll(abandoned);
        }

        /// <summary>
        /// 读一块数据：先消费当前块的剩余字节，再按队列顺序取下一块；
        /// 队列空时按 <see cref="ReadTimeout"/> 等待，超时抛 <see cref="IOException"/>。
        /// </summary>
        /// <param name="buffer">目标缓冲区</param>
        /// <param name="offset">目标缓冲区起始下标</param>
        /// <param name="count">本次最多读取的字节数</param>
        /// <returns>实际读到的字节数；EOF 且队列已空、或本对象已释放时返回 0</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), offset, "offset 不能为负。");
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), count, "count 不能为负。");
            if (buffer.Length - offset < count)
                throw new ArgumentException("offset 与 count 超出目标缓冲区范围。", nameof(count));
            if (count == 0)
                return 0;

            var deadline = ComputeReadDeadline();

            lock (_gate)
            {
                while (true)
                {
                    // 已释放时返回 0 而不是抛异常：让文件读写循环按 EOF 自然退出，收口路径不需要额外判空。
                    if (_disposed)
                        return 0;

                    if (_fault != null)
                        throw new IOException("数据通道已失败:" + _fault.Message, _fault);

                    if (_current == null && _queue.Count > 0)
                    {
                        _current = _queue.Dequeue();
                        _currentOffset = 0;
                        _queuedBytes -= _current.Length;

                        // 出队腾出了空间，唤醒可能因队列满而阻塞的入站解码器。
                        Monitor.PulseAll(_gate);
                    }

                    if (_current != null)
                    {
                        var available = _current.Length - _currentOffset;
                        var take = available < count ? available : count;

                        _current.Memory.Span.Slice(_currentOffset, take).CopyTo(new Span<byte>(buffer, offset, take));
                        _currentOffset += take;

                        if (_currentOffset >= _current.Length)
                        {
                            _current.Dispose();
                            _current = null;
                            _currentOffset = 0;
                        }

                        return take;
                    }

                    if (_completed)
                        return 0;

                    WaitForDataLocked(deadline);
                }
            }
        }

        /// <summary>
        /// 写一段数据：复制成数据块后交给链的业务写入口，并等待交接完成。
        /// <para>
        /// 超时后不在这里释放该块：它可能已被出站队列或传输层持有，重复释放会造成池内缓冲被并发复用。
        /// </para>
        /// </summary>
        /// <param name="buffer">源缓冲区</param>
        /// <param name="offset">源缓冲区起始下标</param>
        /// <param name="count">本次写出的字节数</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), offset, "offset 不能为负。");
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), count, "count 不能为负。");
            if (buffer.Length - offset < count)
                throw new ArgumentException("offset 与 count 超出源缓冲区范围。", nameof(count));
            if (count == 0)
                return;

            IChannelPipeline pipeline;
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(ChannelStreamAdapter));

                pipeline = _pipeline;
            }

            if (pipeline == null)
                throw new InvalidOperationException("数据通道适配器尚未绑定链写入口，不能写出。");

            var chunk = FtpDataChunk.CopyFrom(buffer, offset, count);

            Task sending;
            try
            {
                sending = pipeline.WriteAsync(chunk);
            }
            catch
            {
                // 同步抛出表示还没进入链：所有权仍在本方法，必须归还。
                chunk.Dispose();
                throw;
            }

            var timeout = _writeTimeout;
            if (timeout > 0 && !WaitCompleted(sending, timeout))
            {
                // 超时不在这里释放：该块可能仍被出站队列或传输层持有。
                throw new IOException("数据通道写入超时（WriteTimeout=" + timeout + " 毫秒）。");
            }

            sending.GetAwaiter().GetResult();
        }

        /// <summary>
        /// 空实现：每次 <see cref="Write"/> 已完成交接，没有额外缓冲需要刷出。
        /// </summary>
        public override void Flush()
        {
        }

        /// <summary>不支持定位，恒抛 <see cref="NotSupportedException"/>。</summary>
        /// <param name="offset">忽略</param>
        /// <param name="origin">忽略</param>
        /// <returns>不会返回</returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("数据通道适配器不支持定位。");
        }

        /// <summary>不支持改长度，恒抛 <see cref="NotSupportedException"/>。</summary>
        /// <param name="value">忽略</param>
        public override void SetLength(long value)
        {
            throw new NotSupportedException("数据通道适配器不支持改长度。");
        }

        /// <summary>
        /// 释放适配器：标记关闭、唤醒所有等待者、归还队列里未消费的块，并尝试关闭底层链。可重复调用。
        /// </summary>
        /// <param name="disposing">true 表示由 <see cref="Dispose()"/> 调用</param>
        protected override void Dispose(bool disposing)
        {
            if (!disposing)
            {
                base.Dispose(disposing);
                return;
            }

            IChannelPipeline pipeline;
            var abandoned = new List<FtpDataChunk>();

            lock (_gate)
            {
                if (_disposed)
                {
                    base.Dispose(disposing);
                    return;
                }

                _disposed = true;
                pipeline = _pipeline;
                DrainLocked(abandoned);

                // 唤醒阻塞在 Read 或入队上的线程，让它们按「已释放」退出。
                Monitor.PulseAll(_gate);
            }

            ReleaseAll(abandoned);

            if (pipeline != null)
            {
                try
                {
                    // 只请求关闭，不等待收口：释放路径不能阻塞在链上。
                    var closing = pipeline.CloseAsync("data-closed");
                    if (closing != null)
                    {
                        closing.ContinueWith(
                            task => { var ignored = task.Exception; },
                            CancellationToken.None,
                            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                            TaskScheduler.Default);
                    }
                }
                catch (Exception)
                {
                    // 关闭失败不影响本地资源已归还的事实，这里只吞掉异常。
                }
            }

            base.Dispose(disposing);
        }

        /// <summary>计算本次读的截止时间；无限等待返回 <see cref="InfiniteDeadline"/>。</summary>
        private long ComputeReadDeadline()
        {
            var timeout = _readTimeout;
            return timeout <= 0 ? InfiniteDeadline : Environment.TickCount + (long)timeout;
        }

        /// <summary>在持锁状态下等待新数据或被唤醒；超时抛 <see cref="IOException"/>。</summary>
        /// <param name="deadline">本次读的截止时间</param>
        private void WaitForDataLocked(long deadline)
        {
            if (deadline == InfiniteDeadline)
            {
                Monitor.Wait(_gate);
                return;
            }

            var remaining = deadline - Environment.TickCount;
            if (remaining <= 0)
                throw new IOException("数据通道读取超时（ReadTimeout=" + _readTimeout + " 毫秒）。");

            Monitor.Wait(_gate, remaining > int.MaxValue ? int.MaxValue : (int)remaining);
        }

        /// <summary>把当前块与队列里的块全部摘下，放入 <paramref name="sink"/> 供锁外归还。</summary>
        /// <param name="sink">接收待归还块的列表</param>
        private void DrainLocked(List<FtpDataChunk> sink)
        {
            if (_current != null)
            {
                sink.Add(_current);
                _current = null;
                _currentOffset = 0;
            }

            while (_queue.Count > 0)
                sink.Add(_queue.Dequeue());

            _queuedBytes = 0;
        }

        /// <summary>在锁外逐块归还；单块释放异常不影响其余块。</summary>
        /// <param name="chunks">待归还的块</param>
        private static void ReleaseAll(List<FtpDataChunk> chunks)
        {
            for (var i = 0; i < chunks.Count; i++)
            {
                try
                {
                    chunks[i].Dispose();
                }
                catch (Exception)
                {
                    // 归还失败时块已不可用，继续释放其余块，避免一个异常拖住整批资源。
                }
            }
        }

        /// <summary>
        /// 等待任务完成；任务以失败完成时返回 true，真实异常由调用方用 GetAwaiter().GetResult() 抛出，
        /// 避免对外暴露 <see cref="AggregateException"/>。
        /// </summary>
        /// <param name="task">待等待的任务</param>
        /// <param name="timeout">等待上限（毫秒）</param>
        /// <returns>是否已在超时前完成</returns>
        private static bool WaitCompleted(Task task, int timeout)
        {
            try
            {
                return task.Wait(timeout);
            }
            catch (AggregateException)
            {
                return true;
            }
        }
    }
}