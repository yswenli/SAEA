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
*文件名： TokenAuthHandler
*版本号： v26.11.5.1
*唯一标识：60a25b3d-004f-4010-b86c-ff4c71cac08f
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：第二级鉴权：实体已还原成 CLR 对象后校验业务令牌
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：第二级鉴权：实体已还原成 CLR 对象后校验业务令牌
*
*****************************************************************************/
using System;
using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 第二级鉴权：实体已还原成 CLR 对象后校验业务令牌。
    /// <para>
    /// 默认实现从连接属性 <c>token</c> 取令牌并校验非空；需要签名、过期时间等
    /// 规则时继承并覆盖 <see cref="ValidateToken"/>，不要把它和第一级的
    /// <see cref="AuthHandler"/> 合并——合并后第一级必然要解析 payload。
    /// </para>
    /// <para>
    /// 校验失败抛 <see cref="InvalidOperationException"/>，由链尾
    /// <see cref="ExceptionTerminalHandler"/> 统一记录并断开连接。
    /// </para>
    /// </summary>
    public class TokenAuthHandler : InboundHandlerAdapter
    {
        /// <summary>连接令牌属性键。</summary>
        public const string TokenAttributeKey = "token";

        /// <summary>每连接实例持有鉴权状态，不跨连接共享。</summary>
        public override bool IsSharable
        {
            get { return false; }
        }

        /// <summary>先校验令牌，通过后原样下传；失败抛 <see cref="InvalidOperationException"/>。</summary>
        public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
        {
            if (!ValidateToken(ctx, msg))
                throw new InvalidOperationException("令牌校验失败:" + ctx.ChannelId);

            await ctx.FireChannelReadAsync(msg).ConfigureAwait(false);
        }

        /// <summary>
        /// 校验当前连接的令牌。默认读取 <c>token</c> 属性并检查非空；
        /// 子类可覆盖以实现签名、有效期等规则。返回 <c>false</c> 即拒绝该消息。
        /// </summary>
        protected virtual bool ValidateToken(IChannelHandlerContext ctx, object msg)
        {
            var token = ctx.Channel.GetAttribute(TokenAttributeKey) as string;
            return !string.IsNullOrEmpty(token);
        }
    }
}