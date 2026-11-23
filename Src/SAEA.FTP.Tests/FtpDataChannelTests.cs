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
*命名空间：SAEA.FTP.Tests
*文件名： FtpDataChannelTests
*版本号： net10.0
*唯一标识：4fa642da-2fe4-48c4-97da-d29fba680b43
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 17:05:00
*描述：数据通道适配层的读写、收口、池化归还与出站入站直通用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 18:26:56
*修改人： yswenli
*版本号： net10.0
*描述：追加一次到达超过单帧上限时切块读出的用例
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.FTP;
using SAEA.Sockets.Pipeline;

namespace SAEA.FTP.Tests
{
    /// <summary>
    /// 数据通道用例：流适配器的读、写、收口与池化缓冲归还，以及经真实链路的双向直通。
    /// <para>
    /// <c>Enqueue</c>/<c>Complete</c>/<c>Fail</c> 只对程序集内可见，这里用反射调用，
    /// 避免为测试放宽生产类型的可见性。
    /// </para>
    /// </summary>
    [TestClass]
    public class FtpDataChannelTests
    {
        private const int WaitTimeoutMs = 5000;

        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        private static readonly MethodInfo EnqueueMethod = FindAdapterMethod("Enqueue");

        private static readonly MethodInfo CompleteMethod = FindAdapterMethod("Complete");

        private static readonly MethodInfo FailMethod = FindAdapterMethod("Fail");

        /// <summary>
        /// 队列空且读超时到点时抛 <see cref="IOException"/>，不会无限阻塞。
        /// </summary>
        [TestMethod]
        public void Read_队列无数据_超时抛IOException()
        {
            using (var adapter = new ChannelStreamAdapter(2, 1024))
            {
                adapter.ReadTimeout = 100;

                Assert.ThrowsExactly<IOException>(() => adapter.Read(new byte[1], 0, 1));
            }
        }

        /// <summary>
        /// 一个数据块被多次读取时按顺序返回剩余字节，读空后归还池化缓冲。
        /// </summary>
        [TestMethod]
        public void Read_单块跨多次读取_按序返回剩余字节()
        {
            using (var adapter = new ChannelStreamAdapter(2, 1024))
            {
                adapter.ReadTimeout = WaitTimeoutMs;
                var chunk = FtpDataChunk.CopyFrom(new byte[] { 1, 2, 3, 4, 5 });
                Enqueue(adapter, chunk);

                var buffer = new byte[3];
                Assert.AreEqual(3, adapter.Read(buffer, 0, 3));
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, buffer);

                Assert.AreEqual(2, adapter.Read(buffer, 0, 3));
                Assert.AreEqual(4, buffer[0]);
                Assert.AreEqual(5, buffer[1]);

                Assert.IsTrue(chunk.IsDisposed, "数据块读空后应归还池化缓冲");
            }
        }

        /// <summary>
        /// 对端 EOF 后队列里的块仍可读完，读空再读返回 0。
        /// </summary>
        [TestMethod]
        public void Read_对端EOF_读空后返回0()
        {
            using (var adapter = new ChannelStreamAdapter(2, 1024))
            {
                adapter.ReadTimeout = WaitTimeoutMs;
                Enqueue(adapter, FtpDataChunk.CopyFrom(new byte[] { 9, 8 }));
                Complete(adapter);

                var buffer = new byte[2];
                Assert.AreEqual(2, adapter.Read(buffer, 0, 2));
                CollectionAssert.AreEqual(new byte[] { 9, 8 }, buffer);
                Assert.AreEqual(0, adapter.Read(buffer, 0, 2), "EOF 且队列已空时应返回 0");
            }
        }

        /// <summary>
        /// 通道标记失败后读立即抛 <see cref="IOException"/>，原始异常作为内层异常保留。
        /// </summary>
        [TestMethod]
        public void Read_链上失败_抛带内层异常的IOException()
        {
            using (var adapter = new ChannelStreamAdapter(2, 1024))
            {
                var failure = new InvalidOperationException("decode-broken");
                Fail(adapter, failure);

                var ex = Assert.ThrowsExactly<IOException>(() => adapter.Read(new byte[1], 0, 1));
                Assert.AreSame(failure, ex.InnerException, "链上原始异常应作为内层异常保留");
            }
        }

