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
*文件名： FtpControlReplyHandler
*版本号： v26.11.5.1
*唯一标识：7743b134-1ed9-419e-9761-3aecbc918cf5
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 15:00:00
*描述：FTP 客户端应答收集器，把应答交给同步等待方并唤醒收口等待
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 15:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增客户端应答收集器
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SAEA.FTP.Net;
using SAEA.Sockets.Pipeline;

namespace SAEA.FTP
{
    /// <summary>
    /// 客户端应答收集器：把解码后的应答放进有界队列，供同步 API 阻塞取用。
    /// <para>
    /// 队列容量 32。FTP 控制通道严格一问一答，正常最多暂存一条 150 与一条 226；
    /// 队列满表示调用方长期不取应答，此时阻塞分发线程形成 TCP 反压，属预期行为。
    /// 连接收口或异常时立即唤醒等待方并抛出 <see cref="IOException"/>，不会让调用方挂到超时。
    /// </para>
    /// </summary>
    public sealed class FtpControlReplyHandler : InboundHandlerAdapter
    {
        private const int QueueCapacity = 32;

        private readonly BlockingCollection<object> _replies =
            new BlockingCollection<object>(new ConcurrentQueue<object>(), QueueCapacity);

        private readonly ManualResetEventSlim _ready = new ManualResetEventSlim(false);

        private volatile IChannelPipeline _pipeline;

        private volatile Exception _closedError;

        /// <summary>
        /// 等待处理器链激活；超时或连接已关闭时返回 false。
        /// </summary>
        /// <param name="timeout">最长等待时间</param>
        /// <returns>链已激活返回 true</returns>
        internal bool WaitReady(TimeSpan timeout)
        {
            if (_pipeline != null)
                return true;

            var milliseconds = timeout <= TimeSpan.Zero ? 0 : (int)Math.Min(int.MaxValue, timeout.TotalMilliseconds);

            return _ready.Wait(milliseconds) && _pipeline != null;
        }

        /// <summary>
        /// 当前连接的写入口；链未激活或已收口时为 null。
        /// </summary>
        internal IChannelPipeline Pipeline
        {
            get { return _pipeline; }
        }

        /// <summary>
        /// 取一条应答或故障项；队列关闭时返回 false。
        /// </summary>
        /// <param name="timeout">最长等待时间</param>
        /// <param name="item">取出的应答或 <see cref="FtpReplyFault"/></param>
        /// <returns>取到内容返回 true；超时或连接已关闭返回 false</returns>
        internal bool TryTake(TimeSpan timeout, out object item)
        {
            var milliseconds = timeout <= TimeSpan.Zero ? 0 : (int)Math.Min(int.MaxValue, timeout.TotalMilliseconds);

            if (_replies.TryTake(out item, milliseconds))
                return true;

            var closed = _closedError;
            if (closed != null)
            {
                item = new FtpReplyFault(new IOException("FTP control connection closed by server", closed));
                return true;
            }

            return false;
        }

        /// <summary>
        /// 主动关闭时唤醒等待方；幂等。
        /// </summary>
        /// <param name="error">关闭原因，为 null 时使用通道关闭的标准文案</param>
        internal void MarkClosed(Exception error)
        {
            if (_closedError == null)
                _closedError = error ?? new IOException("FTP control connection closed by server");

            _pipeline = null;

            try { _replies.CompleteAdding(); } catch (ObjectDisposedException) { }

            // 激活等待也必须唤醒，否则连接失败时 Connect 会白等一个超时。
            _ready.Set();
        }

        /// <summary>
        /// 链激活时登记写入口并唤醒等待建连的线程。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <returns>登记完成后结束的任务。</returns>
        public override Task ChannelActiveAsync(IChannelHandlerContext ctx)
        {
            _pipeline = ctx.Pipeline;
            _ready.Set();
            return Task.CompletedTask;
        }

        /// <summary>
        /// 应答入队后继续下传，使用者处理器可据此做审计；非应答对象原样下传。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="msg">解码后的应答、故障项或其它消息。</param>
        /// <returns>下游处理完成后结束的任务。</returns>
        public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
        {
            if (msg is FtpReply || msg is FtpReplyFault)
                Enqueue(msg);

            return ctx.FireChannelReadAsync(msg);
        }

        /// <summary>
        /// 连接收口时唤醒全部等待方。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <returns>唤醒完成后结束的任务。</returns>
        public override Task ChannelInactiveAsync(IChannelHandlerContext ctx)
        {
            MarkClosed(null);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 异常时先唤醒等待方，再按链尾约定继续上报；连接已关闭的异常只记录不重复上报。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="ex">待处理的异常。</param>
        /// <returns>上报完成后结束的任务。</returns>
        public override Task ChannelExceptionCaughtAsync(IChannelHandlerContext ctx, Exception ex)
        {
            MarkClosed(ex);

            if (ex is ChannelClosedException)
                return Task.CompletedTask;

            return ctx.FireExceptionCaughtAsync(ex);
        }

        /// <summary>
        /// 把应答放进队列；队列已关闭时静默丢弃。
        /// </summary>
        /// <param name="item">待入队的应答或故障项</param>
        private void Enqueue(object item)
        {
            try
            {
                _replies.Add(item);
            }
            catch (InvalidOperationException)
            {
                // 收口流程已关闭队列，迟到的应答没有等待方，直接丢弃。
            }
        }
    }
}