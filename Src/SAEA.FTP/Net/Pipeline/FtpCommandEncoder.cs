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
*文件名： FtpCommandEncoder
*版本号： v26.11.5.1
*唯一标识：f73eae89-3f39-4888-8718-d20bc68ac950
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 15:00:00
*描述：FTP 客户端出站编码器，把控制命令编码成行文本帧
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 15:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增客户端控制命令编码器
*
*****************************************************************************/
using System;
using System.Text;
using System.Threading.Tasks;
using SAEA.Common.Caching;
using SAEA.Sockets.Pipeline;

namespace SAEA.FTP
{
    /// <summary>
    /// 客户端出站编码器：把控制命令文本编码成「命令 + CRLF」的帧。
    /// <para>
    /// 编码按构造时给定的 <see cref="Encoding"/> 进行，默认 UTF-8，允许改为 GBK 等与服务器一致的编码。
    /// 已经是原始帧的消息直接透传。
    /// </para>
    /// </summary>
    public sealed class FtpCommandEncoder : OutboundHandlerAdapter
    {
        private readonly Encoding _encoding;

        /// <summary>
        /// 以命令文本编码创建编码器。
        /// </summary>
        /// <param name="encoding">命令文本编码，为 null 时按 UTF-8 处理</param>
        public FtpCommandEncoder(Encoding encoding)
        {
            _encoding = encoding ?? Encoding.UTF8;
        }

        /// <summary>
        /// 编码一次出站写：只接受命令文本与 <see cref="RawFrame"/>。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="msg">待发送消息，只接受 <see cref="string"/> 与 <see cref="RawFrame"/>。</param>
        /// <returns>下游处理完成后结束的任务。</returns>
        public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
        {
            var command = msg as string;
            if (command != null)
            {
                var bytes = _encoding.GetBytes(command + "\r\n");

                var writer = new PooledBufferWriter(bytes.Length);
                try
                {
                    var span = writer.GetSpan(bytes.Length);
                    new ReadOnlySpan<byte>(bytes).CopyTo(span);
                    writer.Advance(bytes.Length);

                    return ctx.WriteAsync(new RawFrame(writer.WrittenMemory, writer));
                }
                catch
                {
                    // 只在移交前失败时归还；成功交出后所有权随帧走，不能再由本节点释放。
                    writer.Dispose();
                    throw;
                }
            }

            if (msg is RawFrame)
                return ctx.WriteAsync(msg);

            throw new InvalidOperationException(
                "FtpCommandEncoder 只接受 string 或 RawFrame，实际为 "
                + (msg == null ? "(null)" : msg.GetType().FullName) + "。");
        }
    }
}