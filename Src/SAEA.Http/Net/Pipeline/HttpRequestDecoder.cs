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
*命名空间：SAEA.Http
*文件名： HttpRequestDecoder
*版本号： v26.11.5.1
*唯一标识：34b2b9e5-a63f-4e76-8bd0-768b3ece45eb
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/9 13:12:01
*描述：HTTP 入站解码器：把已切好的整帧解析成 HttpMessage
*
*=====================================================================
*修改标记
*修改时间：2026/11/24 16:15:30
*修改人： yswenli
*版本号： v26.11.24.1
*描述：chunked 请求体交给 ChunkedBodyDecoder 解码，ContentLength 改写为解码后长度
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SAEA.Http.Base;
using SAEA.Http.Base.Net;
using SAEA.Sockets.Pipeline;

namespace SAEA.Http
{
    /// <summary>
    /// HTTP 入站解码器：把泵切好的整帧解析成 <see cref="HttpMessage"/>。
    /// <para>
    /// 分帧器已保证整帧完整（报头与按 Content-Length 声明的请求体都在帧内），这里只做一次报头解析。
    /// 请求体不超过落盘阈值时保留整帧、请求体流直接读帧内切片，不复制字节；超过阈值时写入临时文件，
    /// 请求体流读文件，避免解码期再复制一份大数组。
    /// </para>
    /// <para>
    /// 报头解析失败按 <see cref="HttpProtocolErrorCodes.BadRequest"/> 抛 <see cref="FrameProtocolException"/>，
    /// 由链上处理器转成 400 响应并收口。
    /// </para>
    /// </summary>
    public sealed class HttpRequestDecoder : ByteToMessageDecoder
    {
        // HTTP 连接按 MaxConcurrentMessages = 1 串行处理，同一连接上一条请求处理完才会解下一帧，
        // 因此可以复用一份消息实例；池化缓冲在每请求释放时归还
        HttpMessage _message;

        readonly int _largeBodyThreshold;

        readonly string _tempDirectory;

        readonly string _channelId;

        /// <summary>
        /// 创建解码器。
        /// </summary>
        /// <param name="largeBodyThreshold">请求体落盘阈值，单位字节；0 表示始终留在内存</param>
        /// <param name="tempDirectory">临时文件目录，null 或空白时用系统临时目录下的 saea-http</param>
        /// <param name="channelId">连接标识，用于临时文件命名</param>
        public HttpRequestDecoder(int largeBodyThreshold = 0, string tempDirectory = null, string channelId = null)
        {
            _largeBodyThreshold = largeBodyThreshold;
            _tempDirectory = tempDirectory;
            _channelId = channelId;
        }

        /// <inheritdoc />
        protected override void Decode(IChannelHandlerContext ctx, PooledMessage frame, List<object> output)
        {
            var session = HttpConnectionSession.TryGet(ctx.Channel);

            if (session != null && session.IsUpgraded)
            {
                // 升级后的字节不是 HTTP：Retain 一份交给下游，原始帧由接收入口消费后释放。
                frame.Retain();
                output.Add(frame);
                return;
            }

            var data = frame.Frame;

            var message = _message;

            if (message == null)
            {
                message = new HttpMessage();

                _message = message;
            }
            else
            {
                // 复位为新建实例的状态，集合与池化缓冲都沿用同一个对象
                message.ResetForNextRequest();
            }

            int headerLength;
            try
            {
                // Analysis 的签名是 Span<byte>，但只做只读解析；AsMemory 只换视图，不复制整帧。
                headerLength = RequestDataReader.Analysis(MemoryMarshal.AsMemory(data).Span, message, out message);
            }
            catch (Exception ex)
            {
                // 解码失败时复位复用实例，及时归还可能已借出的池化缓冲
                _message.ResetForNextRequest();

                throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest, ex);
            }

            if (message == null
                || headerLength <= 0
                || message.Position != headerLength
                || message.ContentLength < 0
                || (long)message.Position + message.ContentLength > data.Length)
            {
                throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest);
            }

            message.ID = ctx.ChannelId;

            // 文件分段落盘按连接配置执行：阈值与临时目录在解析前写入复用消息实例
            message.LargeBodyThreshold = _largeBodyThreshold;
            message.TempDirectory = _tempDirectory;

            // 请求体按阈值选择载体：小体量保留整帧、流直接读帧内切片，零复制；
            // 大体量写入临时文件，避免把整帧再复制成等长数组
            if (IsChunked(message))
            {
                // 原始 chunked 字节可能被解码结果继续引用，先给下游留一份引用；
                // 落盘或精确分配时解码器会立即释放这份引用
                frame.Retain();

                try
                {
                    message.Body = ChunkedBodyDecoder.Decode(
                        data.Slice(message.Position),
                        _largeBodyThreshold,
                        _tempDirectory,
                        _channelId ?? ctx.ChannelId,
                        frame,
                        out var decodedLength);

                    // 对业务与上层限流而言，请求体长度是解码后的负载长度
                    message.ContentLength = decodedLength;
                }
                catch (Exception ex)
                {
                    frame.Release();

                    _message.ResetForNextRequest();

                    throw new FrameProtocolException(HttpProtocolErrorCodes.BadRequest, ex);
                }

                // chunked 负载解码后与普通请求体一样派生表单、文件与 JSON
                RequestDataReader.ParseBody(message);

                output.Add(message);

                return;
            }

            if (message.ContentLength > 0)
            {
                var bodyMemory = data.Slice(message.Position, message.ContentLength);

                if (_largeBodyThreshold > 0 && bodyMemory.Length > _largeBodyThreshold)
                {
                    message.Body = HttpBodyStorage.CreateFileBacked(bodyMemory, _tempDirectory, _channelId ?? ctx.ChannelId);
                }
                else
                {
                    // 请求体流要活得比本帧处理更久，先 Retain 一份，流释放时归还整帧
                    frame.Retain();

                    try
                    {
                        message.Body = HttpRequestBodyStream.FromMemory(bodyMemory, frame);
                    }
                    catch
                    {
                        frame.Release();

                        throw;
                    }
                }

                // 表单、文件与 JSON 都从请求体流派生，不再单独复制整帧
                RequestDataReader.ParseBody(message);
            }

            output.Add(message);
        }

        /// <summary>
        /// 判断请求是否使用 chunked 传输编码。
        /// </summary>
        /// <param name="message">已解析报头的消息</param>
        /// <returns>Transfer-Encoding 的值里含 chunked 返回 true</returns>
        static bool IsChunked(HttpMessage message)
        {
            var headers = message.Headers;

            if (headers == null) return false;

            if (!headers.TryGetValue("transfer-encoding", out var value) || string.IsNullOrEmpty(value))
                return false;

            return value.IndexOf("chunked", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}