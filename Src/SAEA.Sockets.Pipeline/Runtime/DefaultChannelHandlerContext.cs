using System;
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
    }
}