        /// <summary>
        /// 失败时队列里未消费的块立即归还，不会挂在已死的队列上。
        /// </summary>
        [TestMethod]
        public void Fail_队列残留数据块_立即归还池化缓冲()
        {
            using (var adapter = new ChannelStreamAdapter(4, 1024))
            {
                var chunk = FtpDataChunk.CopyFrom(new byte[] { 1, 2, 3 });
                Enqueue(adapter, chunk);

                Fail(adapter, new InvalidOperationException("broken"));

                Assert.IsTrue(chunk.IsDisposed, "失败后残留数据块必须归还池化缓冲");
            }
        }

        /// <summary>
        /// 释放后未消费的块立即归还，读返回 0，写抛 <see cref="ObjectDisposedException"/>。
        /// </summary>
        [TestMethod]
        public void Dispose_有未消费数据块_归还并让读返回0写抛ObjectDisposedException()
        {
            var adapter = new ChannelStreamAdapter(4, 1024);
            var chunk = FtpDataChunk.CopyFrom(new byte[] { 7 });
            Enqueue(adapter, chunk);

            adapter.Dispose();

            Assert.IsTrue(chunk.IsDisposed, "释放适配器必须归还未消费的数据块");
            Assert.AreEqual(0, adapter.Read(new byte[1], 0, 1), "释放后读应按 EOF 返回 0");
            Assert.ThrowsExactly<ObjectDisposedException>(() => adapter.Write(new byte[1], 0, 1));
        }

        /// <summary>
        /// 队列达到块数上限时入站线程阻塞，消费者出队后放行。
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Enqueue_队列已满_阻塞到消费者出队()
        {
            using (var adapter = new ChannelStreamAdapter(1, 1024 * 1024))
            {
                adapter.ReadTimeout = WaitTimeoutMs;
                var first = FtpDataChunk.CopyFrom(new byte[] { 1, 2 });
                var second = FtpDataChunk.CopyFrom(new byte[] { 3, 4 });
                Enqueue(adapter, first);

                var blocked = Task.Run(() => Enqueue(adapter, second));
                Assert.IsFalse(blocked.Wait(200), "队列已满时入队应阻塞等待消费者出队");

                var buffer = new byte[2];
                Assert.AreEqual(2, adapter.Read(buffer, 0, 2));
                CollectionAssert.AreEqual(new byte[] { 1, 2 }, buffer);

                await blocked.WaitAsync(WaitTimeout);
                Assert.AreEqual(2, adapter.Read(buffer, 0, 2));
                CollectionAssert.AreEqual(new byte[] { 3, 4 }, buffer);
            }
        }

        /// <summary>
        /// 未绑定链写入口时写出直接拒绝，不产生无主的池化数据块。
        /// </summary>
        [TestMethod]
        public void Write_未绑定链_抛InvalidOperationException()
        {
            using (var adapter = new ChannelStreamAdapter())
            {
                Assert.ThrowsExactly<InvalidOperationException>(() => adapter.Write(new byte[] { 1 }, 0, 1));
            }
        }

        /// <summary>
        /// 绑定链后一次写入整段交给链写入口，本方法等到交接完成才返回。
        /// </summary>
        [TestMethod]
        public void Write_绑定链_整段交给链写入口()
        {
            using (var adapter = new ChannelStreamAdapter())
            using (var pipeline = new RecordingPipeline())
            {
                adapter.AttachPipeline(pipeline);
                var payload = new byte[] { 10, 20, 30 };

                adapter.Write(payload, 0, payload.Length);

                var written = (FtpDataChunk)pipeline.Writes.Single();
                Assert.AreEqual(payload.Length, written.Length);
                CollectionAssert.AreEqual(payload, written.Memory.ToArray());
                Assert.IsFalse(written.IsDisposed, "交接成功后所有权归链，适配器不得代为释放");
                written.Dispose();
            }
        }

        /// <summary>
        /// 链写入口同步抛出表示还没接手，适配器必须归还数据块再抛出原始异常。
        /// </summary>
        [TestMethod]
        public void Write_链同步抛出_归还数据块()
        {
            using (var adapter = new ChannelStreamAdapter())
            using (var pipeline = new RecordingPipeline())
            {
                pipeline.SyncFailure = new InvalidOperationException("chain-rejected");
                adapter.AttachPipeline(pipeline);

                var ex = Assert.ThrowsExactly<InvalidOperationException>(
                    () => adapter.Write(new byte[] { 1, 2 }, 0, 2));
                Assert.AreEqual("chain-rejected", ex.Message);

                var written = (FtpDataChunk)pipeline.Writes.Single();
                Assert.IsTrue(written.IsDisposed, "同步失败时数据块所有权仍在本方法，必须归还");
            }
        }

