/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.FTP
*文件名： FtpReplyDecoder
*版本号： v26.11.5.1
*唯一标识：f3eac04c-fe14-4b2e-8be3-058deb4b6e5c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 15:00:00
*描述：FTP 客户端应答解码器，跨帧拼装多行响应
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 15:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增客户端应答解码器
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SAEA.FTP.Net;
using SAEA.Sockets.Pipeline;

namespace SAEA.FTP
{
    /// <summary>
    /// 客户端应答解码器：把行分帧器产出的每一行拼成一条完整 <see cref="FtpReply"/>。
    /// <para>
    /// 多行应答是跨帧状态，因此本类持有挂起状态而不复用 <see cref="ByteToMessageDecoder"/>：
    /// 首行第 4 个字符为 <c>-</c> 时进入多行，直到出现「同一状态码 + 空格」开头的终止行才收口。
    /// 非法应答不抛异常，改为产出 <see cref="FtpReplyFault"/>，由上层在等待方线程抛出，
    /// 连接保持可用，与过渡前逐个字节读取时的行为一致。
    /// </para>
    /// </summary>
    public sealed class FtpReplyDecoder : InboundHandlerAdapter
    {
        private readonly Encoding _encoding;

        private bool _inMultiLine;

        private int _code;

        private StringBuilder _raw;

        private StringBuilder _message;

        /// <summary>
        /// 以应答文本编码创建解码器。
        /// </summary>
        /// <param name="encoding">应答文本编码，为 null 时按 UTF-8 处理</param>
        public FtpReplyDecoder(Encoding encoding)
        {
            _encoding = encoding ?? Encoding.UTF8;
        }

        /// <summary>
        /// 解码一行应答；未收口时不向下游派发任何消息。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="msg">行分帧器切好的一行，必须是 <see cref="PooledMessage"/>。</param>
        /// <returns>派发完成或下游处理完成后结束的任务。</returns>
        public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
        {
            var frame = msg as PooledMessage;
            if (frame == null)
                throw new InvalidOperationException("FtpReplyDecoder 的入参必须是 PooledMessage。");

            var line = _encoding.GetString(frame.Frame.ToArray());

            var output = new List<object>(1);
            var item = DecodeLine(line);
            if (item != null)
                output.Add(item);

            for (var i = 0; i < output.Count; i++)
                await ctx.FireChannelReadAsync(output[i]).ConfigureAwait(false);
        }

        /// <summary>
        /// 连接收口时丢弃未收口的多行状态，避免下一次连接读到上一次的残留。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <returns>重置完成后结束的任务。</returns>
        public override Task ChannelInactiveAsync(IChannelHandlerContext ctx)
        {
            Reset();
            return Task.CompletedTask;
        }

        /// <summary>
        /// 按跨行状态机处理一行，收口时返回应答对象，否则返回 null。
        /// </summary>
        /// <param name="line">已去掉 CRLF 的一行文本</param>
        /// <returns>完整应答或非法应答的故障项；未收口时为 null</returns>
        private object DecodeLine(string line)
        {
            if (!_inMultiLine)
            {
                if (line.Length < 3)
                    return new FtpReplyFault(new IOException("Malformed FTP reply: " + line));

                int code;
                if (!int.TryParse(line.Substring(0, 3), out code))
                    return new FtpReplyFault(new IOException("Malformed FTP reply: " + line));

                var message = line.Length > 4 ? line.Substring(4) : string.Empty;

                if (line.Length > 3 && line[3] == '-')
                {
                    _inMultiLine = true;
                    _code = code;
                    _raw = new StringBuilder(line);
                    _message = new StringBuilder(message);
                    return null;
                }

                return new FtpReply
                {
                    Code = code,
                    Message = message,
                    Raw = line
                };
            }

            _raw.Append('\n').Append(line);

            if (line.StartsWith(_code + " ", StringComparison.Ordinal))
            {
                var reply = new FtpReply
                {
                    Code = _code,
                    Message = _message.ToString(),
                    Raw = _raw.ToString()
                };

                Reset();
                return reply;
            }

            if (_message.Length > 0) _message.Append('\n');
            _message.Append(line);

            return null;
        }

        /// <summary>清空多行挂起状态。</summary>
        private void Reset()
        {
            _inMultiLine = false;
            _code = 0;
            _raw = null;
            _message = null;
        }
    }

    /// <summary>
    /// 客户端应答解析失败：由 <see cref="FtpReplyDecoder"/> 产出，等待方取出后转成 <see cref="IOException"/>。
    /// 只做连接内部的传递载体，不对外暴露。
    /// </summary>
    internal sealed class FtpReplyFault
    {
        /// <summary>
        /// 以原始异常创建故障项。
        /// </summary>
        /// <param name="error">解析失败的原因</param>
        public FtpReplyFault(Exception error)
        {
            Error = error;
        }

        /// <summary>
        /// 解析失败的原因。
        /// </summary>
        public Exception Error { get; private set; }
    }
}