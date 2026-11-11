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
*命名空间：SAEA.FTP
*文件名： FtpDataFaultHandler
*版本号： v26.11.5.1
*唯一标识：c6dc7383-8977-491c-9277-360c6abd5902
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 18:20:00
*描述：数据通道的失活与异常桥接器，把连接级通知翻译成流适配器的 EOF 与失败
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 17:57:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：修正 banner 空格行并对齐模板
*
*****************************************************************************/
using System;
using System.Threading.Tasks;
using SAEA.Sockets.Pipeline;

namespace SAEA.FTP
{
    /// <summary>
    /// 数据通道的桥接处理器：把链上的失活与异常翻译成 <see cref="ChannelStreamAdapter"/> 的 EOF 与失败，
    /// 让阻塞在同步 <see cref="System.IO.Stream"/> 读上的传输代码立刻感知连接结束。
    /// <para>
    /// 本处理器不消费、不转发数据帧，只处理连接级通知：失活表示不会再到达新数据，异常则让读侧立即抛出。
    /// 异常仍向链尾传播，由 <see cref="ExceptionTerminalHandler"/> 负责记录与关闭连接。
    /// </para>
    /// </summary>
    public sealed class FtpDataFaultHandler : InboundHandlerAdapter
    {
        private readonly ChannelStreamAdapter _adapter;

        /// <summary>
        /// 绑定需要唤醒的流适配器。
        /// </summary>
        /// <param name="adapter">数据通道流适配器，为 null 抛 <see cref="ArgumentNullException"/></param>
        public FtpDataFaultHandler(ChannelStreamAdapter adapter)
        {
            if (adapter == null)
                throw new ArgumentNullException(nameof(adapter));

            _adapter = adapter;
        }

        /// <summary>
        /// 连接失活（对端 FIN 或本地收口）时标记 EOF：已经入队的数据仍可读完，读空后返回 0。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <returns>已完成的任务。</returns>
        public override Task ChannelInactiveAsync(IChannelHandlerContext ctx)
        {
            _adapter.Complete();

            return Task.CompletedTask;
        }

        /// <summary>
        /// 链上异常时先把失败转交适配器唤醒读侧，再继续向链尾传播由兜底处理器收口。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="ex">待传播的异常。</param>
        /// <returns>下游异常处理完成后结束的任务。</returns>
        public override Task ChannelExceptionCaughtAsync(IChannelHandlerContext ctx, Exception ex)
        {
            // 兜底处理器会按异常类型决定是否断连，这里只保证同步读侧不会继续空等。
            if (ex != null)
                _adapter.Fail(ex);

            return ctx.FireExceptionCaughtAsync(ex);
        }
    }
}