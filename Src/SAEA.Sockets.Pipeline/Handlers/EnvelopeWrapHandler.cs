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