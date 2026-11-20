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
*修改时间：2026/11/20 14:30:00
*修改人： yswenli
*版本号： v26.11.16.1
*描述：调用视图改为按节点单槽复用，出站守卫改为全链共享
*
*****************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 链内部能力：把解码器产出的消息按「链持有」交给下一个入站处理器。
    /// 解码器每次输出都持有一份引用，即使与输入帧是同一实例，异常补偿也由入站边界承担；
    /// 普通 handler 透传同一引用时不走该入口，仍继承上游所有权标记。
    /// </summary>
    internal interface IInboundChainOwnedContext
    {
        /// <summary>按链持有语义把消息交给下一个入站处理器。</summary>
        /// <param name="msg">解码器产出的消息。</param>
        /// <returns>下游处理完成后结束的任务。</returns>
        Task FireChainOwnedChannelReadAsync(object msg);
    }

    /// <summary>
    /// 链节点上的处理器上下文。节点常驻实例只用于 <see cref="IChannelHandler.HandlerAdded"/> /
    /// <see cref="IChannelHandler.HandlerRemoved"/> 与活动/失活等连接级回调；
    /// 每次入站/出站调用从节点租用一个绑定当前节点与本次调用的视图（单槽复用，槽被并发占用时新建），
    /// 回调结束后失效并归还。回调结束后视图失效，跨回调保存的引用不得再使用；
    /// 实例被复用后旧引用可能静默指向后续调用。
    /// </summary>
    internal sealed class DefaultChannelHandlerContext : IChannelHandlerContext, IInboundChainOwnedContext
    {
        private ChannelPipeline.Node _node;
        private InboundDispatch _dispatch;
        private object _inboundMessage;
        private OutboundOperation _operation;
        private IChannelPipeline _pipelineView;
        private int _viewValid;
        private int _outboundForwarded;

        internal DefaultChannelHandlerContext(ChannelPipeline.Node node, ChannelPipeline pipeline)
            : this(node, pipeline, null, null, null)
        {
        }

        internal DefaultChannelHandlerContext(
            ChannelPipeline.Node node,
            ChannelPipeline pipeline,
            InboundDispatch dispatch,
            object inboundMessage)
            : this(node, pipeline, dispatch, inboundMessage, null)
        {
        }

        /// <summary>创建绑定一次业务写操作的出站调用视图；回调结束后失效。</summary>
        internal DefaultChannelHandlerContext(
            ChannelPipeline.Node node,
            ChannelPipeline pipeline,
            OutboundOperation operation,
            object outboundMessage)
            : this(node, pipeline, null, outboundMessage, operation)
        {
        }

        private DefaultChannelHandlerContext(
            ChannelPipeline.Node node,
            ChannelPipeline pipeline,
            InboundDispatch dispatch,
            object inboundMessage,
            OutboundOperation operation)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));
            if (pipeline == null)
                throw new ArgumentNullException(nameof(pipeline));

            _node = node;
            Pipeline = pipeline;
            _dispatch = dispatch;
            _inboundMessage = inboundMessage;
            _operation = operation;
            // 出站守卫只把成员转发给真实链、WriteAsync 恒抛，无状态，全链共用一个实例即可。
            _pipelineView = operation == null ? (IChannelPipeline)pipeline : pipeline.OutboundGuard;
            _viewValid = 1;
        }

        internal ChannelPipeline Pipeline { get; private set; }

        /// <summary>
        /// 把从节点槽里租到的视图复位为本次调用的绑定。节点与链由租用方保证同值，这里不重复写；
        /// 归还前 <see cref="InvalidateView"/> 已清空消息与操作，此处按本次调用重写并重新置为有效。
        /// </summary>
        /// <param name="dispatch">入站派发状态；出站调用为 null。</param>
        /// <param name="inboundMessage">本次调用边界持有的消息。</param>
        /// <param name="operation">本次业务写操作；入站调用为 null。</param>
        /// <param name="pipelineView">本次调用暴露给处理器的链视图。</param>
        internal void Reset(
            InboundDispatch dispatch,
            object inboundMessage,
            OutboundOperation operation,
            IChannelPipeline pipelineView)
        {
            _dispatch = dispatch;
            _inboundMessage = inboundMessage;
            _operation = operation;
            _pipelineView = pipelineView;
            _outboundForwarded = 0;

            // 绑定先写完再置有效：读方只在看到有效标记后才使用本次绑定。
            Volatile.Write(ref _viewValid, 1);
        }

        /// <summary>本次出站调用绑定的业务写操作；入站视图与常驻实例为 null。</summary>
        internal OutboundOperation Operation
        {
            get { return _operation; }
        }

        /// <summary>本次出站调用是否已通过 <see cref="WriteAsync"/> 把消息交给下游。</summary>
        internal int OutboundForwarded
        {
            get { return Volatile.Read(ref _outboundForwarded); }
        }

        /// <summary>连接标识；未接入传输层或节点仍处于 HandlerAdded 半初始化阶段时为 null。</summary>
        public string ChannelId
        {
            get { return _node.LinkedOnce ? Pipeline.ChannelId : null; }
        }

        /// <summary>
        /// 所属连接；节点在 HandlerAdded 阶段尚未挂链时抛 <see cref="InvalidOperationException"/>，
        /// 需要连接或回包的初始化应放到 <see cref="IChannelInboundHandler.ChannelActiveAsync"/>。
        /// </summary>
        public IChannel Channel
        {
            get
            {
                if (!_node.LinkedOnce)
                {
                    throw new InvalidOperationException(
                        "HandlerAdded 阶段节点尚未挂链，不能读取 Channel；需要连接时请改在 ChannelActiveAsync 中初始化。");
                }

                return Pipeline.Channel;
            }
        }

        IChannelPipeline IChannelHandlerContext.Pipeline
        {
            get { return _pipelineView; }
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

        /// <summary>
        /// 从当前节点的下一个入站节点继续传播。
        /// 同一引用透传时继承上游所有权标记；换成新对象时按链持有处理。
        /// 视图在回调结束后失效，继续调用会抛 <see cref="InvalidOperationException"/>。
        /// </summary>
        /// <param name="msg">待传给下一个入站处理器的消息。</param>
        /// <returns>下游处理完成后结束的任务。</returns>
        public Task FireChannelReadAsync(object msg)
        {
            return FireChannelReadCoreAsync(msg, false);
        }

        /// <summary>
        /// 解码器输出专用入口：无论消息是否与输入帧是同一实例，都按链持有处理。
        /// </summary>
        /// <param name="msg">解码器产出的消息。</param>
        /// <returns>下游处理完成后结束的任务。</returns>
        Task IInboundChainOwnedContext.FireChainOwnedChannelReadAsync(object msg)
        {
            return FireChannelReadCoreAsync(msg, true);
        }

        private Task FireChannelReadCoreAsync(object msg, bool forceChainOwned)
        {
            // 失效判定必须先于常驻实例判定：归还到槽里的视图会把派发信息清空，
            // 若先判 _dispatch 为空，失效视图就会被当成常驻实例继续向下游派发。
            if (Volatile.Read(ref _viewValid) == 0)
            {
                throw new InvalidOperationException(
                    "入站处理器调用的上下文视图已失效，不能继续转发消息；需要跨回调保留状态时请保存处理器自身字段。");
            }

            if (_dispatch == null)
            {
                // 常驻实例不在某次消息派发内，没有上游所有权可继承，按链持有处理。
                return Pipeline.FireChannelReadFromAsync(_node.Next, msg, InboundDispatch.CreateChainOwned(null));
            }

            var chainOwned = forceChainOwned || !ReferenceEquals(msg, _inboundMessage) || _dispatch.ChainOwned;
            var next = _dispatch.Derive(chainOwned);

            // 调用即表示已交给下一节点：此后若下游抛错或取消，补偿由下游边界负责，本节点不再重复释放。
            _dispatch.Forwarded = true;

            return Pipeline.FireChannelReadFromAsync(_node.Next, msg, next);
        }

        /// <summary>
        /// 使本次调用绑定的视图失效；回调结束后由链调用，常驻实例不受影响。
        /// 同时清空消息与操作绑定，停在节点槽里的视图不会长期钉住上一条消息与上一个操作。
        /// </summary>
        internal void InvalidateView()
        {
            Volatile.Write(ref _viewValid, 0);
            _dispatch = null;
            _inboundMessage = null;
            _operation = null;
        }

        /// <summary>
        /// 交给前一个出站节点；没有出站前缀时直达链头出站终点。
        /// 只在当前出站回调内可用：每次调用创建一个绑定同一操作的新视图交给下游节点，
        /// 不重新计数或占额；出站回调外、视图失效或同一节点重复转发均抛 <see cref="InvalidOperationException"/>。
        /// 出站异常原样冒泡，由发起方决定补偿与归还。
        /// </summary>
        /// <param name="msg">待发送消息。</param>
        /// <returns>下一个出站处理器处理完成后结束的任务。</returns>
        public Task WriteAsync(object msg)
        {
            // 失效判定必须先于操作绑定判定：归还到槽里的视图会把操作清空，
            // 若先判 _operation 为空，失效视图会被报成「只能在出站回调内」，掩盖了真正原因。
            if (Volatile.Read(ref _viewValid) == 0)
            {
                throw new InvalidOperationException(
                    "出站处理器调用的上下文视图已失效，不能继续转发；需要跨回调保留状态时请保存处理器自身字段。");
            }

            if (_operation == null)
            {
                throw new InvalidOperationException(
                    "IChannelHandlerContext.WriteAsync 只能在出站处理器的 WriteAsync 回调内向前转发；"
                    + "独立业务写请使用 IChannelPipeline.WriteAsync。");
            }

            // 同一节点同一操作只允许转发一次：第二次转发会产生重复帧，且已移交的所有权不能再补偿。
            if (Interlocked.Exchange(ref _outboundForwarded, 1) != 0)
            {
                throw new InvalidOperationException(
                    "同一个出站节点对同一次写操作只能转发一次，重复转发会产生重复帧；消息类型："
                    + (msg == null ? "(null)" : msg.GetType().FullName));
            }

            var prev = ChannelPipeline.FindPrevOutbound(_node.Prev);
            if (prev == null)
                return Pipeline.HeadSinkWriteAsync(msg);

            // 下游节点同样使用绑定当前操作的调用视图，保证其 ctx.WriteAsync 沿用同一操作且能识别重复转发。
            return Pipeline.InvokeOutboundAsync(prev, _operation, msg);
        }

        /// <summary>
        /// 从当前节点之后向链尾传播异常，不会绕回当前处理器。
        /// 在消息派发视图中调用即视为本次派发已通知过，上游边界据此去重。
        /// </summary>
        public Task FireExceptionCaughtAsync(Exception ex)
        {
            if (_dispatch != null)
                _dispatch.MarkExceptionNotified();

            return Pipeline.FireExceptionCaughtFromAsync(_node.Next, ex);
        }

        /// <summary>
        /// 等已提交的出站写全部交接给传输层后再收口。
        /// 禁止在出站回调栈内调用，否则当前写会自等；连接已在收口时由链侧按 no-op 处理。
        /// </summary>
        /// <param name="reason">关闭原因，写入日志用于区分关闭来源。</param>
        /// <returns>已提交的写全部交接或等待超时后结束的任务。</returns>
        public Task CloseAfterWriteAsync(string reason)
        {
            // 视图失效后不允许再借它触发连接级动作：入站视图失效前 _operation 本就是 null，
            // 若只判 _operation，出站视图停放在槽里被旧引用调用就会静默关连接。
            if (Volatile.Read(ref _viewValid) == 0)
            {
                throw new InvalidOperationException(
                    "上下文视图已失效，不能继续发起关闭等待；需要跨回调收口时请保存处理器自身字段。");
            }

            if (_operation != null)
            {
                throw new InvalidOperationException(
                    "不能在出站 WriteAsync 回调内调用 CloseAfterWriteAsync：当前写尚未完成，等待会自等。");
            }

            return Pipeline.CloseAfterWriteAsync(reason);
        }
    }
}