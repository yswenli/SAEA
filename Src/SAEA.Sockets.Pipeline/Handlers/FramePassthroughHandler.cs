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
*文件名： FramePassthroughHandler
*版本号： v26.11.5.1
*唯一标识：41599aae-5055-4766-9439-430e2702f3cd
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：原始帧字节旁路：Retain 队列条目后把同一个 PooledMessage 交给下游分支，由该分支自己的尾部处理器 PooledMessage.Release 这一份引用
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：原始帧字节旁路：Retain 队列条目后把同一个 PooledMessage 交给下游分支，由该分支自己的尾部处理器 PooledMessage.Release 这一份引用
*
*****************************************************************************/
using System;
using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 原始帧字节旁路：<see cref="Retain"/> 队列条目后把同一个 <see cref="PooledMessage"/>
    /// 交给下游分支，由该分支自己的尾部处理器 <see cref="PooledMessage.Release"/> 这一份引用。
    /// <para>
    /// executor 的执行包 <c>finally</c> 释放的是队列条目那一份，与这里 Retain 出来的不是同一个引用，
    /// 两者互不替代。本处理器不得与 <see cref="CodecDecoderHandler"/> /
    /// <see cref="EnvelopeUnwrapHandler"/> 装在同一条链上——两套所有权不能并存。
    /// </para>
    /// </summary>
    public sealed class FramePassthroughHandler : InboundHandlerAdapter
    {
        /// <summary>只接受 <see cref="PooledMessage"/>；Retain 后原样下传。</summary>
        public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
        {
            var pooled = msg as PooledMessage;
            if (pooled == null)
                throw new InvalidOperationException("FramePassthroughHandler 的入参必须是 PooledMessage。");

            pooled.Retain();
            await ctx.FireChannelReadAsync(msg).ConfigureAwait(false);
        }
    }
}