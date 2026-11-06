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
*文件名： AuthHandler
*版本号： v26.11.5.1
*唯一标识：29c79fac-1c3d-47ab-9d47-6f4ba06543a8
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：第一级鉴权：只看 Envelope.Type 白名单与连接属性，绝不解析 payload
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：第一级鉴权：只看 Envelope.Type 白名单与连接属性，绝不解析 payload
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 第一级鉴权：只看 <see cref="Envelope.Type"/> 白名单与连接属性，绝不解析 payload。
    /// <para>
    /// 未认证连接只放行白名单里的类型（如登录、心跳），其余直接关闭连接并丢弃该帧：
    /// 既不向下传，也不抛异常，避免把未认证流量变成日志风暴。
    /// 白名单类型必须在反序列化之前就可用，所以本处理器位于
    /// <see cref="EnvelopeUnwrapHandler"/> 之前。
    /// </para>
    /// </summary>
    public sealed class AuthHandler : InboundHandlerAdapter
    {
        /// <summary>连接通过鉴权后由业务写入的属性键。</summary>
        public const string AuthedAttributeKey = "authed";

        private readonly HashSet<int> _allowedBeforeAuth;

        /// <summary>
        /// 以未认证阶段允许通行的 type code 创建第一级鉴权处理器。
        /// 实例无连接状态，可跨连接共享。
        /// </summary>
        public AuthHandler(IEnumerable<int> allowedBeforeAuth)
        {
            if (allowedBeforeAuth == null)
                throw new ArgumentNullException(nameof(allowedBeforeAuth));

            _allowedBeforeAuth = new HashSet<int>(allowedBeforeAuth);
        }

        /// <summary>无连接状态，可跨连接共享。</summary>
        public override bool IsSharable
        {
            get { return true; }
        }

        /// <summary>只接受 <see cref="Envelope"/>；未认证且 type 不在白名单时关闭连接并终止传播。</summary>
        public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
        {
            var envelope = msg as Envelope;
            if (envelope == null)
                throw new InvalidOperationException("AuthHandler 的入参必须是 Envelope，且必须位于 EnvelopeUnwrapHandler 之前。");

            var authed = Equals(ctx.Channel.GetAttribute(AuthedAttributeKey), true);
            if (!authed && !_allowedBeforeAuth.Contains(envelope.Type))
            {
                await ctx.Channel.CloseAsync("unauthenticated").ConfigureAwait(false);
                return;
            }

            await ctx.FireChannelReadAsync(msg).ConfigureAwait(false);
        }
    }
}