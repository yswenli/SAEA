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
*文件名： FtpDataChunkDecoder
*版本号： v26.11.5.1
*唯一标识：330da161-b443-4814-be28-18752a7fa423
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 16:37:14
*描述：FTP 数据通道入站解码器，把分帧结果送进流适配器队列
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 16:37:14
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增数据通道入站解码器
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using SAEA.Sockets.Pipeline;

namespace SAEA.FTP
{
    /// <summary>
    /// 数据通道入站解码器：把分帧器切出的每一块复制成 <see cref="FtpDataChunk"/>，
    /// 再交给 <see cref="ChannelStreamAdapter"/> 的有界队列。
    /// <para>
    /// 数据块不走链下游：数据通道的消费方是同步 <c>Stream</c> 读循环，由适配器转交给它。
    /// 队列满时本解码器会阻塞等待消费者出队，这是数据通道的背压点。
    /// </para>
    /// </summary>
    public sealed class FtpDataChunkDecoder : ByteToMessageDecoder
    {
        private readonly ChannelStreamAdapter _adapter;

        /// <summary>
        /// 以数据流适配器创建解码器。
        /// </summary>
        /// <param name="adapter">接收数据块的适配器，为 null 抛 <see cref="ArgumentNullException"/></param>
        public FtpDataChunkDecoder(ChannelStreamAdapter adapter)
        {
            if (adapter == null)
                throw new ArgumentNullException(nameof(adapter));

            _adapter = adapter;
        }

        /// <summary>
        /// 把一帧整块复制成数据块并入队；空帧不入队。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="frame">无边界分帧器切出的一块，内存只在本次回调内有效。</param>
        /// <param name="output">按序派发的消息列表；数据块不经链派发，本方法不追加。</param>
        protected override void Decode(IChannelHandlerContext ctx, PooledMessage frame, List<object> output)
        {
            var bytes = frame.Frame;
            if (bytes.Length == 0)
                return;

            // 帧是池化内存，只能在本回调内使用，必须整块深拷贝后再交给适配器。
            var chunk = FtpDataChunk.CopyFrom(bytes.Span);
            try
            {
                // 队列满时在这里阻塞等待消费者出队；通道已收口时适配器会直接归还本块。
                _adapter.Enqueue(chunk);
            }
            catch
            {
                // 入队本身失败表示所有权没有转出去，必须在这里归还，否则池化缓冲泄漏。
                chunk.Dispose();
                throw;
            }
        }
    }
}