        /// <summary>
        /// 等到写超时抛 <see cref="IOException"/>，且不释放可能仍被链持有的数据块。
        /// </summary>
        [TestMethod]
        public void Write_超过写超时_抛IOException且不释放数据块()
        {
            using (var adapter = new ChannelStreamAdapter())
            using (var pipeline = new RecordingPipeline())
            {
                pipeline.BlockWrites = true;
                adapter.AttachPipeline(pipeline);
                adapter.WriteTimeout = 100;

                Assert.ThrowsExactly<IOException>(() => adapter.Write(new byte[] { 1 }, 0, 1));

                var written = (FtpDataChunk)pipeline.Writes.Single();
                Assert.IsFalse(written.IsDisposed, "超时后数据块可能仍被链持有，适配器不得释放");

                written.Dispose();
                pipeline.ReleaseWrites();
            }
        }

        /// <summary>
        /// 链写入口只能绑定一次，重复绑定会让写出走错连接，必须拒绝。
        /// </summary>
        [TestMethod]
        public void AttachPipeline_重复绑定_抛InvalidOperationException()
        {
            using (var adapter = new ChannelStreamAdapter())
            using (var first = new RecordingPipeline())
            using (var second = new RecordingPipeline())
            {
                adapter.AttachPipeline(first);

                Assert.ThrowsExactly<InvalidOperationException>(() => adapter.AttachPipeline(second));
            }
        }

        /// <summary>
        /// 入站字节经真实链路的解码器整块深拷贝后入队，同步读循环读到一致内容。
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task DataChannel_入站字节_经解码器读出一致内容()
        {
            var options = NewDataOptions();
            var adapter = new ChannelStreamAdapter(8, 512 * 1024);
            adapter.ReadTimeout = WaitTimeoutMs;

            using (var pipeline = ChannelPipeline.Create(options))
            {
                var transport = new TestRawByteStream("data-in");
                pipeline.AddLast(new FtpDataChunkEncoder());
                pipeline.AddLast(new OutboundSerializingHandler());
                pipeline.AddLast(new FtpDataChunkDecoder(adapter));
                pipeline.AddLast(new ExceptionTerminalHandler());

                var executor = new ChannelPipelineExecutor(
                    transport, new TestChannel("data-in"), options, CancellationToken.None);
                executor.Attach(pipeline);
                adapter.AttachPipeline(pipeline);
                executor.Start();

                try
                {
                    var expected = Encoding.ASCII.GetBytes("150 data channel ready\r\npayload-bytes");

                    // 一次写入可能被分帧器切成多块，读循环按块累积直到收满。
                    var reading = RunBlocking(() =>
                    {
                        var received = new List<byte>(expected.Length);
                        while (received.Count < expected.Length)
                        {
                            var buffer = new byte[expected.Length - received.Count];
                            var read = adapter.Read(buffer, 0, buffer.Length);
                            if (read == 0)
                                break;

                            for (var i = 0; i < read; i++)
                                received.Add(buffer[i]);
                        }

                        return received.ToArray();
                    });

                    await transport.InputWriteAsync(expected);
                    var actual = await reading.WaitAsync(WaitTimeout);

                    CollectionAssert.AreEqual(expected, actual);
                }
                finally
                {
                    adapter.Dispose();
                    await executor.BeginShutdown("test-done");
                    await executor.Completion.WaitAsync(WaitTimeout);
                }
            }
        }

