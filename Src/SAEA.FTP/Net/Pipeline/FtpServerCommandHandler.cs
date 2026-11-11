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
*文件名： FtpServerCommandHandler
*版本号： v26.11.5.1
*唯一标识：5515ccdd-c2dd-4821-b152-571e9f2dce64
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 15:00:00
*描述：FTP 服务端命令分发处理器，命令交给会话同步执行
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 15:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增服务端命令分发处理器
*
*****************************************************************************/
using System;
using System.IO;
using System.Threading.Tasks;
using SAEA.FTP.Model;
using SAEA.FTP.Net;
using SAEA.Sockets.Pipeline;

namespace SAEA.FTP
{
    /// <summary>
    /// 服务端命令处理器：把解码后的 <see cref="ClientRequest"/> 交给所属会话同步执行，
    /// 并维护连接与处理器链的绑定关系。
    /// <para>
    /// 命令在连接的消费循环上内联执行（<c>DispatchMode = Queued</c>、<c>MaxConcurrentMessages = 1</c>），
    /// 与原「单会话单读循环」语义一致：传输期间不读后续命令，回复按命令顺序写出。
    /// </para>
    /// </summary>
    public sealed class FtpServerCommandHandler : InboundHandlerAdapter
    {
        private readonly FTPServer _server;

        /// <summary>
        /// 绑定所属服务端。
        /// </summary>
        /// <param name="server">所属服务端实例，为 null 抛 ArgumentNullException</param>
        public FtpServerCommandHandler(FTPServer server)
        {
            if (server == null)
                throw new ArgumentNullException(nameof(server));

            _server = server;
        }

        /// <summary>
        /// 链激活时把写入口交给会话并发出服务就绪应答。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <returns>应答写出完成后结束的任务。</returns>
        public override async Task ChannelActiveAsync(IChannelHandlerContext ctx)
        {
            var session = _server.GetSession(ctx.ChannelId);

            if (session == null)
            {
                // 会话登记缺失说明接入钩子与装配顺序被打乱，直接收口而不是空转。
                await ctx.Channel.CloseAsync("ftp-session-missing").ConfigureAwait(false);
                return;
            }

            session.AttachPipeline(ctx.Pipeline);

            await ctx.Pipeline.WriteAsync(new FtpReply
            {
                Code = ServerResponseCode.服务就绪,
                Message = "Welcome to SAEA.FTPServer!"
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 执行一条控制命令，并把命令继续下传供使用者处理器审计。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="msg">已解码的命令或其它消息。</param>
        /// <returns>命令执行与下游处理完成后结束的任务。</returns>
        public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
        {
            var request = msg as ClientRequest;

            if (request == null)
            {
                await ctx.FireChannelReadAsync(msg).ConfigureAwait(false);
                return;
            }

            var session = _server.GetSession(ctx.ChannelId);

            if (session != null)
                session.Handle(request);

            await ctx.FireChannelReadAsync(msg).ConfigureAwait(false);

            // QUIT 与「超出该用户最大会话数」都要求回复后立即断开，回复已在 Handle 内同步写出。
            if (session != null && session.QuitRequested)
            {
                try
                {
                    await ctx.Channel.CloseAsync("ftp-quit").ConfigureAwait(false);
                }
                catch (ChannelClosedException)
                {
                }
            }
        }

        /// <summary>
        /// 链失活时标记会话已关闭并解除登记。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <returns>清理完成后结束的任务。</returns>
        public override Task ChannelInactiveAsync(IChannelHandlerContext ctx)
        {
            var session = _server.GetSession(ctx.ChannelId);

            if (session != null)
            {
                session.MarkClosed();
                _server.RemoveSession(session);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 按原会话循环的分类记录异常并收口：连接级异常静默，其余记录为服务端日志。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="ex">待处理的异常。</param>
        /// <returns>收口请求返回后结束的任务。</returns>
        public override async Task ChannelExceptionCaughtAsync(IChannelHandlerContext ctx, Exception ex)
        {
            if (!(ex is IOException) && !(ex is ObjectDisposedException) && !(ex is ChannelClosedException))
                _server.Log("FTP会话异常", ex);

            if (ctx.Channel.IsActive)
            {
                try
                {
                    await ctx.Channel.CloseAsync("ftp-session-exception").ConfigureAwait(false);
                }
                catch (ChannelClosedException)
                {
                }
            }

            // 会话登记与会话释放统一由随后的失活回调完成，这里只负责收口。
        }
    }
}