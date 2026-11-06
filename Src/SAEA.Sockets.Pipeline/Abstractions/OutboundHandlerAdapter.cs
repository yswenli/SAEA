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
*文件名： OutboundHandlerAdapter
*版本号： v26.11.5.1
*唯一标识：c7dc90fc-a052-4cd9-8586-a590b41f5283
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：出站处理器基类，默认把消息原样交给下一出站处理器（向链首方向）
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：出站处理器基类，默认把消息原样交给下一出站处理器（向链首方向）
*
*****************************************************************************/
using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 出站处理器基类，默认把消息原样交给下一出站处理器（向链首方向）。
    /// </summary>
    public abstract class OutboundHandlerAdapter : ChannelHandlerAdapter, IChannelOutboundHandler
    {
        /// <summary>默认透传；出站异常必须原样冒泡，不得记录后返回成功。</summary>
        public virtual Task WriteAsync(IChannelHandlerContext ctx, object msg)
        {
            return ctx.WriteAsync(msg);
        }
    }
}