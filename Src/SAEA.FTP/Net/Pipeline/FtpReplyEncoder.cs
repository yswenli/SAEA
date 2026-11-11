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
*文件名： FtpReplyEncoder
*版本号： v26.11.5.1
*唯一标识：3535cb27-51f0-4b50-b7b6-e0ff0a44d691
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 15:00:00
*描述：FTP 服务端出站编码器，把控制回复编码成行文本帧
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 15:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增服务端控制回复编码器
*
*****************************************************************************/
using System;
using System.Buffers;
using System.Text;
using System.Threading.Tasks;
using SAEA.Common.Caching;
using SAEA.FTP.Model;
using SAEA.FTP.Net;
using SAEA.Sockets.Pipeline;

namespace SAEA.FTP
{
    /// <summary>
    /// 服务端出站编码器：把 <see cref="FtpReply"/> 与 <see cref="FtpRawText"/> 编码成 UTF-8 文本帧。
    /// <para>
    /// 单行回复自动追加 CRLF；<see cref="FtpRawText"/> 视为已带换行的完整文本原样输出。
    /// 已经是原始帧的消息直接透传。编码得到的帧自带池化缓冲所有权，随帧移交给传输层后由传输层归还。
    /// </para>
    /// </summary>
    public sealed class FtpReplyEncoder : OutboundHandlerAdapter
    {
        private static readonly Encoding _encoding = new UTF8Encoding(false);

        /// <summary>
        /// 编码一次出站写：状态码与文本拼成 <c>code message\r\n</c>，原样文本不追加换行。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="msg">待发送消息，只接受 <see cref="FtpReply"/>、<see cref="FtpRawText"/> 与 <see cref="RawFrame"/>。</param>
        /// <returns>下游处理完成后结束的任务。</returns>
        public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
        {
            var reply = msg as FtpReply;
            if (reply != null)
                return WriteTextAsync(ctx, reply.Code + " " + (reply.Message ?? string.Empty) + "\r\n");

            var raw = msg as FtpRawText;
            if (raw != null)
                return WriteTextAsync(ctx, raw.Text);

            if (msg is RawFrame)
                return ctx.WriteAsync(msg);

            throw new InvalidOperationException(
                "FtpReplyEncoder 只接受 FtpReply、FtpRawText 或 RawFrame，实际为 "
                + (msg == null ? "(null)" : msg.GetType().FullName) + "。");
        }

        /// <summary>
        /// 把文本按 UTF-8 编成帧并交给下游；编码失败时归还池化缓冲后原样抛出。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="text">待写出的完整文本</param>
        /// <returns>下游处理完成后结束的任务</returns>
        private static Task WriteTextAsync(IChannelHandlerContext ctx, string text)
        {
            // 回复都是短文本，先编码成数组再整段复制，避免依赖 netstandard2.0 未提供的 Span 重载。
            var bytes = _encoding.GetBytes(text ?? string.Empty);

            var writer = new PooledBufferWriter(bytes.Length);
            try
            {
                if (bytes.Length > 0)
                {
                    var span = writer.GetSpan(bytes.Length);
                    new ReadOnlySpan<byte>(bytes).CopyTo(span);
                    writer.Advance(bytes.Length);
                }

                return ctx.WriteAsync(new RawFrame(writer.WrittenMemory, writer));
            }
            catch
            {
                // 只在移交前失败时归还；成功交出后所有权随帧走，不能再由本节点释放。
                writer.Dispose();
                throw;
            }
        }
    }
}