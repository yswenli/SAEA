/****************************************************************************
*项目名称：SAEA.Http2.Core
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Core
*类 名 称：ConnectionWriter
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 16:37:47
*描述：
*=====================================================================
*修改时间：2019/6/27 16:37:47
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.Http2.Events;
using SAEA.Http2.Interfaces;
using SAEA.Http2.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Http2.Core
{
    /// <summary>
    /// 写入连接的任务
    /// </summary>
    internal class ConnectionWriter
    {
        /// <summary>
        /// ConnectionWriter的配置选项
        /// </summary>
        public struct Options
        {
            public int MaxFrameSize;
            public int MaxHeaderListSize;
            public int DynamicTableSizeLimit;
            public int InitialWindowSize;
        }

        private struct StreamData
        {
            public uint StreamId;
            public int Window;
            public Queue<WriteRequest> WriteQueue;
            public bool EndOfStreamQueued;
        }

        private struct SharedData
        {
            public object Mutex;
            public int InitialWindowSize;
            public Queue<WriteRequest> WriteQueue;
            public List<StreamData> Streams;
            public ChangeSettingsRequest ChangeSettingsRequest;
            public bool CloseRequested;
        }

        /// <summary>
        /// 表示写入操作的结果
        /// </summary>
        public enum WriteResult
        {
            InProgress,
            Success,
            ConnectionError,
            ConnectionClosedError,
            StreamResetError,
        }

        // 这需要是一个类，才能进行可变
        private class WriteRequest
        {
            public FrameHeader Header;
            public WindowUpdateData WindowUpdateData;
            public ResetFrameData ResetFrameData;
            public GoAwayFrameData GoAwayData;
            public IEnumerable<HeaderField> Headers;
            public ArraySegment<byte> Data;
            public AsyncManualResetEvent Completed;
            public WriteResult Result;
        }

        private class ChangeSettingsRequest
        {
            public Settings NewRemoteSettings;
            public AsyncManualResetEvent Completed;
            public Http2Error? Result;
        }


        public Connection Connection { get; }

        private readonly IWriteAndCloseableByteStream outStream;


        private readonly HPack.Encoder hEncoder;


        private int closeConnectionIssued = 0;


        private SharedData shared = new SharedData();
        private AsyncManualResetEvent wakeupWriter = new AsyncManualResetEvent(false);
        private Task writeTask;

        private byte[] outBuf;


        private readonly int DynamicTableSizeLimit;


        private int connFlowWindow = Constants.InitialConnectionWindowSize;

        private int MaxFrameSize;

        private int MaxHeaderListSize;


        public Task Done => writeTask;


        public ConnectionWriter(
            Connection connection, IWriteAndCloseableByteStream outStream,
            Options options, HPack.Encoder.Options hpackOptions)
        {
            this.Connection = connection;
            this.outStream = outStream;
            this.DynamicTableSizeLimit = options.DynamicTableSizeLimit;
            this.MaxFrameSize = options.MaxFrameSize;
            this.MaxHeaderListSize = options.MaxHeaderListSize;
            this.hEncoder = new HPack.Encoder(hpackOptions);

            shared.Mutex = new object();
            shared.CloseRequested = false;
            shared.InitialWindowSize = options.InitialWindowSize;
            shared.WriteQueue = new Queue<WriteRequest>();
            shared.Streams = new List<StreamData>();

            this.writeTask = Task.Run(() => this.RunAsync());
        }

        internal void EnsureBuffer(int minSize)
        {
            if (outBuf != null)
            {
                if (outBuf.Length >= minSize) return;

                Connection.config.BufferPool.Return(outBuf);
                outBuf = null;
            }

            outBuf = Connection.config.BufferPool.Rent(minSize);
        }

        internal void ReleaseBuffer(int treshold)
        {
            if (outBuf == null || outBuf.Length <= treshold) return;
            Connection.config.BufferPool.Return(outBuf);
            outBuf = null;
        }


        private async Task RunAsync()
        {
            try
            {
                EnsureBuffer(Connection.PersistentBufferSize);


                if (!Connection.IsServer)
                {
                    await ClientPreface.WriteAsync(outStream);
                }

                await WriteSettingsAsync(Connection.localSettings);

                bool continueRun = true;
                while (continueRun)
                {
                    await this.wakeupWriter;

                    WriteRequest writeRequest = null;
                    ChangeSettingsRequest changeSettings = null;
                    bool doClose = false;

                    lock (shared.Mutex)
                    {
                        // 1: 应用新设置
                        // 2: 写一帧
                        // 3: 如果需要，请关闭连接
                        if (shared.ChangeSettingsRequest != null)
                        {
                            changeSettings = shared.ChangeSettingsRequest;
                            shared.ChangeSettingsRequest = null;
                        }
                        else
                        {
                            writeRequest = GetNextReadyWriteRequest();
                        }

                        if (changeSettings == null && writeRequest == null)
                        {
                            if (shared.CloseRequested)
                            {
                                doClose = true;
                            }
                            else
                            {
                                this.wakeupWriter.Reset();
                            }
                        }
                    }

                    if (changeSettings != null)
                    {
                        var err = ApplyNewSettings(
                            changeSettings.NewRemoteSettings);
                        if (err == null)
                        {
                            EnsureBuffer(Connection.PersistentBufferSize);
                            await WriteSettingsAckAsync();
                        }
                        changeSettings.Result = err;
                        changeSettings.Completed.Set();
                    }
                    else if (writeRequest != null)
                    {
                        EnsureBuffer(Connection.PersistentBufferSize);
                        await ProcessWriteRequestAsync(writeRequest);
                        ReleaseBuffer(Connection.PersistentBufferSize);
                    }
                    else if (doClose)
                    {
                        continueRun = false;
                    }
                }
            }
            catch (Exception e)
            {
                LogHelper.Error("ConnectionWriter.RunAsync", e);
            }


            await CloseNow(false);


            lock (shared.Mutex)
            {
                shared.CloseRequested = true;

                if (shared.ChangeSettingsRequest != null)
                {
                    shared.ChangeSettingsRequest.Completed.Set();
                    shared.ChangeSettingsRequest = null;
                }

                FinishAllOutstandingWritesLocked();
            }


            if (outBuf != null)
            {
                Connection.config.BufferPool.Return(outBuf);
                outBuf = null;
            }
        }


        private async ValueTask<DoneHandle> CloseNow(bool needWakeup)
        {
            if (Interlocked.CompareExchange(ref closeConnectionIssued, 1, 0) == 0)
            {
                try
                {
                    await outStream.CloseAsync();
                }
                catch (Exception)
                {

                }

                if (needWakeup)
                {
                    lock (shared.Mutex)
                    {
                        shared.CloseRequested = true;
                    }
                    wakeupWriter.Set();
                }
            }

            return DoneHandle.Instance;
        }

        /// <summary>
        /// 立即强制关闭连接。
        /// </summary>
        public ValueTask<DoneHandle> CloseNow()
        {
            return CloseNow(true);
        }


        private WriteRequest GetNextReadyWriteRequest()
        {
            if (shared.WriteQueue.Count > 0)
            {
                var writeRequest = shared.WriteQueue.Dequeue();
                return writeRequest;
            }

            for (var i = 0; i < shared.Streams.Count; i++)
            {
                var s = shared.Streams[i];
                if (s.WriteQueue.Count == 0) continue;
                var first = s.WriteQueue.Peek();

                if (first.Header.Type == FrameType.Data)
                {
                    var canSend = Math.Min(this.connFlowWindow, s.Window);

                    canSend = Math.Min(canSend, MaxFrameSize);

                    if (canSend <= 0 && first.Data.Count != 0) continue;


                    var toSend = Math.Min(canSend, first.Data.Count);
                    connFlowWindow -= toSend;
                    s.Window -= toSend;
                    shared.Streams[i] = s;

                    if (canSend < first.Data.Count)
                    {
                        var we = AllocateWriteRequest();
                        we.Header = first.Header;
                        we.Header.Flags = 0;
                        we.Data = new ArraySegment<byte>(
                            first.Data.Array, first.Data.Offset, canSend);
                        var oldData = first.Data;
                        first.Data = new ArraySegment<byte>(
                            oldData.Array, oldData.Offset + canSend, oldData.Count - canSend);
                        return we;
                    }
                }

                first = s.WriteQueue.Dequeue();

                if (first.Header.Type == FrameType.ResetStream || first.Header.HasEndOfStreamFlag)
                {
                    shared.Streams.RemoveAt(i);

                    if (s.WriteQueue.Count > 0)
                    {
                        throw new Exception(
                            "意外：流的writeQueue在endofstream之后包含数据");
                    }
                }
                return first;
            }

            return null;
        }

        private async Task ProcessWriteRequestAsync(WriteRequest wr)
        {
            try
            {
                switch (wr.Header.Type)
                {
                    case FrameType.Headers:
                        await WriteHeadersAsync(wr);
                        break;
                    case FrameType.PushPromise:
                        await WritePushPromiseAsync(wr);
                        break;
                    case FrameType.Data:
                        await WriteDataFrameAsync(wr);
                        break;
                    case FrameType.GoAway:
                        await WriteGoAwayFrameAsync(wr);
                        break;
                    case FrameType.Continuation:
                        throw new Exception("continuations可以不被直接排队");
                    case FrameType.Ping:
                        await WritePingFrameAsync(wr);
                        break;
                    case FrameType.Priority:
                        await WritePriorityFrameAsync(wr);
                        break;
                    case FrameType.ResetStream:
                        await WriteResetFrameAsync(wr);
                        break;
                    case FrameType.WindowUpdate:
                        await WriteWindowUpdateFrame(wr);
                        break;
                    case FrameType.Settings:
                        throw new Exception("不支持设置更改");
                    default:
                        throw new Exception("未知的帧类型");
                }
                wr.Result = WriteResult.Success;
            }
            catch (Exception)
            {
                wr.Result = WriteResult.ConnectionError;
                throw;
            }
            finally
            {
                if (wr.Header.HasEndOfStreamFlag)
                {
                    lock (shared.Mutex)
                    {
                        RemoveStreamLocked(wr.Header.StreamId);
                    }
                }

                wr.Completed.Set();
            }
        }

        private Task WriteWindowUpdateFrame(WriteRequest wr)
        {
            wr.Header.Length = WindowUpdateData.Size;

            wr.Header.EncodeInto(
                new ArraySegment<byte>(outBuf, 0, FrameHeader.HeaderSize));

            wr.WindowUpdateData.EncodeInto(new ArraySegment<byte>(
                outBuf, FrameHeader.HeaderSize, WindowUpdateData.Size));
            var totalSize = FrameHeader.HeaderSize + WindowUpdateData.Size;
            var data = new ArraySegment<byte>(outBuf, 0, totalSize);

            return this.outStream.WriteAsync(data);
        }

        private bool TryEnqueueWriteRequestLocked(uint streamId, WriteRequest wr)
        {
            if (streamId == 0 ||
                wr.Header.Type == FrameType.WindowUpdate ||
                wr.Header.Type == FrameType.ResetStream)
            {
                shared.WriteQueue.Enqueue(wr);

                if (wr.Header.Type == FrameType.ResetStream && streamId != 0)
                {
                    this.RemoveStreamLocked(streamId);
                }

                return true;
            }


            for (var i = 0; i < shared.Streams.Count; i++)
            {
                var stream = shared.Streams[i];
                if (stream.StreamId == streamId)
                {

                    if (wr.Header.HasEndOfStreamFlag)
                    {
                        stream.EndOfStreamQueued = true;
                        shared.Streams[i] = stream;
                    }
                    stream.WriteQueue.Enqueue(wr);
                    return true;
                }
            }


            return false;
        }


        private static readonly ConcurrentBag<WriteRequest> writeRequestPool =
            new ConcurrentBag<WriteRequest>();

        private const int MaxPooledWriteRequests = 10 * 1024;


        private WriteRequest AllocateWriteRequest()
        {
            WriteRequest r;
            if (writeRequestPool.TryTake(out r))
            {
                return r;
            }

            var wr = new WriteRequest()
            {
                Completed = new AsyncManualResetEvent(false),
            };
            return wr;
        }


        private void ReleaseWriteRequest(WriteRequest wr)
        {
            wr.Result = WriteResult.InProgress;
            wr.Headers = null;

            wr.Data = Constants.EmptyByteArray;
            wr.GoAwayData.Reason.DebugData = Constants.EmptyByteArray;
            wr.Completed.Reset();


            if (writeRequestPool.Count < MaxPooledWriteRequests)
            {
                writeRequestPool.Add(wr);
            }

        }

        private async ValueTask<WriteResult> PerformWriteRequestAsync(
            uint streamId, Action<WriteRequest> populateRequest, bool closeAfterwards)
        {
            WriteRequest wr = null;
            lock (shared.Mutex)
            {
                if (shared.CloseRequested)
                {
                    return WriteResult.ConnectionClosedError;
                }
                if (closeAfterwards)
                {
                    shared.CloseRequested = true;
                }

                wr = AllocateWriteRequest();
                populateRequest(wr);

                var enqueued = TryEnqueueWriteRequestLocked(streamId, wr);
                if (!enqueued)
                {
                    return WriteResult.StreamResetError;
                }
            }


            wakeupWriter.Set();

            await wr.Completed;

            var result = wr.Result;
            ReleaseWriteRequest(wr);

            if (result == WriteResult.InProgress)
            {
                throw new Exception(
                    "Unexpected: Write is still marked as in progress");
            }

            return result;
        }

        public ValueTask<WriteResult> WriteHeaders(
            FrameHeader header, IEnumerable<HeaderField> headers)
        {
            return PerformWriteRequestAsync(
                header.StreamId,
                wr => {
                    wr.Header = header;
                    wr.Headers = headers;
                },
                false);
        }

        public ValueTask<WriteResult> WriteResetStream(
            FrameHeader header, ResetFrameData data)
        {
            return PerformWriteRequestAsync(
                header.StreamId,
                wr => {
                    wr.Header = header;
                    wr.ResetFrameData = data;
                },
                false);
        }

        public ValueTask<WriteResult> WritePing(
            FrameHeader header, ArraySegment<byte> data)
        {
            return PerformWriteRequestAsync(
                0,
                wr => {
                    wr.Header = header;
                    wr.Data = data;
                },
                false);
        }

        public ValueTask<WriteResult> WriteWindowUpdate(
            FrameHeader header, WindowUpdateData data)
        {
            return PerformWriteRequestAsync(
                header.StreamId,
                wr => {
                    wr.Header = header;
                    wr.WindowUpdateData = data;
                },
                false);
        }

        public ValueTask<WriteResult> WriteGoAway(
            FrameHeader header, GoAwayFrameData data, bool closeAfterwards)
        {
            return PerformWriteRequestAsync(
                0,
                wr => {
                    wr.Header = header;
                    wr.GoAwayData = data;
                },
                closeAfterwards);
        }

        public ValueTask<WriteResult> WriteData(
            FrameHeader header, ArraySegment<byte> data)
        {
            return PerformWriteRequestAsync(
                header.StreamId,
                wr => {
                    wr.Header = header;
                    wr.Data = data;
                },
                false);
        }


        private async Task WriteDataFrameAsync(WriteRequest wr)
        {
            wr.Header.Flags = (byte)((wr.Header.Flags & ~((uint)DataFrameFlags.Padded)) & 0xFF);
            wr.Header.Length = wr.Data.Count;

            var headerView = new ArraySegment<byte>(outBuf, 0, FrameHeader.HeaderSize);
            wr.Header.EncodeInto(headerView);

            await this.outStream.WriteAsync(headerView);
            await this.outStream.WriteAsync(wr.Data);
        }

        /// <summary>
        /// Writes a PING frame
        /// </summary>
        private Task WritePingFrameAsync(WriteRequest wr)
        {
            wr.Header.Length = 8;
            var headerView = new ArraySegment<byte>(outBuf, 0, FrameHeader.HeaderSize);
            wr.Header.EncodeInto(headerView);

            Array.Copy(wr.Data.Array, wr.Data.Offset, outBuf, FrameHeader.HeaderSize, 8);
            var totalSize = FrameHeader.HeaderSize + 8;
            var data = new ArraySegment<byte>(outBuf, 0, totalSize);

            return this.outStream.WriteAsync(data);
        }


        private Task WriteGoAwayFrameAsync(WriteRequest wr)
        {
            var dataSize = wr.GoAwayData.RequiredSize;
            var totalSize = FrameHeader.HeaderSize + dataSize;
            EnsureBuffer(totalSize);

            wr.Header.Length = dataSize;
            var headerView = new ArraySegment<byte>(outBuf, 0, FrameHeader.HeaderSize);
            wr.Header.EncodeInto(headerView);

            wr.GoAwayData.EncodeInto(
                new ArraySegment<byte>(outBuf, FrameHeader.HeaderSize, dataSize));
            var data = new ArraySegment<byte>(outBuf, 0, totalSize);

            return this.outStream.WriteAsync(data);
        }


        private Task WriteResetFrameAsync(WriteRequest wr)
        {
            wr.Header.Length = ResetFrameData.Size;
            wr.Header.EncodeInto(new ArraySegment<byte>(outBuf, 0, FrameHeader.HeaderSize));
            wr.ResetFrameData.EncodeInto(
                new ArraySegment<byte>(outBuf, FrameHeader.HeaderSize, ResetFrameData.Size));
            var totalSize = FrameHeader.HeaderSize + ResetFrameData.Size;
            var data = new ArraySegment<byte>(outBuf, 0, totalSize);


            return this.outStream.WriteAsync(data);
        }


        private Task WriteSettingsAsync(Settings settings)
        {
            var fh = new FrameHeader
            {
                Type = FrameType.Settings,
                StreamId = 0u,
                Length = settings.RequiredSize,
                Flags = 0,
            };

            fh.EncodeInto(
                new ArraySegment<byte>(outBuf, 0, FrameHeader.HeaderSize));
            settings.EncodeInto(new ArraySegment<byte>(
                outBuf, FrameHeader.HeaderSize, settings.RequiredSize));
            var totalSize = FrameHeader.HeaderSize + settings.RequiredSize;
            var data = new ArraySegment<byte>(outBuf, 0, totalSize);
            return this.outStream.WriteAsync(data);
        }


        private Task WriteSettingsAckAsync()
        {
            var fh = new FrameHeader
            {
                Type = FrameType.Settings,
                StreamId = 0u,
                Length = 0,
                Flags = (byte)SettingsFrameFlags.Ack,
            };

            var headerView = new ArraySegment<byte>(outBuf, 0, FrameHeader.HeaderSize);
            fh.EncodeInto(headerView);
            return this.outStream.WriteAsync(headerView);
        }


        private async Task WriteHeadersAsync(WriteRequest wr)
        {

            EnsureBuffer(FrameHeader.HeaderSize + MaxFrameSize);


            var maxFrameSize = Math.Min(
                MaxFrameSize,
                outBuf.Length - FrameHeader.HeaderSize);

            var headerView = new ArraySegment<byte>(
                outBuf, 0, FrameHeader.HeaderSize);

            var headers = wr.Headers;
            var nrTotalHeaders = headers.Count();
            var sentHeaders = 0;
            var isContinuation = false;

            while (true)
            {
                var headerBlockFragment = new ArraySegment<byte>(
                    outBuf, FrameHeader.HeaderSize, maxFrameSize);
                var encodeResult = this.hEncoder.EncodeInto(
                    headerBlockFragment, headers);

                if (encodeResult.FieldCount == 0 && (nrTotalHeaders - sentHeaders) != 0)
                {
                    throw new Exception(
                        "遇到过大的headerfield");
                }

                sentHeaders += encodeResult.FieldCount;
                var remaining = nrTotalHeaders - sentHeaders;

                FrameHeader hdr = wr.Header;
                hdr.Length = encodeResult.UsedBytes;
                if (!isContinuation)
                {
                    hdr.Type = FrameType.Headers;
                    if (remaining == 0)
                    {
                        hdr.Flags |= (byte)HeadersFrameFlags.EndOfHeaders;
                    }
                    else
                    {
                        var f = hdr.Flags & ~((byte)HeadersFrameFlags.EndOfHeaders);
                        hdr.Flags = (byte)f;
                    }
                }
                else
                {
                    hdr.Type = FrameType.Continuation;
                    hdr.Flags = 0;
                    if (remaining == 0)
                    {
                        hdr.Flags = (byte)ContinuationFrameFlags.EndOfHeaders;
                    }
                }

                hdr.EncodeInto(headerView);
                var dataView = new ArraySegment<byte>(
                    outBuf, 0, FrameHeader.HeaderSize + encodeResult.UsedBytes);
                await this.outStream.WriteAsync(dataView);

                if (remaining == 0)
                {
                    break;
                }
                else
                {
                    isContinuation = true;

                    headers = wr.Headers.Skip(sentHeaders);
                }
            }
        }

        private ValueTask<object> WritePushPromiseAsync(WriteRequest wr)
        {
            throw new NotSupportedException("不支持的推送");
        }

        private ValueTask<object> WritePriorityFrameAsync(WriteRequest wr)
        {
            throw new NotSupportedException("不支持优先级");
        }

        /// <summary>
        /// 注册一个新的流，对于该流，帧必须在编写器上传输。
        /// </summary>
        /// <param name="streamId"></param>
        /// <returns></returns>
        public bool RegisterStream(uint streamId)
        {
            lock (shared.Mutex)
            {
                if (shared.CloseRequested)
                {
                    return false;
                }

                var sd = new StreamData
                {
                    StreamId = streamId,
                    Window = shared.InitialWindowSize, 
                    WriteQueue = new Queue<WriteRequest>(),
                    EndOfStreamQueued = false,
                };
                shared.Streams.Add(sd);
            }

            return true;
        }

        /// <summary>
        /// 从编写器中删除具有给定流ID的流。
        /// </summary>
        /// <param name="streamId"></param>
        public void RemoveStream(uint streamId)
        {
            lock (shared.Mutex)
            {
                RemoveStreamLocked(streamId);
            }
        }

        private void RemoveStreamLocked(uint streamId)
        {
            Queue<WriteRequest> writeQueue = null;
            for (var i = 0; i < shared.Streams.Count; i++)
            {
                var s = shared.Streams[i];
                if (s.StreamId == streamId)
                {
                    writeQueue = s.WriteQueue;
                    shared.Streams.RemoveAt(i);
                    break;
                }
            }

            if (writeQueue != null)
            {
                foreach (var elem in writeQueue)
                {
                    elem.Result = WriteResult.StreamResetError;
                    elem.Completed.Set();
                }
            }
        }

        private void FinishAllOutstandingWritesLocked()
        {
            var streamsMap = shared.Streams;
            foreach (var stream in shared.Streams)
            {
                foreach (var elem in stream.WriteQueue)
                {
                    elem.Result = WriteResult.StreamResetError;
                    elem.Completed.Set();
                }
                stream.WriteQueue.Clear();
            }
            streamsMap.Clear();

            foreach (var elem in shared.WriteQueue)
            {
                elem.Result = WriteResult.StreamResetError;
                elem.Completed.Set();
            }
            shared.WriteQueue.Clear();
        }

        /// <summary>
        /// 指示编写器使用远程所需的新设置并发送设置确认帧。
        /// </summary>
        /// <param name="newRemoteSettings"></param>
        /// <returns></returns>
        public async Task<Http2Error?> ApplyAndAckRemoteSettings(
            Settings newRemoteSettings)
        {
            ChangeSettingsRequest changeRequest = null;

            lock (shared.Mutex)
            {
                if (shared.CloseRequested) return null;

                changeRequest = new ChangeSettingsRequest
                {
                    NewRemoteSettings = newRemoteSettings,
                    Completed = new AsyncManualResetEvent(false),
                };
                shared.ChangeSettingsRequest = changeRequest;
            }

            wakeupWriter.Set();

            await changeRequest.Completed;
            return changeRequest.Result;
        }

        private Http2Error? ApplyNewSettings(
            Settings remoteSettings)
        {
            this.MaxFrameSize = (int)remoteSettings.MaxFrameSize;

            this.MaxHeaderListSize = (int)remoteSettings.MaxHeaderListSize;

            var newRequestedTableSize = (int)remoteSettings.HeaderTableSize;
            if (newRequestedTableSize > this.hEncoder.DynamicTableSize)
            {
                this.hEncoder.DynamicTableSize =
                    Math.Min(newRequestedTableSize, DynamicTableSizeLimit);
            }
            else
            {
                this.hEncoder.DynamicTableSize = newRequestedTableSize;
            }

            lock (shared.Mutex)
            {
                var initialWindowSizeDelta =
                    (int)remoteSettings.InitialWindowSize - shared.InitialWindowSize;

                shared.InitialWindowSize = (int)remoteSettings.InitialWindowSize;

                return UpdateAllStreamWindowsLocked(initialWindowSizeDelta);
            }
        }

        /// <summary>
        /// 按数量更新给定流的流控制窗口。
        /// </summary>
        /// <param name="streamId"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        public Http2Error? UpdateFlowControlWindow(uint streamId, int amount)
        {
            var wakeup = false;

            if (amount < 1)
            {
                return new Http2Error
                {
                    Code = ErrorCode.ProtocolError,
                    StreamId = streamId,
                    Message = "收到无效的流控制窗口更新",
                };
            }

            lock (shared.Mutex)
            {
                if (streamId == 0)
                {
                    var updatedValue = (long)connFlowWindow + (long)amount;
                    if (updatedValue > (long)int.MaxValue)
                    {
                        return new Http2Error
                        {
                            StreamId = 0,
                            Code = ErrorCode.FlowControlError,
                            Message = "流量控制窗口溢出",
                        };
                    }
                    if (connFlowWindow == 0) wakeup = true;
                    connFlowWindow = (int)updatedValue;
                }
                else
                {
                    for (var i = 0; i < shared.Streams.Count; i++)
                    {
                        if (shared.Streams[i].StreamId == streamId)
                        {
                            var s = shared.Streams[i];
                            var updatedValue = (long)s.Window + (long)amount;
                            if (updatedValue > (long)int.MaxValue)
                            {
                                return new Http2Error
                                {
                                    Code = ErrorCode.FlowControlError,
                                    StreamId = streamId,
                                    Message = "流量控制窗口溢出",
                                };
                            }
                            s.Window = (int)updatedValue;
                            shared.Streams[i] = s;
                            if (s.Window > 0 && s.WriteQueue.Count > 0)
                            {
                                wakeup = true;
                            }
                            break;
                        }
                    }
                }
            }

            if (wakeup)
            {
                this.wakeupWriter.Set();
            }
            return null;
        }

        private Http2Error? UpdateAllStreamWindowsLocked(int amount)
        {
            bool hasOverflow = false;
            if (amount == 0) return null;

            for (var i = 0; i < shared.Streams.Count; i++)
            {
                var s = shared.Streams[i];

                var updatedValue = (long)s.Window + (long)amount;
                if (updatedValue > (long)int.MaxValue ||
                    updatedValue < (long)int.MinValue)
                {
                    hasOverflow = true;
                    break;
                }
                s.Window += amount;
                shared.Streams[i] = s;
            }

            if (hasOverflow)
            {
                return new Http2Error
                {
                    Code = ErrorCode.FlowControlError,
                    StreamId = 0u,
                    Message = "通过设置更新的流控制窗口溢出",
                };
            }
            else
            {
                return null;
            }
        }
    }
}
