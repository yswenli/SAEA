/****************************************************************************
*项目名称：SAEA.Http2.Core
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Core
*类 名 称：StreamImpl
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 16:26:21
*描述：
*=====================================================================
*修改时间：2019/6/27 16:26:21
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Http2.Events;
using SAEA.Http2.Extentions;
using SAEA.Http2.Interfaces;
using SAEA.Http2.Model;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Http2.Core
{
    /// <summary>
    /// HTTP/2流的实现
    /// </summary>
    internal class StreamImpl : IStream
    {

        public uint Id { get; }

        public StreamState State
        {
            get
            {
                lock (stateMutex)
                {
                    return this.state;
                }
            }
        }

        private readonly Connection connection;
        private StreamState state;
        private readonly object stateMutex = new object();


        private SemaphoreSlim writeMutex = new SemaphoreSlim(1);
        private bool headersSent = false;
        private bool dataSent = false;

        private enum HeaderReceptionState : byte
        {
            ReceivedNoHeaders,
            ReceivedInformationalHeaders,
            ReceivedAllHeaders,
        }
        private HeaderReceptionState headersReceived =
            HeaderReceptionState.ReceivedNoHeaders;

        private bool dataReceived = false;


        private List<HeaderField> inHeaders;
        private List<HeaderField> inTrailers;


        private readonly AsyncManualResetEvent readDataPossible =
            new AsyncManualResetEvent(false);
        private readonly AsyncManualResetEvent readHeadersPossible =
            new AsyncManualResetEvent(false);
        private readonly AsyncManualResetEvent readTrailersPossible =
            new AsyncManualResetEvent(false);

        private long declaredInContentLength = -1;
        private long totalInData = 0;

        private int totalReceiveWindow;
        private int receiveWindow;


        private class ReceiveQueueItem
        {
            public byte[] Buffer;
            public int Offset;
            public int Count;
            public ReceiveQueueItem Next;

            public ReceiveQueueItem(ArraySegment<byte> segment)
            {
                this.Buffer = segment.Array;
                this.Offset = segment.Offset;
                this.Count = segment.Count;
            }
        }

        private ReceiveQueueItem receiveQueueHead = null;

        private int ReceiveQueueLength
        {
            get
            {
                int len = 0;
                var item = receiveQueueHead;
                while (item != null)
                {
                    len += item.Count;
                    item = item.Next;
                }
                return len;
            }
        }


        private static readonly HeaderField[] EmptyHeaders = new HeaderField[0];


        public StreamImpl(
            Connection connection,
            uint streamId,
            StreamState state,
            int receiveWindow)
        {
            this.connection = connection;
            this.Id = streamId;


            this.state = state;
            this.receiveWindow = receiveWindow;
            this.totalReceiveWindow = receiveWindow;
        }

        private async Task SendHeaders(
            IEnumerable<HeaderField> headers, bool endOfStream)
        {
            var fhh = new FrameHeader
            {
                StreamId = this.Id,
                Type = FrameType.Headers,
                Flags = endOfStream ? (byte)HeadersFrameFlags.EndOfStream : (byte)0,
            };
            var res = await connection.writer.WriteHeaders(fhh, headers);
            if (res != ConnectionWriter.WriteResult.Success)
            {
                throw new Exception("无法写入流");
            }
        }

        public Task WriteHeadersAsync(
            IEnumerable<HeaderField> headers, bool endOfStream)
        {
            HeaderValidationResult hvr;

            if (connection.IsServer) hvr = HeaderValidator.ValidateResponseHeaders(headers);
            else hvr = HeaderValidator.ValidateRequestHeaders(headers);
            if (hvr != HeaderValidationResult.Ok)
            {
                throw new Exception(hvr.ToString());
            }

            return WriteValidatedHeadersAsync(headers, endOfStream);
        }

        internal async Task WriteValidatedHeadersAsync(
            IEnumerable<HeaderField> headers, bool endOfStream = false)
        {
            var removeStream = false;

            await writeMutex.WaitAsync();
            try
            {
                lock (stateMutex)
                {
                    if (dataSent)
                    {
                        throw new Exception("试图在数据后写入头");
                    }

                    if (headersSent)
                    {
                        //todo 多头
                    }

                    headersSent = true;
                    switch (state)
                    {
                        case StreamState.Idle:
                            state = StreamState.Open;
                            break;
                        case StreamState.ReservedLocal:
                            state = StreamState.HalfClosedRemote;
                            break;
                        case StreamState.Reset:
                            throw new StreamResetException();
                    }

                    if (state == StreamState.Open && endOfStream)
                    {
                        state = StreamState.HalfClosedLocal;
                    }
                    else if (state == StreamState.HalfClosedRemote && endOfStream)
                    {
                        state = StreamState.Closed;
                        removeStream = true;
                    }
                }

                await SendHeaders(headers, endOfStream); // TODO: Use result
            }
            finally
            {
                writeMutex.Release();
                if (removeStream)
                {
                    connection.UnregisterStream(this);
                }
            }
        }

        public async Task WriteTrailersAsync(IEnumerable<HeaderField> headers)
        {
            HeaderValidationResult hvr = HeaderValidator.ValidateTrailingHeaders(headers);

            if (hvr != HeaderValidationResult.Ok)
            {
                throw new Exception(hvr.ToString());
            }

            var removeStream = false;

            await writeMutex.WaitAsync();
            try
            {
                lock (stateMutex)
                {
                    if (!dataSent)
                    {
                        throw new Exception("试图在没有数据的情况下写入");
                    }

                    switch (state)
                    {
                        case StreamState.Open:
                            state = StreamState.HalfClosedLocal;
                            break;
                        case StreamState.HalfClosedRemote:
                            state = StreamState.HalfClosedRemote;
                            state = StreamState.Closed;
                            removeStream = true;
                            break;
                        case StreamState.Idle:
                        case StreamState.ReservedRemote:
                        case StreamState.HalfClosedLocal:
                        case StreamState.Closed:
                            throw new Exception("发送的状态无效");
                        case StreamState.Reset:
                            throw new StreamResetException();
                        case StreamState.ReservedLocal:

                            throw new Exception("意外状态：发送数据后保留本地");
                    }
                }

                await SendHeaders(headers, true); // TODO: Use result
            }
            finally
            {
                writeMutex.Release();
                if (removeStream)
                {
                    connection.UnregisterStream(this);
                }
            }
        }

        public void Cancel()
        {
            var writeResetTask = Reset(ErrorCode.Cancel, false);
        }

        public void Dispose()
        {
            Cancel();
        }

        internal ValueTask<ConnectionWriter.WriteResult> Reset(
            ErrorCode errorCode, bool fromRemote)
        {
            ValueTask<ConnectionWriter.WriteResult> writeResetTask =
                new ValueTask<ConnectionWriter.WriteResult>(
                    ConnectionWriter.WriteResult.Success);

            lock (stateMutex)
            {
                if (state == StreamState.Reset || state == StreamState.Closed)
                {
                    return writeResetTask;
                }
                state = StreamState.Reset;
                var head = receiveQueueHead;
                receiveQueueHead = null;
                FreeReceiveQueue(head);
            }

            if (!fromRemote)
            {
                var fh = new FrameHeader
                {
                    StreamId = this.Id,
                    Type = FrameType.ResetStream,
                    Flags = 0,
                };
                var resetData = new ResetFrameData
                {
                    ErrorCode = errorCode
                };
                writeResetTask = connection.writer.WriteResetStream(fh, resetData);
            }
            else
            {
                connection.writer.RemoveStream(this.Id);
            }

            if (!fromRemote)
            {
                this.connection.UnregisterStream(this);
            }

            readDataPossible.Set();
            readTrailersPossible.Set();
            readHeadersPossible.Set();

            return writeResetTask;
        }

        public async ValueTask<StreamReadResult> ReadAsync(ArraySegment<byte> buffer)
        {
            while (true)
            {
                await readDataPossible;

                int windowUpdateAmount = 0;
                StreamReadResult result = new StreamReadResult();
                bool hasResult = false;

                lock (stateMutex)
                {
                    if (state == StreamState.Reset)
                    {
                        throw new StreamResetException();
                    }

                    var streamClosedFromRemote =
                        state == StreamState.Closed || state == StreamState.HalfClosedRemote;

                    if (receiveQueueHead != null)
                    {
                        var offset = buffer.Offset;
                        var count = buffer.Count;
                        while (receiveQueueHead != null && count > 0)
                        {
                            var toCopy = Math.Min(receiveQueueHead.Count, count);
                            Array.Copy(
                                receiveQueueHead.Buffer, receiveQueueHead.Offset,
                                buffer.Array, offset,
                                toCopy);
                            offset += toCopy;
                            count -= toCopy;

                            if (toCopy == receiveQueueHead.Count)
                            {
                                connection.config.BufferPool.Return(
                                    receiveQueueHead.Buffer);
                                receiveQueueHead = receiveQueueHead.Next;
                            }
                            else
                            {
                                receiveQueueHead.Offset += toCopy;
                                receiveQueueHead.Count -= toCopy;
                                break;
                            }
                        }
                        if (!streamClosedFromRemote)
                        {
                            var isFree = totalReceiveWindow - ReceiveQueueLength;
                            var possibleWindowUpdate = isFree - receiveWindow;
                            if (possibleWindowUpdate >= (totalReceiveWindow / 2))
                            {
                                windowUpdateAmount = possibleWindowUpdate;
                                receiveWindow += windowUpdateAmount;
                            }
                        }

                        result = new StreamReadResult
                        {
                            BytesRead = offset - buffer.Offset,
                            EndOfStream = false,
                        };
                        hasResult = true;

                        if (receiveQueueHead == null && !streamClosedFromRemote)
                        {
                            readDataPossible.Reset();
                        }
                    }
                    else if (streamClosedFromRemote)
                    {
                        result = new StreamReadResult
                        {
                            BytesRead = 0,
                            EndOfStream = true,
                        };
                        hasResult = true;
                    }
                }

                if (hasResult)
                {
                    if (windowUpdateAmount > 0)
                    {
                        await SendWindowUpdate(windowUpdateAmount);
                    }
                    return result;
                }
            }
        }


        private async ValueTask<object> SendWindowUpdate(int amount)
        {
            var fh = new FrameHeader
            {
                StreamId = this.Id,
                Type = FrameType.WindowUpdate,
                Flags = 0,
            };

            var updateData = new WindowUpdateData
            {
                WindowSizeIncrement = amount,
            };

            try
            {
                await this.connection.writer.WriteWindowUpdate(fh, updateData);
            }
            catch (Exception)
            {

            }

            return null;
        }

        public Task WriteAsync(ArraySegment<byte> buffer)
        {
            return WriteAsync(buffer, false);
        }

        public async Task WriteAsync(ArraySegment<byte> buffer, bool endOfStream = false)
        {
            var removeStream = false;

            await writeMutex.WaitAsync();
            try
            {
                lock (stateMutex)
                {
                    if (state == StreamState.Reset)
                    {
                        throw new StreamResetException();
                    }
                    else if (state != StreamState.Open && state != StreamState.HalfClosedRemote)
                    {
                        throw new Exception("尝试以无效的流状态写入数据");
                    }
                    else if (state == StreamState.Open && endOfStream)
                    {
                        state = StreamState.HalfClosedLocal;
                    }
                    else if (state == StreamState.HalfClosedRemote && endOfStream)
                    {
                        state = StreamState.Closed;
                        removeStream = true;
                    }

                    if (!this.headersSent)
                    {
                        throw new Exception("试图在头之前写入数据");
                    }

                    dataSent = true;
                }


                var fh = new FrameHeader
                {
                    StreamId = this.Id,
                    Type = FrameType.Data,
                    Flags = endOfStream ? ((byte)DataFrameFlags.EndOfStream) : (byte)0,
                };

                var res = await connection.writer.WriteData(fh, buffer);
                if (res == ConnectionWriter.WriteResult.StreamResetError)
                {
                    throw new StreamResetException();
                }
                else if (res != ConnectionWriter.WriteResult.Success)
                {
                    throw new Exception("无法写入流"); // TODO: Improve me
                }
            }
            finally
            {
                writeMutex.Release();
                if (removeStream)
                {
                    connection.UnregisterStream(this);
                }
            }
        }

        public Task CloseAsync()
        {
            return this.WriteAsync(Constants.EmptyByteArray, true);
        }

        public async Task<IEnumerable<HeaderField>> ReadHeadersAsync()
        {
            await readHeadersPossible;
            IEnumerable<HeaderField> result = null;
            lock (stateMutex)
            {
                if (state == StreamState.Reset)
                {
                    throw new StreamResetException();
                }
                if (inHeaders != null)
                {
                    result = inHeaders;

                    if (result.IsInformationalHeaders())
                    {
                        inHeaders = null;
                        readHeadersPossible.Reset();
                    }
                }
                else result = EmptyHeaders;
            }
            return result;
        }

        public async Task<IEnumerable<HeaderField>> ReadTrailersAsync()
        {
            await readTrailersPossible;
            IEnumerable<HeaderField> result = null;
            lock (stateMutex)
            {
                if (state == StreamState.Reset)
                {
                    throw new StreamResetException();
                }
                if (inTrailers != null) result = inTrailers;
                else result = EmptyHeaders;
            }
            return result;
        }

        /// <summary>
        /// 处理传入头的接收
        /// </summary>
        public Http2Error? ProcessHeaders(
            CompleteHeadersFrameData headers)
        {
            var wakeupDataWaiter = false;
            var wakeupHeaderWaiter = false;
            var wakeupTrailerWaiter = false;
            var removeStream = false;

            lock (stateMutex)
            {
                switch (state)
                {
                    case StreamState.ReservedLocal:
                    case StreamState.ReservedRemote:
                        return new Http2Error
                        {
                            StreamId = Id,
                            Code = ErrorCode.InternalError,
                            Message = "接收到的头帧处于未覆盖的推送约定状态",
                        };
                    case StreamState.Idle:
                    case StreamState.Open:
                    case StreamState.HalfClosedLocal:
                        if (headersReceived != HeaderReceptionState.ReceivedAllHeaders)
                        {
                            HeaderValidationResult hvr;
                            if (connection.IsServer)
                            {
                                hvr = HeaderValidator.ValidateRequestHeaders(headers.Headers);
                            }
                            else
                            {
                                hvr = HeaderValidator.ValidateResponseHeaders(headers.Headers);
                            }
                            if (hvr != HeaderValidationResult.Ok)
                            {
                                return new Http2Error
                                {
                                    StreamId = Id,
                                    Code = ErrorCode.ProtocolError,
                                    Message = "Received invalid headers",
                                };
                            }

                            if (!connection.config.IsServer &&
                                headers.Headers.IsInformationalHeaders())
                            {
                                headersReceived =
                                    HeaderReceptionState.ReceivedInformationalHeaders;
                            }
                            else
                            {
                                headersReceived =
                                    HeaderReceptionState.ReceivedAllHeaders;
                            }
                            wakeupHeaderWaiter = true;
                            declaredInContentLength = headers.Headers.GetContentLength();
                            inHeaders = headers.Headers;
                        }
                        else if (!dataReceived)
                        {
                            return new Http2Error
                            {
                                StreamId = Id,
                                Code = ErrorCode.ProtocolError,
                                Message = "接收的无标题",
                            };
                        }
                        else
                        {
                            if (!headers.EndOfStream)
                            {
                                return new Http2Error
                                {
                                    StreamId = Id,
                                    Code = ErrorCode.ProtocolError,
                                    Message = "接收到没有endofstream标志",
                                };
                            }
                            var hvr = HeaderValidator.ValidateTrailingHeaders(headers.Headers);
                            if (hvr != HeaderValidationResult.Ok)
                            {
                                return new Http2Error
                                {
                                    StreamId = Id,
                                    Code = ErrorCode.ProtocolError,
                                    Message = "接收到无效",
                                };
                            }


                            if (declaredInContentLength >= 0 &&
                                declaredInContentLength != totalInData)
                            {
                                return new Http2Error
                                {
                                    StreamId = Id,
                                    Code = ErrorCode.ProtocolError,
                                    Message =
                                        "数据帧的长度与内容长度不匹配",
                                };
                            }

                            wakeupTrailerWaiter = true;
                            inTrailers = headers.Headers;
                        }

                        if (state == StreamState.Idle)
                        {
                            state = StreamState.Open;
                        }
                        if (headers.EndOfStream)
                        {
                            if (state == StreamState.HalfClosedLocal)
                            {
                                state = StreamState.Closed;
                                removeStream = true;
                            }
                            else
                            {
                                state = StreamState.HalfClosedRemote;
                            }
                            wakeupTrailerWaiter = true;
                            wakeupDataWaiter = true;
                        }
                        break;
                    case StreamState.HalfClosedRemote:
                    case StreamState.Closed:
                        return new Http2Error
                        {
                            Code = ErrorCode.StreamClosed,
                            StreamId = Id,
                            Message = "已接收封闭流的头",
                        };
                    case StreamState.Reset:
                        break;
                    default:
                        throw new Exception("未处理的流状态");
                }
            }

            if (wakeupHeaderWaiter)
            {
                readHeadersPossible.Set();
            }
            if (wakeupDataWaiter)
            {
                readDataPossible.Set();
            }
            if (wakeupTrailerWaiter)
            {
                readTrailersPossible.Set();
            }

            if (removeStream)
            {
                connection.UnregisterStream(this);
            }

            return null;
        }

        /// <summary>
        /// 处理数据帧的接收。
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="endOfStream"></param>
        /// <param name="tookBufferOwnership"></param>
        /// <returns></returns>
        public Http2Error? PushBuffer(
            ArraySegment<byte> buffer,
            bool endOfStream,
            out bool tookBufferOwnership)
        {
            tookBufferOwnership = false;
            var wakeupDataWaiter = false;
            var wakeupTrailerWaiter = false;
            var removeStream = false;

            lock (stateMutex)
            {
                switch (state)
                {
                    case StreamState.ReservedLocal:
                    case StreamState.ReservedRemote:
                        throw new NotImplementedException();
                    case StreamState.Open:
                    case StreamState.HalfClosedLocal:
                        if (headersReceived != HeaderReceptionState.ReceivedAllHeaders)
                        {
                            return new Http2Error
                            {
                                StreamId = Id,
                                Code = ErrorCode.ProtocolError,
                                Message = "在所有头之前接收到数据",
                            };
                        }

                        if (buffer.Count > 0)
                        {
                            if (buffer.Count > receiveWindow)
                            {
                                return new Http2Error
                                {
                                    StreamId = Id,
                                    Code = ErrorCode.FlowControlError,
                                    Message = "超过接收窗口",
                                };
                            }
                            receiveWindow -= buffer.Count;
                            var newItem = new ReceiveQueueItem(buffer);
                            EnqueueReceiveQueueItem(newItem);
                            wakeupDataWaiter = true;
                            tookBufferOwnership = true;
                        }

                        dataReceived = true;


                        totalInData += buffer.Count;
                        if (endOfStream &&
                            declaredInContentLength >= 0 &&
                            declaredInContentLength != totalInData)
                        {
                            return new Http2Error
                            {
                                StreamId = Id,
                                Code = ErrorCode.ProtocolError,
                                Message =
                                    "数据帧的长度与内容长度不匹配",
                            };
                        }

                        if (endOfStream)
                        {
                            if (state == StreamState.HalfClosedLocal)
                            {
                                state = StreamState.Closed;
                                removeStream = true;
                            }
                            else
                            {
                                state = StreamState.HalfClosedRemote;
                            }
                            wakeupTrailerWaiter = true;
                            wakeupDataWaiter = true;
                        }
                        break;
                    case StreamState.Idle:
                    case StreamState.HalfClosedRemote:
                    case StreamState.Closed:

                        return new Http2Error
                        {
                            StreamId = Id,
                            Code = ErrorCode.StreamClosed,
                            Message = "已接收封闭流的数据",
                        };
                    case StreamState.Reset:

                        break;
                    default:
                        throw new Exception("未处理的流状态");
                }
            }


            if (wakeupDataWaiter)
            {
                readDataPossible.Set();
            }
            if (wakeupTrailerWaiter)
            {
                readTrailersPossible.Set();
            }

            if (removeStream)
            {
                connection.UnregisterStream(this);
            }

            return null;
        }

        private void EnqueueReceiveQueueItem(ReceiveQueueItem newItem)
        {
            if (receiveQueueHead == null)
            {
                receiveQueueHead = newItem;
            }
            else
            {
                var current = receiveQueueHead;
                var next = receiveQueueHead.Next;
                while (next != null)
                {
                    current = next;
                    next = current.Next;
                }
                current.Next = newItem;
            }
        }

        private void FreeReceiveQueue(ReceiveQueueItem item)
        {
            while (item != null && item.Buffer != null)
            {
                connection.config.BufferPool.Return(item.Buffer);
                item.Buffer = null;
                var current = item;
                item = item.Next;
                current.Next = null;
            }
        }
    }
}
