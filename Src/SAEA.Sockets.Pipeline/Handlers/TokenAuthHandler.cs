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