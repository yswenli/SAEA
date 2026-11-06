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
*命名空间：SAEA.Sockets.Pipeline
*文件名： DefaultChannelHandlerContext
*版本号： v26.11.5.1
*唯一标识：6873286a-a609-4188-b34e-d12ef3ba863e
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：链节点上的处理器上下文
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：链节点上的处理器上下文
*
*****************************************************************************/
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 链节点上的处理器上下文。实例随节点常驻、跨消息复用，
    /// 因此不保存任何单条消息的状态，令牌与连接都从所属链实时读取。
    /// </summary>
    internal sealed class DefaultChannelHandlerContext : IChannelHandlerContext
    {
#if DEBUG
        private sealed class OutboundWriteMarker
        {
            public ChannelPipeline.Node LastNode;

            public int Writes;
        }

        // 按消息实例记录"最后是哪个节点写的、写了几次"。键为弱引用，实例不再被引用时条目自动消失。
        private static readonly ConditionalWeakTable<object, OutboundWriteMarker> OutboundWrites =
            new ConditionalWeakTable<object, OutboundWriteMarker>();
#endif

        private readonly ChannelPipeline.Node _node;

        internal DefaultChannelHandlerContext(ChannelPipeline.Node node, ChannelPipeline pipeline)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));
            if (pipeline == null)
                throw new ArgumentNullException(nameof(pipeline));

            _node = node;
            Pipeline = pipeline;
        }

        internal ChannelPipeline Pipeline { get; }

        /// <summary>连接标识；未接入传输层时为 null。</summary>
        public string ChannelId
        {
            get { return Pipeline.ChannelId; }
        }

        /// <summary>所属连接；未接入传输层时抛 <see cref="InvalidOperationException"/>。</summary>
        public IChannel Channel
        {
            get { return Pipeline.Channel; }
        }

        IChannelPipeline IChannelHandlerContext.Pipeline
        {
            get { return Pipeline; }
        }

        /// <summary>当前节点的处理器；链首/链尾哨兵返回 null。</summary>
        public IChannelHandler Handler
        {
            get { return _node.Handler; }
        }

        /// <summary>当前消息的执行令牌，消息之外回落到连接级令牌。</summary>
        public CancellationToken CancellationToken
        {
            get { return Pipeline.CurrentCancellationToken; }
        }

        /// <summary>从当前节点的下一个入站节点继续传播。</summary>
        public Task FireChannelReadAsync(object msg)
        {
            return Pipeline.FireChannelReadFromAsync(_node.Next, msg);
        }

        /// <summary>
        /// 交给前一个出站节点；没有出站前缀时直达链头出站终点。
        /// 出站异常原样冒泡，由发起方决定补偿与归还。
        /// </summary>
        public Task WriteAsync(object msg)
        {
            VerifySingleOutboundWrite(msg);

            var prev = ChannelPipeline.FindPrevOutbound(_node.Prev);
            if (prev == null)
                return Pipeline.HeadSinkWriteAsync(msg);

            return ((IChannelOutboundHandler)prev.Handler).WriteAsync(prev.Context, msg);
        }

        /// <summary>从当前节点之后向链尾传播异常，不会绕回当前处理器。</summary>
        public Task FireExceptionCaughtAsync(Exception ex)
        {
            return Pipeline.FireExceptionCaughtFromAsync(_node.Next, ex);
        }

        /// <summary>
        /// 出站契约检查：同一个 handler 节点对同一个消息实例只允许调用一次
        /// <see cref="WriteAsync"/>；不同节点转发同一实例（如日志 handler 透传）是合法的。
        /// 同一节点重复写入会让下游产生重复帧，属于难以从抓包定位的错误。
        /// 本检查只在 Debug 构建下生效，Release 构建的调用点被编译器移除，无额外开销。
        /// </summary>
        [Conditional("DEBUG")]
        private void VerifySingleOutboundWrite(object msg)
        {
#if DEBUG
            if (msg == null)
                return;

            var marker = OutboundWrites.GetOrCreateValue(msg);
            lock (marker)
            {
                if (ReferenceEquals(marker.LastNode, _node))
                {
                    marker.Writes++;
                    if (marker.Writes > 1)
                    {
                        throw new InvalidOperationException(
                            "同一个处理器节点对同一个消息实例只能写一次，重复写入会产生重复帧；处理器：" +
                            (_node.Handler == null ? "(哨兵)" : _node.Handler.GetType().FullName) +
                            "；消息类型：" + msg.GetType().FullName);
                    }
                }
                else
                {
                    marker.LastNode = _node;
                    marker.Writes = 1;
                }
            }
#endif
        }

        /// <summary>
        /// 移除实例的出站传递标记；链头接收消息后调用，避免标记长期留在表里。
        /// 与 <see cref="VerifySingleOutboundWrite"/> 一样只在 Debug 构建下生效。
        /// </summary>
        [Conditional("DEBUG")]
        internal static void ClearOutboundWriteMarker(object msg)
        {
#if DEBUG
            if (msg == null)
                return;

            OutboundWrites.Remove(msg);
#endif
        }
    }
}