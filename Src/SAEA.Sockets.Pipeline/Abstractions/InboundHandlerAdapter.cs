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
*文件名： InboundHandlerAdapter
*版本号： v26.11.5.1
*唯一标识：5a131e61-ecb7-4ea6-b712-8dd5e1a4af2b
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：入站处理器基类，默认把消息与异常原样传给下游
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：入站处理器基类，默认把消息与异常原样传给下游
*
*****************************************************************************/
using System;
using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 入站处理器基类，默认把消息与异常原样传给下游。
    /// 子类只重写需要介入的方法；若某个方法不应透传，必须显式重写并写明语义。
    /// </summary>
    public abstract class InboundHandlerAdapter : ChannelHandlerAdapter, IChannelInboundHandler
    {
        /// <summary>默认透传到下一入站处理器。</summary>
        public virtual Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
        {
            return ctx.FireChannelReadAsync(msg);
        }

        /// <summary>默认不做事；该通知由泵按链顺序逐个调用，不依赖处理器转发。</summary>
        public virtual Task ChannelReadCompleteAsync(IChannelHandlerContext ctx)
        {
            return Task.CompletedTask;
        }

        /// <summary>默认继续向链尾传播。</summary>
        public virtual Task ChannelExceptionCaughtAsync(IChannelHandlerContext ctx, Exception ex)
        {
            return ctx.FireExceptionCaughtAsync(ex);
        }
    }
}