        /// <summary>
        /// 一次到达 256KB（4 倍单帧上限）时按上限切块读出，不抛协议异常且总长与字节一致。
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task DataChannel_一次到达超过单帧上限_切块读出且不丢字节()
        {
            var options = NewDataOptions();
            var adapter = new ChannelStreamAdapter(8, 512 * 1024);
            adapter.ReadTimeout = WaitTimeoutMs;

            using (var pipeline = ChannelPipeline.Create(options))
            {
                var transport = new TestRawByteStream("data-over-limit");
                pipeline.AddLast(new FtpDataChunkEncoder());
                pipeline.AddLast(new OutboundSerializingHandler());
                pipeline.AddLast(new FtpDataChunkDecoder(adapter));
                pipeline.AddLast(new ExceptionTerminalHandler());

                var executor = new ChannelPipelineExecutor(
                    transport, new TestChannel("data-over-limit"), options, CancellationToken.None);
                executor.Attach(pipeline);
                adapter.AttachPipeline(pipeline);
                executor.Start();

                try
                {
                    var expected = new byte[4 * 64 * 1024];
                    for (var i = 0; i < expected.Length; i++)
                        expected[i] = (byte)(i % 251);

                    var reading = RunBlocking(() =>
                    {
                        var received = new List<byte>(expected.Length);
                        while (received.Count < expected.Length)
                        {
                            var buffer = new byte[expected.Length - received.Count];
                            var read = adapter.Read(buffer, 0, buffer.Length);
                            if (read == 0)
                                break;

                            for (var i = 0; i < read; i++)
                                received.Add(buffer[i]);
                        }

                        return received.ToArray();
                    });

                    // 一次写满 4 倍单帧上限，泵的一次 ReadAsync 会交出超过单帧上限的合并缓冲。
                    await transport.InputWriteAsync(expected);
                    var actual = await reading.WaitAsync(WaitTimeout);

                    Assert.AreEqual(expected.Length, actual.Length, "切块读出不得丢字节。");
                    CollectionAssert.AreEqual(expected, actual);
                }
                finally
                {
                    adapter.Dispose();
                    await executor.BeginShutdown("test-done");
                    await executor.Completion.WaitAsync(WaitTimeout);
                }
            }
        }

        /// <summary>
        /// 出站写入经编码器转成原始帧后按原字节交给传输层，并由传输层释放数据块。
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task DataChannel_出站写入_编码后按原字节交传输层()
        {
            var options = NewDataOptions();
            var adapter = new ChannelStreamAdapter(8, 512 * 1024);

            using (var pipeline = ChannelPipeline.Create(options))
            {
                var transport = new TestRawByteStream("data-out");
                pipeline.AddLast(new FtpDataChunkEncoder());
                pipeline.AddLast(new OutboundSerializingHandler());
                pipeline.AddLast(new FtpDataChunkDecoder(adapter));
                pipeline.AddLast(new ExceptionTerminalHandler());

                var executor = new ChannelPipelineExecutor(
                    transport, new TestChannel("data-out"), options, CancellationToken.None);
                executor.Attach(pipeline);
                adapter.AttachPipeline(pipeline);
                executor.Start();

                try
                {
                    var payload = Encoding.ASCII.GetBytes("outbound-data-channel-bytes");
                    adapter.Write(payload, 0, payload.Length);

                    var sent = transport.SingleSent();
                    CollectionAssert.AreEqual(payload, sent.Bytes);

                    var owner = sent.Owner as FtpDataChunk;
                    Assert.IsNotNull(owner, "传输层收到的 owner 应是数据块本身");
                    Assert.IsTrue(owner.IsDisposed, "传输层发送完成后应释放数据块");
                }
                finally
                {
                    adapter.Dispose();
                    await executor.BeginShutdown("test-done");
                    await executor.Completion.WaitAsync(WaitTimeout);
                }
            }
        }

        private static ChannelPipelineOptions NewDataOptions()
        {
            return new ChannelPipelineOptions
            {
                // 数据通道是读多少算多少的无边界透传，块大小上限取一次读取的上限。
                Splitter = new TakeAvailableSplitter(64 * 1024),
                QueueCapacity = 8,
                MaxConcurrentMessages = 1,
                DispatchMode = ChannelDispatchMode.Inline,
                MessageTimeout = TimeSpan.Zero,
                RequireTerminalHandler = true
            };
        }

        private static MethodInfo FindAdapterMethod(string name)
        {
            var method = typeof(ChannelStreamAdapter).GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (method == null)
                throw new InvalidOperationException("未找到 ChannelStreamAdapter." + name + "，适配层契约已变化。");

            return method;
        }

        private static void Enqueue(ChannelStreamAdapter adapter, FtpDataChunk chunk)
        {
            InvokeAdapter(adapter, EnqueueMethod, chunk);
        }

        private static void Complete(ChannelStreamAdapter adapter)
        {
            InvokeAdapter(adapter, CompleteMethod);
        }

        private static void Fail(ChannelStreamAdapter adapter, Exception ex)
        {
            InvokeAdapter(adapter, FailMethod, ex);
        }

