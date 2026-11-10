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
*修改时间：2026/11/10 12:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：复制请求体后按 ContentType 解析表单、文件与 JSON，保持与旧解码路径一致
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
    /// 分帧器已保证整帧完整（报头与按 Content-Length 声明的请求体都在帧内），这里只做一次报头解析
    /// 与一次请求体复制：请求体复制成独立数组交给消息对象，派发时不再引用池化帧内存。
    /// </para>
    /// <para>
    /// 报头解析失败按 <see cref="HttpProtocolErrorCodes.BadRequest"/> 抛 <see cref="FrameProtocolException"/>，
    /// 由链上处理器转成 400 响应并收口。
    /// </para>
    /// </summary>
    public sealed class HttpRequestDecoder : ByteToMessageDecoder
    {
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

            HttpMessage message;
            int headerLength;
            try
            {
                // Analysis 的签名是 Span<byte>，但只做只读解析；AsMemory 只换视图，不复制整帧。
                headerLength = RequestDataReader.Analysis(MemoryMarshal.AsMemory(data).Span, out message);
            }
            catch (Exception ex)
            {
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

            // 只复制请求体；报头已解析成字符串与集合，不再需要原始字节。
            if (message.ContentLength > 0)
            {
                message.Body = data.Slice(message.Position, message.ContentLength).ToArray();

                // 表单、文件与 JSON 都从这份 body 派生，不读原始帧，避免多复制一次整帧。
                RequestDataReader.ParseBody(message);
            }

            message.ID = ctx.ChannelId;

            output.Add(message);
        }
    }
}