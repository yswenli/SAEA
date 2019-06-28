/****************************************************************************
*项目名称：SAEA.Http2.Core
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Core
*类 名 称：HeaderReader
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/28 10:39:17
*描述：
*=====================================================================
*修改时间：2019/6/28 10:39:17
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Http2.Extentions;
using SAEA.Http2.Interfaces;
using SAEA.Http2.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static SAEA.Http2.Extentions.DecoderExtensions;

namespace SAEA.Http2.Core
{
    /// <summary>
    /// 读取并解码一系列的头和连续帧到完整的解码头列表中。
    /// </summary>
    internal class HeaderReader : IDisposable
    {
        /// <summary>
        /// 存储readheaders操作的结果
        /// </summary>
        public struct Result
        {
            public Http2Error? Error;
            public CompleteHeadersFrameData HeaderData;
        }

        uint maxFrameSize;
        uint maxHeaderFieldsSize;
        HPack.Decoder hpackDecoder;
        IReadableByteStream reader;

        public HeaderReader(
            HPack.Decoder hpackDecoder,
            uint maxFrameSize, uint maxHeaderFieldsSize,
            IReadableByteStream reader
        )
        {
            this.reader = reader;
            this.hpackDecoder = hpackDecoder;
            this.maxFrameSize = maxFrameSize;
            this.maxHeaderFieldsSize = maxHeaderFieldsSize;
        }

        public void Dispose()
        {
            hpackDecoder.Dispose();
        }


        private static Http2Error? DecodeResultToError(DecodeFragmentResult res)
        {
            if (res.Status != DecodeStatus.Success)
            {
                var errc =
                    (res.Status == DecodeStatus.MaxHeaderListSizeExceeded)
                    ? ErrorCode.ProtocolError
                    : ErrorCode.CompressionError;
                return new Http2Error
                {
                    StreamId = 0,
                    Code = errc,
                    Message = res.Status.ToString(),
                };
            }
            return null;
        }

        /// <summary>
        /// 读取和解码包含单个头段帧和0个或多个连续帧的头段块。
        /// </summary>
        /// <param name="firstHeader"></param>
        /// <param name="ensureBuffer"></param>
        /// <returns></returns>
        public async ValueTask<Result> ReadHeaders(
            FrameHeader firstHeader,
            Func<int, byte[]> ensureBuffer)
        {
            if (firstHeader.Length > maxFrameSize)
            {
                return new Result
                {
                    Error = new Http2Error
                    {
                        StreamId = 0,
                        Code = ErrorCode.FrameSizeError,
                        Message = "超过最大帧大小",
                    },
                };
            }

            PriorityData? prioData = null;
            var allowedHeadersSize = maxHeaderFieldsSize;
            var headers = new List<HeaderField>();
            var initialFlags = firstHeader.Flags;

            var f = (HeadersFrameFlags)firstHeader.Flags;
            var isEndOfStream = f.HasFlag(HeadersFrameFlags.EndOfStream);
            var isEndOfHeaders = f.HasFlag(HeadersFrameFlags.EndOfHeaders);
            var isPadded = f.HasFlag(HeadersFrameFlags.Padded);
            var hasPriority = f.HasFlag(HeadersFrameFlags.Priority);


            var minLength = 0;
            if (isPadded) minLength += 1;
            if (hasPriority) minLength += 5;
            if (firstHeader.Length < minLength)
            {
                return new Result
                {
                    Error = new Http2Error
                    {
                        StreamId = 0,
                        Code = ErrorCode.ProtocolError,
                        Message = "帧内容大小无效",
                    },
                };
            }

            byte[] buffer = ensureBuffer(firstHeader.Length);

            await reader.ReadAll(new ArraySegment<byte>(buffer, 0, firstHeader.Length));

            var offset = 0;
            var padLen = 0;

            if (isPadded)
            {
                padLen = buffer[0];
                offset++;
            }

            if (hasPriority)
            {
                prioData = PriorityData.DecodeFrom(
                    new ArraySegment<byte>(buffer, offset, 5));
                offset += 5;
            }

            var contentLen = firstHeader.Length - offset - padLen;
            if (contentLen < 0)
            {
                return new Result
                {
                    Error = new Http2Error
                    {
                        StreamId = 0,
                        Code = ErrorCode.ProtocolError,
                        Message = "帧内容大小无效",
                    },
                };
            }


            hpackDecoder.AllowTableSizeUpdates = true;

            var decodeResult = hpackDecoder.DecodeHeaderBlockFragment(
                new ArraySegment<byte>(buffer, offset, contentLen),
                allowedHeadersSize,
                headers);

            var err = DecodeResultToError(decodeResult);
            if (err != null)
            {
                return new Result { Error = err };
            }

            allowedHeadersSize -= decodeResult.HeaderFieldsSize;

            while (!isEndOfHeaders)
            {

                var contHeader = await FrameHeader.ReceiveAsync(reader, buffer);

                if (contHeader.Type != FrameType.Continuation
                    || contHeader.StreamId != firstHeader.StreamId
                    || contHeader.Length > maxFrameSize
                    || contHeader.Length == 0)
                {
                    return new Result
                    {
                        Error = new Http2Error
                        {
                            StreamId = 0,
                            Code = ErrorCode.ProtocolError,
                            Message = "延续帧无效",
                        },
                    };
                }

                var contFlags = ((ContinuationFrameFlags)contHeader.Flags);
                isEndOfHeaders = contFlags.HasFlag(ContinuationFrameFlags.EndOfHeaders);


                buffer = ensureBuffer(contHeader.Length);
                await reader.ReadAll(new ArraySegment<byte>(buffer, 0, contHeader.Length));

                offset = 0;
                contentLen = contHeader.Length;


                decodeResult = hpackDecoder.DecodeHeaderBlockFragment(
                    new ArraySegment<byte>(buffer, offset, contentLen),
                    allowedHeadersSize,
                    headers);

                var err2 = DecodeResultToError(decodeResult);
                if (err2 != null)
                {
                    return new Result { Error = err2 };
                }

                allowedHeadersSize -= decodeResult.HeaderFieldsSize;
            }


            if (!hpackDecoder.HasInitialState)
            {
                return new Result
                {
                    Error = new Http2Error
                    {
                        Code = ErrorCode.CompressionError,
                        StreamId = 0u,
                        Message = "接收到不完整的头块",
                    },
                };
            }

            return new Result
            {
                Error = null,
                HeaderData = new CompleteHeadersFrameData
                {
                    StreamId = firstHeader.StreamId,
                    Headers = headers,
                    Priority = prioData,
                    EndOfStream = isEndOfStream,
                },
            };
        }
    }

    /// <summary>
    /// 存储readheaders操作的结果
    /// </summary>
    internal struct CompleteHeadersFrameData
    {
        public uint StreamId;
        public PriorityData? Priority;
        public List<HeaderField> Headers;
        public bool EndOfStream;
    }
}