        private static void InvokeAdapter(ChannelStreamAdapter adapter, MethodInfo method, params object[] args)
        {
            try
            {
                method.Invoke(adapter, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                // 还原原始异常类型，避免把库内异常包成 TargetInvocationException 后再断言失败。
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }

        private static Task<T> RunBlocking<T>(Func<T> func)
        {
            // 适配器的 Read 是同步阻塞调用，用独占线程避免占用线程池影响泵的调度。
            return Task.Factory.StartNew(
                func,
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        /// <summary>
        /// 可记录写入并模拟同步失败、等待交接的链写入口替身。
        /// </summary>
        private sealed class RecordingPipeline : IChannelPipeline
        {
            private readonly List<object> _writes = new List<object>();

            private TaskCompletionSource<bool> _gate =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Exception SyncFailure;

            public bool BlockWrites;

            public IReadOnlyList<object> Writes
            {
                get
                {
                    lock (_writes)
                        return _writes.ToList();
                }
            }

            public IChannelPipeline AddLast(IChannelHandler handler)
            {
                return this;
            }

            public IChannelPipeline AddFirst(IChannelHandler handler)
            {
                return this;
            }

            public IChannelPipeline Remove(IChannelHandler handler)
            {
                return this;
            }

            public IReadOnlyList<IChannelHandler> Handlers
            {
                get { return new IChannelHandler[0]; }
            }

            public Task FireChannelReadAsync(object msg)
            {
                return Task.CompletedTask;
            }

            public Task FireChannelReadCompleteAsync()
            {
                return Task.CompletedTask;
            }

            public Task FireExceptionCaughtAsync(Exception ex)
            {
                return Task.CompletedTask;
            }

            public Task WriteAsync(object msg)
            {
                lock (_writes)
                    _writes.Add(msg);

                if (SyncFailure != null)
                    throw SyncFailure;

                return BlockWrites ? _gate.Task : Task.CompletedTask;
            }

            public Task CloseAsync(string reason)
            {
                return Task.CompletedTask;
            }

            public void Dispose()
            {
                ReleaseWrites();
            }

            public void ReleaseWrites()
            {
                var gate = _gate;
                _gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                gate.TrySetResult(true);
            }
        }

        private sealed class SentFrame
        {
            public SentFrame(byte[] bytes, IDisposable owner)
            {
                Bytes = bytes;
                Owner = owner;
            }

            public byte[] Bytes { get; }

            public IDisposable Owner { get; }
        }

        private sealed class TestRawByteStream : IRawByteStream
        {
            private readonly Pipe _pipe;

            private readonly ConcurrentQueue<SentFrame> _sent = new ConcurrentQueue<SentFrame>();

            public TestRawByteStream(string id)
            {
                ChannelId = id;
                _pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 4 * 1024 * 1024));
            }

            public string ChannelId { get; }

            public PipeReader Input
            {
                get { return _pipe.Reader; }
            }

            public Task SendAsync(ReadOnlyMemory<byte> data, IDisposable owner)
            {
                _sent.Enqueue(new SentFrame(data.ToArray(), owner));

                // 模拟传输层：发送完成或失败后由传输层释放所有权。
                if (owner != null)
                    owner.Dispose();

                return Task.CompletedTask;
            }

            public SentFrame SingleSent()
            {
                Assert.AreEqual(1, _sent.Count, "传输层收到的帧数不符合预期");
                SentFrame frame;
                Assert.IsTrue(_sent.TryPeek(out frame));
                return frame;
            }

            public async Task InputWriteAsync(byte[] data)
            {
                await _pipe.Writer.WriteAsync(data);
                await _pipe.Writer.FlushAsync();
            }

            public void Pause()
            {
            }

            public void Resume()
            {
            }

            public Task CloseAsync(string reason)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class TestChannel : IChannel
        {
            private readonly ConcurrentDictionary<string, object> _attributes =
                new ConcurrentDictionary<string, object>();

            private int _active = 1;

            public TestChannel(string id)
            {
                Id = id;
            }

            public string Id { get; }

            public bool IsActive
            {
                get { return Volatile.Read(ref _active) == 1; }
            }

            public IReadOnlyDictionary<string, object> Attributes
            {
                get { return _attributes; }
            }

            public void SetAttribute(string key, object value)
            {
                _attributes[key] = value;
            }

            public object GetAttribute(string key)
            {
                object value;
                return _attributes.TryGetValue(key, out value) ? value : null;
            }

            public bool TryRemoveAttribute(string key, out object value)
            {
                return _attributes.TryRemove(key, out value);
            }

            public Task CloseAsync(string reason)
            {
                Volatile.Write(ref _active, 0);
                return Task.CompletedTask;
            }
        }
    }
}