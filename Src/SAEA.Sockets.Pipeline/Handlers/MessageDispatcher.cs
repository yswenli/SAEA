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
*文件名： MessageDispatcher
*版本号： v26.11.5.1
*唯一标识：b6640052-8bf4-4023-9223-141c1b918eaf
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：业务消息分发：按消息的精确 CLR 类型查表，未命中时交给可选兜底处理器
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：业务消息分发：按消息的精确 CLR 类型查表，未命中时交给可选兜底处理器
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SAEA.Common;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 业务消息分发：按消息的精确 CLR 类型查表，未命中时交给可选兜底处理器。
    /// <para>
    /// 未命中且没有兜底时不允许静默丢弃：先记一条 Warn 继续下传；
    /// 若本处理器已是链上最后一个入站节点（没有链尾兜底），则升级为 Error
    /// 并在这里就地回收消息。
    /// </para>
    /// </summary>
    public sealed class MessageDispatcher : InboundHandlerAdapter
    {
        private readonly Dictionary<Type, IMessageHandler> _handlers =
            new Dictionary<Type, IMessageHandler>();

        private IMessageHandler _fallback;

        /// <summary>按精确 CLR 类型注册处理器；同一类型重复注册以最后一次为准。</summary>
        /// <param name="handler">处理该类型消息的处理器，不能为 null。</param>
        /// <returns>当前分发器，便于链式注册。</returns>
        public MessageDispatcher Register<TMessage>(IMessageHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            _handlers[typeof(TMessage)] = handler;
            return this;
        }

        /// <summary>注册未命中时的兜底处理器；再次调用会替换旧的兜底。</summary>
        /// <param name="handler">兜底处理器，不能为 null。</param>
        /// <returns>当前分发器，便于链式注册。</returns>
        public MessageDispatcher RegisterFallback(IMessageHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            _fallback = handler;
            return this;
        }

        /// <summary>
        /// 命中则用本节点的 <paramref name="ctx"/> 调用业务处理器（保证回包沿正确节点出站）；
        /// 未命中按兜底、Warn 下传、链尾就地丢弃三级处理。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="msg">待分发的业务消息，不能为 null。</param>
        /// <returns>业务处理或下传完成后结束的任务。</returns>
        public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
        {
            if (msg == null)
                throw new InvalidOperationException("MessageDispatcher 的入参不能为 null。");

            var messageType = msg.GetType();

            IMessageHandler handler;
            if (_handlers.TryGetValue(messageType, out handler))
            {
                await handler.HandleAsync(ctx, msg).ConfigureAwait(false);
                return;
            }

            if (_fallback != null)
            {
                await _fallback.HandleAsync(ctx, msg).ConfigureAwait(false);
                return;
            }

            if (IsLastInbound(ctx))
            {
                LogHelper.Error(
                    "MessageDispatcher@tail 未命中路由且无链尾，已丢弃:" + ctx.ChannelId + "," + messageType.FullName,
                    null);
                DisposeMessage(msg);
                return;
            }

            LogHelper.Warn(
                "MessageDispatcher@chain 未命中路由，继续下传:" + ctx.ChannelId + "," + messageType.FullName,
                null);
            await ctx.FireChannelReadAsync(msg).ConfigureAwait(false);
        }

        /// <summary>链上最后一个入站节点是否就是本实例。</summary>
        private bool IsLastInbound(IChannelHandlerContext ctx)
        {
            var handlers = ctx.Pipeline.Handlers;
            for (var i = handlers.Count - 1; i >= 0; i--)
            {
                var inbound = handlers[i] as IChannelInboundHandler;
                if (inbound != null)
                    return ReferenceEquals(inbound, this);
            }

            return false;
        }

        private static void DisposeMessage(object msg)
        {
            var disposable = msg as IDisposable;
            if (disposable != null)
                disposable.Dispose();
        }
    }
}