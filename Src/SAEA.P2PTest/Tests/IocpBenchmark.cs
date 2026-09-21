using System;
using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SAEA.Common;
using SAEA.Sockets;
using SAEA.Sockets.Base;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// IOCP 端到端接收基准：真实 socket 收包，对比
    /// 1) 旧路径：库内复制整块 + OnReceive(byte[]) + BaseCoder.Decode（物化）
    /// 2) 新路径：OnClientReceiveSpan/OnServerReceiveSpan + BaseCoder.DecodeStream（零拷贝）
    /// 3) 纯投递：Span 事件但不解码，用于隔离库内整块复制这一项开销
    /// 接收发生在 IOCP 线程池，故用进程级 GC.GetTotalAllocatedBytes 统计分配。
    /// </summary>
    public static class IocpBenchmark
    {
        const int BodySize = 4096;
        const int FrameCount = 30000;
        const int WarmupFrames = 3000;
        const int ReadBufferSize = 8192;

        public static async Task RunAsync()
        {
            TestHarness.Section("IOCP end-to-end receive benchmark");

            var frame = StreamDecoderTest.BuildFrame((byte)SocketProtocalType.RequestSend, new byte[BodySize]);
            ConsoleHelper.WriteLine($"frame={BodySize}B payload, {frame.Length}B wire, {FrameCount} frames/run, warmup={WarmupFrames}, readBuffer={ReadBufferSize}");

            await ClientReceiveAsync(frame, ReceiveMode.LegacyDecode);
            await ClientReceiveAsync(frame, ReceiveMode.SpanDecodeStream);
            await ClientReceiveAsync(frame, ReceiveMode.SpanDeliveryOnly);

            await ServerReceiveAsync(frame, ReceiveMode.LegacyDecode);
            await ServerReceiveAsync(frame, ReceiveMode.SpanDecodeStream);
            await ServerReceiveAsync(frame, ReceiveMode.SpanDeliveryOnly);

            TestHarness.WriteSummary("IocpBenchmark");
        }

        enum ReceiveMode
        {
            LegacyDecode,
            SpanDecodeStream,
            SpanDeliveryOnly
        }

        static async Task ClientReceiveAsync(byte[] frame, ReceiveMode mode)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            var option = SocketOptionBuilder.Instance
                .UseIocp()
                .SetIP("127.0.0.1")
                .SetPort(port)
                .SetReadBufferSize(ReadBufferSize)
                .Build();

            var client = new IocpClientSocket(option);
            var counter = new ReceiveCounter();
            var decoder = new BaseCoder();

            if (mode == ReceiveMode.LegacyDecode)
                client.OnReceive += data =>
                {
                    counter.AddChunk(data.Length);
                    using (var d = decoder.Decode(new ReadOnlySequence<byte>(data)))
                    {
                        counter.AddFrames(d.Count);
                    }
                    counter.Complete();
                };
            else if (mode == ReceiveMode.SpanDecodeStream)
                client.OnClientReceiveSpan += span =>
                {
                    counter.AddChunk(span.Length);
                    decoder.DecodeStream(span, counter);
                    counter.Complete();
                };
            else
                client.OnClientReceiveSpan += span =>
                {
                    counter.AddChunk(span.Length);
                    counter.Complete();
                };

            client.ConnectAsync();
            var accepted = await listener.AcceptTcpClientAsync();
            accepted.NoDelay = true;

            var result = Drive(accepted.Client, frame, counter);

            try { client.Dispose(); } catch { }
            try { accepted.Close(); } catch { }
            listener.Stop();

            Report("client", mode, frame, result);
        }

        static async Task ServerReceiveAsync(byte[] frame, ReceiveMode mode)
        {
            int port = TestHarness.GetFreeTcpPort();
            var option = SocketOptionBuilder.Instance
                .UseIocp()
                .SetIP("127.0.0.1")
                .SetPort(port)
                .SetReadBufferSize(ReadBufferSize)
                .Build();

            var server = new IocpServerSocket(option);
            var counter = new ReceiveCounter();
            var decoder = new BaseCoder();

            if (mode == ReceiveMode.LegacyDecode)
                server.OnReceive += (token, data) =>
                {
                    counter.AddChunk(data.Length);
                    using (var d = decoder.Decode(new ReadOnlySequence<byte>(data)))
                    {
                        counter.AddFrames(d.Count);
                    }
                    counter.Complete();
                };
            else if (mode == ReceiveMode.SpanDecodeStream)
                server.OnServerReceiveSpan += (token, span) =>
                {
                    counter.AddChunk(span.Length);
                    decoder.DecodeStream(span, counter);
                    counter.Complete();
                };
            else
                server.OnServerReceiveSpan += (token, span) =>
                {
                    counter.AddChunk(span.Length);
                    counter.Complete();
                };

            server.Start();
            await Task.Delay(200);

            BenchResult result;
            using (var tcp = new TcpClient())
            {
                await tcp.ConnectAsync(IPAddress.Loopback, port);
                tcp.NoDelay = true;
                result = Drive(tcp.Client, frame, counter);
            }

            try { server.Stop(); } catch { }
            try { server.Dispose(); } catch { }

            Report("server", mode, frame, result);
        }

        static BenchResult Drive(Socket remote, byte[] frame, ReceiveCounter counter)
        {
            counter.BeginTarget((long)WarmupFrames * frame.Length);
            Task.Run(() => SendLoop(remote, frame, WarmupFrames));
            counter.WaitDone(30000);
            Thread.Sleep(100);

            counter.BeginTarget((long)FrameCount * frame.Length);
            long before = GC.GetTotalAllocatedBytes(true);
            int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
            var sw = Stopwatch.StartNew();
            Task.Run(() => SendLoop(remote, frame, FrameCount));
            bool completed = counter.WaitDone(60000);
            sw.Stop();
            long allocated = GC.GetTotalAllocatedBytes(true) - before;

            return new BenchResult
            {
                Completed = completed,
                Allocated = allocated,
                Elapsed = sw.Elapsed,
                Frames = counter.Frames,
                Chunks = counter.Chunks,
                ChunkBytes = counter.ChunkBytes,
                Gen0 = GC.CollectionCount(0) - g0,
                Gen1 = GC.CollectionCount(1) - g1,
                Gen2 = GC.CollectionCount(2) - g2
            };
        }

        static void SendLoop(Socket socket, byte[] frame, int count)
        {
            for (int i = 0; i < count; i++)
            {
                int sent = 0;
                while (sent < frame.Length)
                    sent += socket.Send(frame, sent, frame.Length - sent, SocketFlags.None);
            }
        }

        static void Report(string side, ReceiveMode mode, byte[] frame, BenchResult r)
        {
            long expectedBytes = (long)FrameCount * frame.Length;
            double ops = FrameCount / r.Elapsed.TotalSeconds;
            long bytesPerFrame = r.Allocated / FrameCount;
            double mibPerSec = (FrameCount * (double)frame.Length) / (1024 * 1024) / r.Elapsed.TotalSeconds;

            ConsoleHelper.WriteLine($"[{side}] {mode,-16} {(long)r.Elapsed.TotalMilliseconds,5} ms | {ops,10:F0} frames/s | {mibPerSec,7:F1} MiB/s | {bytesPerFrame,7} B/frame | GC0={r.Gen0,3} GC1={r.Gen1,2} GC2={r.Gen2,2} | chunks={r.Chunks}");

            TestHarness.Expect(r.Completed && r.ChunkBytes == expectedBytes,
                $"{side} {mode} received all {FrameCount} frames", $"bytes={r.ChunkBytes}/{expectedBytes}");

            if (mode != ReceiveMode.SpanDeliveryOnly)
                TestHarness.Expect(r.Frames == FrameCount,
                    $"{side} {mode} decoded all frames", $"frames={r.Frames}");
        }

        struct BenchResult
        {
            public bool Completed;
            public long Allocated;
            public TimeSpan Elapsed;
            public long Frames;
            public long Chunks;
            public long ChunkBytes;
            public int Gen0;
            public int Gen1;
            public int Gen2;
        }

        sealed class ReceiveCounter : IFrameHandler
        {
            long _chunkBytes;
            long _chunks;
            long _frames;
            long _frameBytes;
            long _targetBytes = long.MaxValue;
            readonly ManualResetEventSlim _done = new ManualResetEventSlim(false);

            public long ChunkBytes => Interlocked.Read(ref _chunkBytes);
            public long Chunks => Interlocked.Read(ref _chunks);
            public long Frames => Interlocked.Read(ref _frames);

            public void BeginTarget(long targetBytes)
            {
                _done.Reset();
                Interlocked.Exchange(ref _chunkBytes, 0);
                Interlocked.Exchange(ref _chunks, 0);
                Interlocked.Exchange(ref _frames, 0);
                Interlocked.Exchange(ref _frameBytes, 0);
                Interlocked.Exchange(ref _targetBytes, targetBytes);
            }

            public bool WaitDone(int timeoutMs) => _done.Wait(timeoutMs);

            public void AddChunk(int length)
            {
                Interlocked.Increment(ref _chunks);
                Interlocked.Add(ref _chunkBytes, length);
            }

            public void AddFrames(int count)
            {
                if (count > 0) Interlocked.Add(ref _frames, count);
            }

            public void OnFrame(in SocketFrame frame)
            {
                Interlocked.Increment(ref _frames);
                Interlocked.Add(ref _frameBytes, frame.Content.Length);
            }

            public void Complete()
            {
                if (Interlocked.Read(ref _chunkBytes) >= Interlocked.Read(ref _targetBytes))
                    _done.Set();
            }
        }
    }
}