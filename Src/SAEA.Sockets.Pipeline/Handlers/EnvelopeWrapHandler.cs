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
*文件名： EnvelopeWrapHandler
*版本号： v26.11.5.1
*唯一标识：5e79634c-d73f-438a-b696-69360316d4af
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：出站包装：把业务实体序列化成 payload，并按类型注册表查出类型码，产出信封
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：出站包装：把业务实体序列化成 payload，并按类型注册表查出类型码，产出信封
*
*****************************************************************************/
using System;
using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 出站包装：把业务实体序列化成 payload，并按类型注册表查出类型码，产出信封。
    /// 未注册的 CLR 类型直接抛错——出站是自家代码，静默丢回包更糟。
    /// </summary>
    public sealed class EnvelopeWrapHandler : OutboundHandlerAdapter
    {
        private readonly ITypeRegistry _registry;
        private readonly IMessageCodec _codec;

        /// <summary>以类型注册表与第二层编解码器创建出站包装处理器。</summary>
        /// <param name="registry">类型码与 CLR 类型的双向映射，不能为 null。</param>
        /// <param name="codec">业务实体到 payload 的编解码器，不能为 null。</param>
        public EnvelopeWrapHandler(ITypeRegistry registry, IMessageCodec codec)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));
            if (codec == null)
                throw new ArgumentNullException(nameof(codec));

            _registry = registry;
            _codec = codec;
        }

        /// <summary>实体 → 类型码 + payload；类型未注册时抛 <see cref="InvalidOperationException"/>。</summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="msg">待包装的业务实体，不能为 null。</param>
        /// <returns>下游写出完成后结束的任务。</returns>
        public override async Task WriteAsync(IChannelHandlerContext ctx, object msg)
        {
            if (msg == null)
                throw new InvalidOperationException("EnvelopeWrapHandler 的入参不能为 null。");

            var type = msg.GetType();
            var code = _registry.ResolveCode(type);
            if (code < 0)
                throw new InvalidOperationException("类型 " + type.FullName + " 未在 ITypeRegistry 注册。");

            var payload = _codec.Encode(msg);
            await ctx.WriteAsync(new Envelope { Type = code, Payload = payload }).ConfigureAwait(false);
        }
    }
}