/****************************************************************************
*项目名称：SAEA.Http2.Core
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Core
*类 名 称：Connection
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 15:54:01
*描述：
*=====================================================================
*修改时间：2019/6/27 15:54:01
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Http2.Extentions;
using SAEA.Http2.Interfaces;
using SAEA.Http2.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Http2.Core
{
    public class Connection
    {
        public struct Options
        {
            public ServerUpgradeRequest ServerUpgradeRequest;

            public ClientUpgradeRequest ClientUpgradeRequest;
        }

        private struct SharedData
        {
            public object Mutex;

            public bool Closed;

            public Dictionary<uint, StreamImpl> streamMap;

            public uint LastOutgoingStreamId;

            public uint LastIncomingStreamId;

            public bool GoAwaySent;

            public PingState PingState;
        }


        internal const int PersistentBufferSize = 128;


        private class PingState
        {
            public ulong Counter = 0;
            public readonly Dictionary<ulong, TaskCompletionSource<bool>> PingMap =
                new Dictionary<ulong, TaskCompletionSource<bool>>();
        }


        private class ClientState
        {
            public readonly SemaphoreSlim CreateStreamMutex = new SemaphoreSlim(1);
        }

        private SharedData shared;
        private ClientState clientState;

        byte[] receiveBuffer;

        bool settingsReceived = false;
        int nrUnackedSettings = 0;
        private int connReceiveFlowWindow = Constants.InitialConnectionWindowSize;

        internal readonly ConnectionWriter writer;

        internal readonly IReadableByteStream inputStream;

        private readonly Task readerDone;

        internal readonly ConnectionConfiguration config;

        private readonly HeaderReader headerReader;

        internal readonly Settings localSettings;

        internal Settings remoteSettings = Settings.Default;


        internal ServerUpgradeRequest serverUpgradeRequest;

        private TaskCompletionSource<GoAwayReason> remoteGoAwayTcs =
            new TaskCompletionSource<GoAwayReason>();


        public bool IsServer => config.IsServer;


        public Task Done => readerDone;


        public int ActiveStreamCount
        {
            get
            {
                lock (shared.Mutex) { return shared.streamMap.Count; }
            }
        }


        public Task<GoAwayReason> RemoteGoAwayReason => remoteGoAwayTcs.Task;

        /// <summary>
        /// 在双向流的顶部创建新的HTTP/2连接
        /// </summary>
        /// <param name="config"></param>
        /// <param name="inputStream"></param>
        /// <param name="outputStream"></param>
        /// <param name="options"></param>
        public Connection(ConnectionConfiguration config, IReadableByteStream inputStream, IWriteAndCloseableByteStream outputStream, Options? options = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            this.config = config;

            if (!config.IsServer)
            {
                clientState = new ClientState();
            }

            localSettings = config.Settings;

            localSettings.EnablePush = false;


            if (IsServer && options?.ServerUpgradeRequest != null)
            {
                serverUpgradeRequest = options.Value.ServerUpgradeRequest;
                if (!serverUpgradeRequest.IsValid)
                {

                    throw new ArgumentException(
                        "ServerUpgradeRequest无效.\n" +
                        "无效的HTTP/1升级请求必须被拒绝");
                }
                else
                {
                    remoteSettings = serverUpgradeRequest.Settings;
                }
            }

            if (inputStream == null) throw new ArgumentNullException(nameof(inputStream));
            if (outputStream == null) throw new ArgumentNullException(nameof(outputStream));
            this.inputStream = inputStream;


            shared.Mutex = new object();
            shared.streamMap = new Dictionary<uint, StreamImpl>();
            shared.LastOutgoingStreamId = 0u;
            shared.LastIncomingStreamId = 0u;
            shared.GoAwaySent = false;
            shared.Closed = false;
            shared.PingState = null;


            if (!IsServer && options?.ClientUpgradeRequest != null)
            {
                var upgrade = options.Value.ClientUpgradeRequest;
                if (!upgrade.IsValid)
                {
                    throw new ArgumentException(
                        "ClientUpgradeRequest无效。\n" +
                        "无效的升级请求必须被HTTP/1处理程序拒绝");
                }

                localSettings = upgrade.Settings;

                var newStream = new StreamImpl(
                    this,
                    1u,
                    StreamState.HalfClosedLocal,
                    (int)localSettings.InitialWindowSize);

                shared.streamMap[1u] = newStream;
                shared.LastOutgoingStreamId = 1u;
                var setStream =
                    upgrade.UpgradeRequestStreamTcs.TrySetResult(newStream);
            }

            var dynTableSizeLimit = Math.Min(localSettings.HeaderTableSize, int.MaxValue);

            writer = new ConnectionWriter(
                this, outputStream,
                new ConnectionWriter.Options
                {
                    MaxFrameSize = (int)remoteSettings.MaxFrameSize,
                    MaxHeaderListSize = (int)remoteSettings.MaxHeaderListSize,
                    InitialWindowSize = (int)remoteSettings.InitialWindowSize,
                    DynamicTableSizeLimit = (int)dynTableSizeLimit,
                },
                new HPack.Encoder.Options
                {
                    DynamicTableSize = (int)remoteSettings.HeaderTableSize,
                    HuffmanStrategy = config.HuffmanStrategy,
                }
            );

            nrUnackedSettings++;

            headerReader = new HeaderReader(
                new HPack.Decoder(new HPack.Decoder.Options
                {

                    DynamicTableSizeLimit = (int)dynTableSizeLimit,
                    BufferPool = config.BufferPool,
                }),
                localSettings.MaxFrameSize,
                localSettings.MaxHeaderListSize,
                inputStream
            );

            readerDone = Task.Run(() => this.RunReaderAsync());
        }

        internal void EnsureBuffer(int minSize)
        {
            if (receiveBuffer != null)
            {
                if (receiveBuffer.Length >= minSize) return;
                config.BufferPool.Return(receiveBuffer);
                receiveBuffer = null;
            }

            receiveBuffer = config.BufferPool.Rent(minSize);
        }

        internal void ReleaseBuffer(int treshold)
        {
            if (receiveBuffer == null || receiveBuffer.Length <= treshold) return;
            config.BufferPool.Return(receiveBuffer);
            receiveBuffer = null;
        }

        internal byte[] GetBuffer(int minSize)
        {
            EnsureBuffer(minSize);
            return receiveBuffer;
        }

        private async Task RunReaderAsync()
        {
            try
            {
                if (IsServer)
                {
                    EnsureBuffer(ClientPreface.Length);
                    await ClientPreface.ReadAsync(inputStream, config.ClientPrefaceTimeout);
                }

                var continueRead = true;

                if (serverUpgradeRequest != null)
                {
                    var upgrade = serverUpgradeRequest;
                    serverUpgradeRequest = null;

                    var headers = new CompleteHeadersFrameData
                    {
                        StreamId = 1u,
                        Priority = null,
                        Headers = upgrade.Headers,
                        EndOfStream = upgrade.Payload == null,
                    };

                    var err = await HandleHeaders(headers);
                    if (err != null)
                    {
                        if (err.Value.StreamId == 0)
                            continueRead = false;
                        await HandleFrameProcessingError(err.Value);
                    }
                    else if (upgrade.Payload != null)
                    {
                        var buf = config.BufferPool.Rent(upgrade.Payload.Length);
                        Array.Copy(
                            upgrade.Payload, 0, buf, 0, upgrade.Payload.Length);

                        StreamImpl stream = null;
                        lock (shared.Mutex)
                        {
                            shared.streamMap.TryGetValue(1u, out stream);
                        }

                        bool tookBufferOwnership;
                        err = stream.PushBuffer(
                            new ArraySegment<byte>(buf, 0, upgrade.Payload.Length),
                            true,
                            out tookBufferOwnership);
                        if (!tookBufferOwnership)
                        {
                            config.BufferPool.Return(buf);
                        }
                        if (err != null)
                        {
                            if (err.Value.StreamId == 0)
                                continueRead = false;
                            await HandleFrameProcessingError(err.Value);
                        }
                    }
                }

                while (continueRead)
                {
                    EnsureBuffer(PersistentBufferSize);
                    var err = await ReadOneFrame();
                    ReleaseBuffer(PersistentBufferSize);
                    if (err != null)
                    {
                        if (err.Value.StreamId == 0)
                        {
                            continueRead = false;
                        }
                        await HandleFrameProcessingError(err.Value);
                    }
                }
            }
            catch (Exception e)
            {

            }

            await writer.CloseNow();

            await writer.Done;

            Dictionary<uint, StreamImpl> activeStreams = null;
            lock (shared.Mutex)
            {

                activeStreams = shared.streamMap;
                shared.streamMap = null;


                shared.Closed = true;
            }
            foreach (var kvp in activeStreams)
            {
                await kvp.Value.Reset(ErrorCode.ConnectError, fromRemote: true);
            }

            PingState pingState = null;
            lock (shared.Mutex)
            {
                if (shared.PingState != null)
                {
                    pingState = shared.PingState;
                    shared.PingState = null;
                }
            }
            if (pingState != null)
            {
                var ex = new ConnectionClosedException();
                foreach (var kvp in pingState.PingMap)
                {
                    kvp.Value.SetException(ex);
                }
            }
            if (!remoteGoAwayTcs.Task.IsCompleted)
            {
                remoteGoAwayTcs.TrySetException(new EndOfStreamException());
            }

            if (receiveBuffer != null)
            {
                config.BufferPool.Return(receiveBuffer);
                receiveBuffer = null;
            }

            headerReader.Dispose();
        }

        private async Task HandleFrameProcessingError(
            Http2Error err)
        {
            if (err.StreamId == 0)
            {
                await InitiateGoAway(err.Code, true);
            }
            else
            {
                StreamImpl stream = null;
                lock (shared.Mutex)
                {
                    shared.streamMap.TryGetValue(err.StreamId, out stream);
                }

                if (stream != null)
                {
                    await stream.Reset(err.Code, false);
                }
                else
                {
                    var fh = new FrameHeader
                    {
                        StreamId = err.StreamId,
                        Type = FrameType.ResetStream,
                        Flags = 0,
                    };
                    var resetData = new ResetFrameData
                    {
                        ErrorCode = err.Code,
                    };
                    await writer.WriteResetStream(fh, resetData);
                }
            }
        }

        /// <summary>
        /// 立即强制关闭连接
        /// </summary>
        /// <returns></returns>
        public async Task CloseNow()
        {
            await writer.CloseNow();

            await readerDone;
        }

        /// <summary>
        /// 将带有给定错误代码的Goaway帧发送到远程。
        /// </summary>
        /// <param name="errorCode"></param>
        /// <param name="closeConnection"></param>
        /// <returns></returns>
        public async Task GoAwayAsync(ErrorCode errorCode, bool closeConnection = false)
        {
            await InitiateGoAway(errorCode, closeConnection);
            if (closeConnection)
            {
                await Done;
            }
        }

        /// <summary>
        /// 向远程服务器发送ping请求并返回任务。
        /// </summary>
        /// <returns></returns>
        public Task PingAsync()
        {
            return PingAsync(CancellationToken.None);
        }

        /// <summary>
        /// 向远程服务器发送ping请求并返回任务。
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task PingAsync(CancellationToken ct)
        {
            ulong pingId = 0;
            Task waitTask = null;
            lock (shared.Mutex)
            {
                if (shared.Closed)
                {
                    throw new ConnectionClosedException();
                }

                if (shared.PingState == null)
                {
                    shared.PingState = new PingState();
                }
                pingId = shared.PingState.Counter;
                var tcs = new TaskCompletionSource<bool>();
                shared.PingState.PingMap[pingId] = tcs;
                shared.PingState.Counter++;
                waitTask = tcs.Task;
            }

            if (ct != CancellationToken.None)
            {
                ct.Register(() =>
                {
                    lock (shared.Mutex)
                    {
                        if (shared.PingState == null)
                        {
                            return;
                        }

                        TaskCompletionSource<bool> tcs = null;
                        if (shared.PingState.PingMap.TryGetValue(pingId, out tcs))
                        {
                            shared.PingState.PingMap.Remove(pingId);
                            tcs.TrySetCanceled();
                        }
                    }
                }, false);
            }

            var fh = new FrameHeader
            {
                Type = FrameType.Ping,
                Flags = 0,
                StreamId = 0u,
                Length = 8,
            };

            var pingBuffer = BitConverter.GetBytes(pingId);
            var writePingResult = await writer.WritePing(
                fh, new ArraySegment<byte>(pingBuffer));
            await waitTask;
        }

        /// <summary>
        /// 如果连接耗尽，则返回true
        /// </summary>
        public bool IsExhausted
        {
            get
            {
                lock (shared.Mutex)
                {
                    if (shared.Closed)
                    {
                        return true;
                    }
                    return shared.LastOutgoingStreamId > int.MaxValue - 2;
                }
            }
        }

        /// <summary>
        /// 在连接顶部创建新流。
        /// </summary>
        /// <param name="headers"></param>
        /// <param name="endOfStream"></param>
        /// <returns></returns>
        public async Task<IStream> CreateStreamAsync(IEnumerable<HeaderField> headers, bool endOfStream = false)
        {
            if (config.IsServer) throw new NotSupportedException("只能为客户端创建流");

            var hvr = HeaderValidator.ValidateRequestHeaders(headers);
            if (hvr != HeaderValidationResult.Ok)
                throw new Exception(hvr.ToString());

            await clientState.CreateStreamMutex.WaitAsync();

            try
            {
                uint streamId = 0u;
                StreamImpl stream = null;
                lock (shared.Mutex)
                {
                    if (shared.Closed)
                    {
                        throw new ConnectionClosedException();
                    }

                    if (shared.LastOutgoingStreamId == 0)
                        shared.LastOutgoingStreamId = 1;
                    else if (shared.LastOutgoingStreamId <= int.MaxValue - 2)
                        shared.LastOutgoingStreamId += 2;
                    else
                        throw new ConnectionExhaustedException();

                    streamId = shared.LastOutgoingStreamId;

                    stream = new StreamImpl(
                        this, streamId,
                        StreamState.Idle,
                        (int)localSettings.InitialWindowSize);

                    shared.streamMap[streamId] = stream;
                }

                if (!writer.RegisterStream(streamId))
                {
                    throw new ConnectionClosedException();
                }

                try
                {
                    await stream.WriteValidatedHeadersAsync(headers, endOfStream);
                }
                catch (Exception)
                {
                    throw new ConnectionClosedException();
                }
                return stream;
            }
            finally
            {
                clientState.CreateStreamMutex.Release();
            }
        }

        private async Task InitiateGoAway(ErrorCode errorCode, bool closeWriter)
        {
            uint lastProcessedStreamId = 0u;
            bool goAwaySent = false;
            lock (shared.Mutex)
            {
                lastProcessedStreamId = shared.LastIncomingStreamId;
                goAwaySent = shared.GoAwaySent;
                shared.GoAwaySent = true;
            }

            if (goAwaySent)
            {
                if (closeWriter)
                {
                    await writer.CloseNow();
                }
            }
            else
            {
                var fh = new FrameHeader
                {
                    Type = FrameType.GoAway,
                    StreamId = 0,
                    Flags = 0,
                };

                var goAwayData = new GoAwayFrameData
                {
                    Reason = new GoAwayReason
                    {
                        LastStreamId = lastProcessedStreamId,
                        ErrorCode = errorCode,
                        DebugData = Constants.EmptyByteArray,
                    },
                };

                await writer.WriteGoAway(fh, goAwayData, closeWriter);
            }
        }

        private async ValueTask<Http2Error?> ReadOneFrame()
        {
            var fh = await FrameHeader.ReceiveAsync(inputStream, receiveBuffer);

            if (!settingsReceived)
            {
                if (fh.Type != FrameType.Settings || (fh.Flags & (byte)SettingsFrameFlags.Ack) != 0)
                {
                    return new Http2Error
                    {
                        StreamId = 0,
                        Code = ErrorCode.ProtocolError,
                        Message = "预期设置帧为第一帧",
                    };
                }
            }

            switch (fh.Type)
            {
                case FrameType.Settings:
                    return await HandleSettingsFrame(fh);
                case FrameType.Priority:
                    return await HandlePriorityFrame(fh);
                case FrameType.Ping:
                    return await HandlePingFrame(fh);
                case FrameType.WindowUpdate:
                    return await HandleWindowUpdateFrame(fh);
                case FrameType.PushPromise:
                    return await HandlePushPromiseFrame(fh);
                case FrameType.ResetStream:
                    return await HandleResetFrame(fh);
                case FrameType.GoAway:
                    return await HandleGoAwayFrame(fh);
                case FrameType.Continuation:
                    return new Http2Error
                    {
                        StreamId = 0,
                        Code = ErrorCode.ProtocolError,
                        Message = "意外的继续帧",
                    };
                case FrameType.Data:
                    return await HandleDataFrame(fh);
                case FrameType.Headers:
                    var headerRes = await headerReader.ReadHeaders(fh, GetBuffer);
                    if (headerRes.Error != null) return headerRes.Error;
                    return await HandleHeaders(headerRes.HeaderData);
                default:
                    return await HandleUnknownFrame(fh);
            }
        }

        private async ValueTask<Http2Error?> HandleHeaders(CompleteHeadersFrameData headers)
        {
            if (headers.StreamId == 0)
            {
                return new Http2Error
                {
                    StreamId = headers.StreamId,
                    Code = ErrorCode.ProtocolError,
                    Message = "Received HEADERS frame with stream ID 0",
                };
            }

            StreamImpl stream = null;
            uint lastOutgoingStream = 0u;
            uint lastIncomingStream = 0u;
            lock (shared.Mutex)
            {
                lastIncomingStream = shared.LastIncomingStreamId;
                lastOutgoingStream = shared.LastOutgoingStreamId;
                shared.streamMap.TryGetValue(headers.StreamId, out stream);
            }

            if (stream != null)
            {
                if (headers.Priority.HasValue)
                {
                    var handlePrioErr = HandlePriorityData(
                        headers.StreamId, headers.Priority.Value);
                    if (handlePrioErr != null)
                    {
                        return handlePrioErr;
                    }
                }
                return stream.ProcessHeaders(headers);
            }

            var isServerInitiated = headers.StreamId % 2 == 0;
            var isRemoteInitiated =
                (IsServer && !isServerInitiated) || (!IsServer && isServerInitiated);

            var isValidNewStream =
                IsServer &&
                isRemoteInitiated &&
                (headers.StreamId > lastIncomingStream);


            if (!isValidNewStream)
            {
                return new Http2Error
                {
                    StreamId = headers.StreamId,
                    Code = ErrorCode.StreamClosed,
                    Message = "拒绝不打开新流的头",
                };
            }

            lock (shared.Mutex)
            {
                shared.LastIncomingStreamId = headers.StreamId;

                if (shared.GoAwaySent)
                {
                    return new Http2Error
                    {
                        StreamId = headers.StreamId,
                        Code = ErrorCode.RefusedStream,
                        Message = "离开",
                    };
                }

                if ((uint)shared.streamMap.Count + 1 > localSettings.MaxConcurrentStreams)
                {
                    return new Http2Error
                    {
                        StreamId = headers.StreamId,
                        Code = ErrorCode.RefusedStream,
                        Message = "由于最大并发流而拒绝流",
                    };
                }
            }

            var newStream = new StreamImpl(
                this, headers.StreamId, StreamState.Idle,
                (int)localSettings.InitialWindowSize);

            lock (shared.Mutex)
            {
                shared.streamMap[headers.StreamId] = newStream;
            }

            if (!writer.RegisterStream(headers.StreamId))
            {
                return new Http2Error
                {
                    StreamId = 0,
                    Code = ErrorCode.InternalError,
                    Message = "Can't register stream at writer",
                };
            }

            var err = newStream.ProcessHeaders(headers);
            if (err != null)
            {
                return err;
            }

            if (headers.Priority.HasValue)
            {
                err = HandlePriorityData(
                    headers.StreamId,
                    headers.Priority.Value);
                if (err != null)
                {
                    return err;
                }
            }

            var handledByUser = config.StreamListener(newStream);
            if (!handledByUser)
            {
                await newStream.Reset(ErrorCode.RefusedStream, false);
            }

            return null;
        }

        private async ValueTask<Http2Error?> HandleDataFrame(FrameHeader fh)
        {
            if (fh.StreamId == 0)
            {
                return new Http2Error
                {
                    StreamId = 0,
                    Code = ErrorCode.ProtocolError,
                    Message = "接收到无效的数据帧头",
                };
            }
            if ((fh.Flags & (byte)DataFrameFlags.Padded) != 0 &&
                fh.Length < 1)
            {
                return new Http2Error
                {
                    StreamId = 0,
                    Code = ErrorCode.ProtocolError,
                    Message = "帧太小，无法包含填充",
                };
            }
            if (fh.Length > localSettings.MaxFrameSize)
            {
                return new Http2Error
                {
                    StreamId = 0,
                    Code = ErrorCode.FrameSizeError,
                    Message = "超过最大帧大小",
                };
            }

            var dataBuffer = config.BufferPool.Rent(fh.Length);
            try
            {
                await inputStream.ReadAll(
                    new ArraySegment<byte>(dataBuffer, 0, fh.Length));
            }
            catch (Exception)
            {
                config.BufferPool.Return(dataBuffer);
                throw;
            }

            var isPadded = (fh.Flags & (byte)DataFrameFlags.Padded) != 0;
            var padLen = 0;
            var offset = isPadded ? 1 : 0;
            var dataSize = fh.Length;
            if (isPadded)
            {
                padLen = dataBuffer[0];
                dataSize = fh.Length - 1 - padLen;
                if (dataSize < 0)
                {
                    config.BufferPool.Return(dataBuffer);
                    return new Http2Error
                    {
                        StreamId = 0,
                        Code = ErrorCode.ProtocolError,
                        Message = "减法填充后帧太小",
                    };
                }
            }

            if (dataSize != 0)
            {
                if (dataSize > connReceiveFlowWindow)
                {
                    config.BufferPool.Return(dataBuffer);
                    return new Http2Error
                    {
                        StreamId = 0,
                        Code = ErrorCode.FlowControlError,
                        Message = "超过接收窗口",
                    };
                }
                connReceiveFlowWindow -= dataSize;
            }

            StreamImpl stream = null;
            uint lastIncomingStreamId;
            uint lastOutgoingStreamId;
            lock (shared.Mutex)
            {
                lastIncomingStreamId = shared.LastIncomingStreamId;
                lastOutgoingStreamId = shared.LastOutgoingStreamId;
                shared.streamMap.TryGetValue(fh.StreamId, out stream);
            }

            Http2Error? processError = null;
            bool streamTookBufferOwnership = false;
            if (stream != null)
            {
                processError = stream.PushBuffer(
                    new ArraySegment<byte>(dataBuffer, offset, dataSize),
                    (fh.Flags & (byte)DataFrameFlags.EndOfStream) != 0,
                    out streamTookBufferOwnership);
            }
            else
            {
                var isIdleStreamId = IsIdleStreamId(
                    fh.StreamId, lastOutgoingStreamId, lastIncomingStreamId);
                processError = new Http2Error
                {
                    StreamId = isIdleStreamId ? 0u : fh.StreamId,
                    Code = ErrorCode.StreamClosed,
                    Message = "接收到未知帧的数据",
                };
            }

            if (!streamTookBufferOwnership)
            {
                config.BufferPool.Return(dataBuffer);
            }
            dataBuffer = null;

            if (processError.HasValue && processError.Value.StreamId == 0)
            {
                return processError;
            }

            var maxWindow = Constants.InitialConnectionWindowSize;
            var possibleWindowUpdate = maxWindow - connReceiveFlowWindow;
            var windowUpdateAmount = 0;
            if (possibleWindowUpdate >= (maxWindow / 2))
            {
                windowUpdateAmount = possibleWindowUpdate;
                connReceiveFlowWindow += windowUpdateAmount;
            }

            if (windowUpdateAmount > 0)
            {
                var wfh = new FrameHeader
                {
                    StreamId = 0,
                    Type = FrameType.WindowUpdate,
                    Flags = 0,
                };

                var updateData = new WindowUpdateData
                {
                    WindowSizeIncrement = windowUpdateAmount,
                };

                try
                {
                    await writer.WriteWindowUpdate(wfh, updateData);
                }
                catch (Exception)
                {

                }
            }

            return processError;
        }

        private ValueTask<Http2Error?> HandlePushPromiseFrame(FrameHeader fh)
        {
            return new ValueTask<Http2Error?>(
                new Http2Error
                {
                    StreamId = 0,
                    Code = ErrorCode.ProtocolError,
                    Message = "收到不支持的PUSH_PROMISE帧",
                });
        }


        private async ValueTask<Http2Error?> HandleUnknownFrame(FrameHeader fh)
        {
            if (fh.Length > localSettings.MaxFrameSize)
            {
                return new Http2Error
                {
                    StreamId = 0,
                    Code = ErrorCode.FrameSizeError,
                    Message = "超过最大帧大小",
                };
            }

            EnsureBuffer(fh.Length);
            await inputStream.ReadAll(
                new ArraySegment<byte>(receiveBuffer, 0, fh.Length));


            return null;
        }

        private async ValueTask<Http2Error?> HandleGoAwayFrame(FrameHeader fh)
        {
            if (fh.StreamId != 0 || fh.Length < 8)
            {
                return new Http2Error
                {
                    StreamId = 0,
                    Code = ErrorCode.ProtocolError,
                    Message = "接收到无效的goaway帧头",
                };
            }
            if (fh.Length > localSettings.MaxFrameSize)
            {
                return new Http2Error
                {
                    StreamId = 0,
                    Code = ErrorCode.FrameSizeError,
                    Message = "超过最大帧大小",
                };
            }

            EnsureBuffer(fh.Length);
            await inputStream.ReadAll(new ArraySegment<byte>(receiveBuffer, 0, fh.Length));

            var goAwayData = GoAwayFrameData.DecodeFrom(
                new ArraySegment<byte>(receiveBuffer, 0, fh.Length));

            if (!remoteGoAwayTcs.Task.IsCompleted)
            {
                var reason = goAwayData.Reason;
                var data = reason.DebugData;
                var debugData = new byte[data.Count];
                Array.Copy(data.Array, data.Offset, debugData, 0, data.Count);
                reason.DebugData = new ArraySegment<byte>(debugData);

                remoteGoAwayTcs.TrySetResult(reason);
            }

            return null;
        }

        private async ValueTask<Http2Error?> HandleResetFrame(FrameHeader fh)
        {
            if (fh.StreamId == 0 || fh.Length != ResetFrameData.Size)
            {
                var errc = ErrorCode.ProtocolError;
                if (fh.Length != ResetFrameData.Size) errc = ErrorCode.FrameSizeError;
                return new Http2Error
                {
                    StreamId = 0,
                    Code = errc,
                    Message = "接收到无效的RST_STREAM帧头",
                };
            }

            await inputStream.ReadAll(
                new ArraySegment<byte>(receiveBuffer, 0, ResetFrameData.Size));

            var resetData = ResetFrameData.DecodeFrom(
                new ArraySegment<byte>(receiveBuffer, 0, ResetFrameData.Size));

            StreamImpl stream = null;
            uint lastOutgoingStream = 0u;
            uint lastIncomingStream = 0u;
            lock (shared.Mutex)
            {
                lastIncomingStream = shared.LastIncomingStreamId;
                lastOutgoingStream = shared.LastOutgoingStreamId;
                shared.streamMap.TryGetValue(fh.StreamId, out stream);
                if (stream != null)
                {
                    shared.streamMap.Remove(fh.StreamId);
                }
            }

            if (stream != null)
            {
                await stream.Reset(resetData.ErrorCode, true);
            }
            else
            {
                if (IsIdleStreamId(fh.StreamId, lastOutgoingStream, lastIncomingStream))
                {
                    return new Http2Error
                    {
                        StreamId = 0u,
                        Code = ErrorCode.ProtocolError,
                        Message = "接收到空闲流的RST_STREAM",
                    };
                }
            }

            return null;
        }

        private bool IsIdleStreamId(
            uint streamId, uint lastOutgoingStreamId, uint lastIncomingStreamId)
        {
            var isServerInitiated = streamId % 2 == 0;
            var isRemoteInitiated =
                (IsServer && !isServerInitiated) ||
                (!IsServer && isServerInitiated);
            var isIdle = (isRemoteInitiated && streamId > lastIncomingStreamId ||
                          !isRemoteInitiated && streamId > lastOutgoingStreamId);
            return isIdle;
        }

        private async ValueTask<Http2Error?> HandleWindowUpdateFrame(FrameHeader fh)
        {
            if (fh.Length != WindowUpdateData.Size)
            {
                return new Http2Error
                {
                    StreamId = 0,
                    Code = ErrorCode.FrameSizeError,
                    Message = "收到无效的窗口更新帧头",
                };
            }

            await inputStream.ReadAll(
                new ArraySegment<byte>(receiveBuffer, 0, WindowUpdateData.Size));

            var windowUpdateData = WindowUpdateData.DecodeFrom(
                new ArraySegment<byte>(receiveBuffer, 0, WindowUpdateData.Size));

            bool isIdleStream = false;
            lock (shared.Mutex)
            {
                isIdleStream = IsIdleStreamId(
                    fh.StreamId, shared.LastOutgoingStreamId, shared.LastIncomingStreamId);
            }

            if (isIdleStream)
            {
                return new Http2Error
                {
                    StreamId = 0u,
                    Code = ErrorCode.ProtocolError,
                    Message = "接收到空闲流的窗口更新",
                };
            }

            return writer.UpdateFlowControlWindow(
                fh.StreamId, windowUpdateData.WindowSizeIncrement);
        }

        private async ValueTask<Http2Error?> HandlePingFrame(FrameHeader fh)
        {
            if (fh.StreamId != 0 || fh.Length != 8)
            {
                var errc = ErrorCode.ProtocolError;
                if (fh.Length != 8) errc = ErrorCode.FrameSizeError;
                return new Http2Error
                {
                    StreamId = 0,
                    Code = errc,
                    Message = "接收到无效的ping帧头",
                };
            }

            await inputStream.ReadAll(new ArraySegment<byte>(receiveBuffer, 0, 8));

            var hasAck = (fh.Flags & (byte)PingFrameFlags.Ack) != 0;
            if (hasAck)
            {
                TaskCompletionSource<bool> tcs = null;
                lock (shared.Mutex)
                {
                    if (shared.PingState != null)
                    {
                        var id = BitConverter.ToUInt64(receiveBuffer, 0);
                        if (shared.PingState.PingMap.TryGetValue(id, out tcs))
                        {
                            shared.PingState.PingMap.Remove(id);
                        }
                    }
                }
                if (tcs != null)
                {
                    tcs.SetResult(true);
                }
            }
            else
            {
                var pongHeader = fh;
                pongHeader.Flags = (byte)PingFrameFlags.Ack;
                await writer.WritePing(
                    pongHeader, new ArraySegment<byte>(receiveBuffer, 0, 8));
            }

            return null;
        }

        private async ValueTask<Http2Error?> HandlePriorityFrame(FrameHeader fh)
        {
            if (fh.StreamId == 0 || fh.Length != PriorityData.Size)
            {
                var errc = ErrorCode.ProtocolError;
                if (fh.Length != PriorityData.Size) errc = ErrorCode.FrameSizeError;
                return new Http2Error
                {
                    StreamId = 0,
                    Code = errc,
                    Message = "接收到无效的优先级帧头",
                };
            }

            await inputStream.ReadAll(
                new ArraySegment<byte>(receiveBuffer, 0, PriorityData.Size));

            var prioData = PriorityData.DecodeFrom(
                new ArraySegment<byte>(receiveBuffer, 0, PriorityData.Size));

            return HandlePriorityData(fh.StreamId, prioData);
        }

        private Http2Error? HandlePriorityData(
            uint streamId, PriorityData data)
        {
            if (streamId == data.StreamDependency)
            {
                return new Http2Error
                {
                    Code = ErrorCode.ProtocolError,
                    StreamId = streamId,
                    Message = "优先级错误：流不能依赖于自身",
                };
            }

            return null;
        }

        private async ValueTask<Http2Error?> HandleSettingsFrame(FrameHeader fh)
        {
            if (fh.StreamId != 0)
            {
                return new Http2Error
                {
                    StreamId = 0,
                    Code = ErrorCode.ProtocolError,
                    Message = "接收到流ID无效的设置帧",
                };
            }
            bool isAck = (fh.Flags & (byte)SettingsFrameFlags.Ack) != 0;

            if (isAck)
            {
                if (fh.Length != 0)
                {
                    return new Http2Error
                    {
                        StreamId = 0,
                        Code = ErrorCode.ProtocolError,
                        Message = "接收到非零长度的设置确认",
                    };
                }
                nrUnackedSettings--;
                if (nrUnackedSettings < 0)
                {
                    return new Http2Error
                    {
                        StreamId = 0,
                        Code = ErrorCode.ProtocolError,
                        Message = "收到意外设置确认",
                    };
                }
            }
            else
            {
                if (fh.Length > localSettings.MaxFrameSize)
                {
                    return new Http2Error
                    {
                        StreamId = 0,
                        Code = ErrorCode.FrameSizeError,
                        Message = "超过最大帧大小",
                    };
                }
                if (fh.Length % 6 != 0)
                {
                    return new Http2Error
                    {
                        StreamId = 0,
                        Code = ErrorCode.ProtocolError,
                        Message = "设置帧长度无效",
                    };
                }

                EnsureBuffer(fh.Length);
                await inputStream.ReadAll(
                    new ArraySegment<byte>(receiveBuffer, 0, fh.Length));

                var err = remoteSettings.UpdateFromData(
                    new ArraySegment<byte>(receiveBuffer, 0, fh.Length));
                if (err != null)
                {
                    return err;
                }

                settingsReceived = true;

                err = await writer.ApplyAndAckRemoteSettings(remoteSettings);
                if (err != null)
                {
                    return err;
                }
            }

            return null;
        }

        /// <summary>
        /// 要注销的流
        /// </summary>
        /// <param name="stream"></param>
        internal void UnregisterStream(StreamImpl stream)
        {
            lock (shared.Mutex)
            {
                if (shared.streamMap != null)
                {
                    shared.streamMap.Remove(stream.Id);
                }
            }
        }
    }
}
