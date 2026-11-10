/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.RPC.Tests.Tests.Support
*文件名： PipelineTestHarness
*版本号： net10.0
*唯一标识：5e25c978-ee55-467f-bf09-d78f3d69d4b7
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 17:05:00
*描述：RPC 测试用的通道链宿主：内存字节流、连接视图、捕获处理器与启动/收口辅助
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 17:05:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，RPC 测试用的通道链宿主
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using SAEA.Sockets.Pipeline;

namespace SAEA.RPC.Tests.Tests.Support
{
    /// <summary>
    /// 内存版字节流：读取来自 <see cref="Pipe"/>，发送只记录字节并立即释放所有权，不连接真实 socket。
    /// </summary>
    public sealed class TestRawByteStream : IRawByteStream
    {
        private readonly Pipe _pipe;
        private readonly ConcurrentQueue<byte[]> _sent = new ConcurrentQueue<byte[]>();
        private int _pauseCount;
        private int _resumeCount;

        /// <summary>以连接标识创建内存字节流。</summary>
        /// <param name="id">连接标识。</param>
        public TestRawByteStream(string id)
        {
            ChannelId = id;

            // 4 MB 写阈值让测试一次写入整段帧时不会因为管道背压阻塞。
            _pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 4 * 1024 * 1024));
        }

        /// <inheritdoc />
        public string ChannelId { get; }

        /// <summary>底层管道，供测试直接写入或观察。</summary>
        public Pipe Pipe
        {
            get { return _pipe; }
        }

        /// <inheritdoc />
        public PipeReader Input
        {
            get { return _pipe.Reader; }
        }

        /// <summary>传输层关闭时完成，结果为关闭原因；用于断言收口来源。</summary>
        public TaskCompletionSource<string> Closed { get; } =
            new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>已交给传输层的帧字节快照。</summary>
        public IReadOnlyCollection<byte[]> Sent
        {
            get { return _sent; }
        }

        /// <summary>已交给传输层的帧数量。</summary>
        public int SentCount
        {
            get { return _sent.Count; }
        }

        /// <inheritdoc />
        public Task SendAsync(ReadOnlyMemory<byte> data, IDisposable owner)
        {
            _sent.Enqueue(data.ToArray());

            // 测试传输层不做真实发送，所有权在交接后立刻归还，避免背衬泄漏。
            ReleaseSafely(owner);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void Pause()
        {
            Interlocked.Increment(ref _pauseCount);
        }

        /// <inheritdoc />
        public void Resume()
        {
            Interlocked.Increment(ref _resumeCount);
        }

        /// <inheritdoc />
        public Task CloseAsync(string reason)
        {
            Closed.TrySetResult(reason);

            // 取消挂起的读取，让接收泵能感知传输层关闭而不是永久等待。
            try { _pipe.Reader.CancelPendingRead(); }
            catch (InvalidOperationException) { }

            return Task.CompletedTask;
        }

        /// <summary>向管道写入一段字节并立即刷新。</summary>
        /// <param name="data">待写入字节。</param>
        /// <returns>写入并刷新完成后结束的任务。</returns>
        public async Task WriteAsync(byte[] data)
        {
            await _pipe.Writer.WriteAsync(data).ConfigureAwait(false);
            await _pipe.Writer.FlushAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// 等待至少收到指定数量的出站帧。
        /// </summary>
        /// <param name="count">期望的帧数量。</param>
        /// <param name="timeout">最长等待时间。</param>
        /// <returns>在超时前达到数量返回 true；超时返回 false。</returns>
        public async Task<bool> WaitSentAsync(int count, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (_sent.Count < count)
            {
                if (DateTime.UtcNow >= deadline)
                    return false;

                await Task.Delay(10).ConfigureAwait(false);
            }

            return true;
        }

        private static void ReleaseSafely(IDisposable owner)
        {
            if (owner == null)
                return;

            try { owner.Dispose(); }
            catch (Exception) { }
        }
    }

    /// <summary>
    /// 内存版连接视图：只提供标识、活跃标记与线程安全属性表。
    /// </summary>
    public sealed class TestChannel : IChannel
    {
        private readonly ConcurrentDictionary<string, object> _attributes =
            new ConcurrentDictionary<string, object>();

        private int _active = 1;

        /// <summary>以连接标识创建连接视图。</summary>
        /// <param name="id">连接标识。</param>
        public TestChannel(string id)
        {
            Id = id;
        }

        /// <inheritdoc />
        public string Id { get; }

        /// <inheritdoc />
        public bool IsActive
        {
            get { return Volatile.Read(ref _active) == 1; }
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<string, object> Attributes
        {
            get { return _attributes; }
        }

        /// <inheritdoc />
        public void SetAttribute(string key, object value)
        {
            _attributes[key] = value;
        }

        /// <inheritdoc />
        public object GetAttribute(string key)
        {
            object value;
            return _attributes.TryGetValue(key, out value) ? value : null;
        }

        /// <inheritdoc />
        public bool TryRemoveAttribute(string key, out object value)
        {
            return _attributes.TryRemove(key, out value);
        }

        /// <inheritdoc />
        public Task CloseAsync(string reason)
        {
            Volatile.Write(ref _active, 0);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 链尾捕获处理器：按到达顺序保存入站消息，供断言消息字段。
    /// </summary>
    public sealed class CapturingInboundHandler : InboundHandlerAdapter
    {
        private readonly ConcurrentQueue<object> _messages = new ConcurrentQueue<object>();
        private readonly TaskCompletionSource<object> _first =
            new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>已捕获的消息数量。</summary>
        public int Count
        {
            get { return _messages.Count; }
        }

        /// <summary>已捕获消息的内存快照。</summary>
        public IReadOnlyCollection<object> Messages
        {
            get { return _messages; }
        }

        /// <inheritdoc />
        public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
        {
            _messages.Enqueue(msg);
            _first.TrySetResult(msg);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 等待至少达到指定消息数。
        /// </summary>
        /// <param name="count">期望的消息数。</param>
        /// <param name="timeout">最长等待时间。</param>
        /// <returns>在超时前达到数量返回 true；超时返回 false。</returns>
        public async Task<bool> WaitCountAsync(int count, TimeSpan timeout)
        {
            if (count <= 0)
                return true;

            var deadline = DateTime.UtcNow + timeout;
            while (_messages.Count < count)
            {
                if (DateTime.UtcNow >= deadline)
                    return false;

                await Task.Delay(10).ConfigureAwait(false);
            }

            return true;
        }

        /// <summary>
        /// 等待并返回第一条捕获的消息。
        /// </summary>
        /// <param name="timeout">最长等待时间。</param>
        /// <returns>第一条消息。</returns>
        public async Task<object> FirstAsync(TimeSpan timeout)
        {
            return await _first.Task.WaitAsync(timeout).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 异常捕获处理器：记录链上冒泡的异常，供断言协议错误。
    /// </summary>
    public sealed class CapturingExceptionHandler : InboundHandlerAdapter
    {
        private readonly ConcurrentQueue<Exception> _exceptions = new ConcurrentQueue<Exception>();
        private readonly TaskCompletionSource<Exception> _first =
            new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>已捕获的异常数量。</summary>
        public int Count
        {
            get { return _exceptions.Count; }
        }

        /// <inheritdoc />
        public override Task ChannelExceptionCaughtAsync(IChannelHandlerContext ctx, Exception ex)
        {
            _exceptions.Enqueue(ex);
            _first.TrySetResult(ex);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 等待并返回第一条捕获的异常。
        /// </summary>
        /// <param name="timeout">最长等待时间。</param>
        /// <returns>第一条异常。</returns>
        public async Task<Exception> FirstAsync(TimeSpan timeout)
        {
            return await _first.Task.WaitAsync(timeout).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 通道链测试辅助：创建配置、写入字节与带超时的收口。
    /// </summary>
    public static class PipelineTestKit
    {
        /// <summary>默认等待上限。</summary>
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 创建测试用链配置：队列容量固定、不走内联快路径，并让未成帧缓冲上限按分帧器推导。
        /// </summary>
        /// <param name="splitter">分帧器；必填。</param>
        /// <param name="maxConcurrentMessages">同时在途消息上限，默认 1。</param>
        /// <returns>可直接传给 <see cref="ChannelPipeline.Create"/> 的配置。</returns>
        public static ChannelPipelineOptions CreateOptions(IFrameSplitter splitter, int maxConcurrentMessages = 1)
        {
            return new ChannelPipelineOptions
            {
                Splitter = splitter,
                QueueCapacity = 16,
                MaxConcurrentMessages = maxConcurrentMessages,
                DispatchMode = ChannelDispatchMode.Queued,
                Guard = NullChannelGuard.Instance,

                // 由链按分帧器推导缓冲上限，测试不重复维护这组推导关系。
                MaxBufferedBytes = 0,
                SplitterSwitchTimeout = TimeSpan.FromSeconds(3)
            };
        }

        /// <summary>
        /// 向内存字节流写入一段字节，自带超时。
        /// </summary>
        /// <param name="stream">目标字节流。</param>
        /// <param name="data">待写入字节。</param>
        /// <param name="timeout">写入超时；不传时使用 <see cref="DefaultTimeout"/>。</param>
        /// <returns>写入完成后结束的任务。</returns>
        public static async Task WriteAsync(TestRawByteStream stream, byte[] data, TimeSpan? timeout = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            await stream.WriteAsync(data).WaitAsync(timeout ?? DefaultTimeout).ConfigureAwait(false);
        }

        /// <summary>
        /// 请求收口并等待资源回收，自带超时；超时抛 <see cref="TimeoutException"/>，避免用例永久挂起。
        /// </summary>
        /// <param name="executor">待收口的执行器。</param>
        /// <param name="timeout">收口超时；不传时使用 <see cref="DefaultTimeout"/>。</param>
        /// <returns>收口完成后结束的任务。</returns>
        public static async Task StopAsync(ChannelPipelineExecutor executor, TimeSpan? timeout = null)
        {
            if (executor == null)
                return;

            var limit = timeout ?? DefaultTimeout;

            await executor.BeginShutdown("test-done").WaitAsync(limit).ConfigureAwait(false);
            await executor.Completion.WaitAsync(limit).ConfigureAwait(false);
        }

        /// <summary>
        /// 拼两段字节，测试里组装跨段或粘包输入。
        /// </summary>
        /// <param name="first">前一段。</param>
        /// <param name="second">后一段。</param>
        /// <returns>顺序拼接后的新数组。</returns>
        public static byte[] Concat(byte[] first, byte[] second)
        {
            var result = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, result, 0, first.Length);
            Buffer.BlockCopy(second, 0, result, first.Length, second.Length);
            return result;
        }
    }
}