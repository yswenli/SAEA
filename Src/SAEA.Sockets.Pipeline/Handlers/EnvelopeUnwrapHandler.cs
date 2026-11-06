using System;
using System.Threading.Tasks;
using SAEA.Common;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 入站解包：按信封里的类型码查 CLR 类型，再用第二层编解码器还原业务实体。
    /// 位于鉴权之后，未认证流量不会走到反序列化。
    /// </summary>
    public sealed class EnvelopeUnwrapHandler : InboundHandlerAdapter
    {
        /// <summary>payload 拷贝告警阈值默认值（4KB），与 <see cref="ChannelPipelineOptions.EnvelopeCopyThreshold"/> 同口径。</summary>
        public const int DefaultCopyThreshold = 4 * 1024;

        private readonly ITypeRegistry _registry;
        private readonly IMessageCodec _codec;
        private readonly int _copyThreshold;

        /// <summary>以默认告警阈值创建入站解包处理器。</summary>
        public EnvelopeUnwrapHandler(ITypeRegistry registry, IMessageCodec codec)
            : this(registry, codec, DefaultCopyThreshold)
        {
        }

        /// <summary>
        /// 以指定 payload 告警阈值创建入站解包处理器。阈值只影响日志，
        /// 超阈值仍按信封路径完整拷贝，不改变解包方式。
        /// </summary>
        public EnvelopeUnwrapHandler(ITypeRegistry registry, IMessageCodec codec, int envelopeCopyThreshold)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));
            if (codec == null)
                throw new ArgumentNullException(nameof(codec));
            if (envelopeCopyThreshold < 0)
                throw new ArgumentOutOfRangeException(nameof(envelopeCopyThreshold), "payload 告警阈值不能为负。");

            _registry = registry;
            _codec = codec;
            _copyThreshold = envelopeCopyThreshold;
        }

        /// <summary>只接受 <see cref="Envelope"/>；类型码未注册时抛 <see cref="InvalidOperationException"/>。</summary>
        public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
        {
            var envelope = msg as Envelope;
            if (envelope == null)
                throw new InvalidOperationException("EnvelopeUnwrapHandler 的入参必须是 Envelope。");

            var target = _registry.Resolve(envelope.Type);
            if (target == null)
                throw new InvalidOperationException("type code " + envelope.Type + " 未在 ITypeRegistry 注册。");

            if (envelope.Payload != null && envelope.Payload.Length > _copyThreshold)
            {
                LogHelper.Warn(
                    "Envelope payload 超过阈值:" + ctx.ChannelId + "," + envelope.Payload.Length + "," + _copyThreshold,
                    null);
            }

            var entity = _codec.Decode(envelope.Payload ?? Array.Empty<byte>(), target);
            await ctx.FireChannelReadAsync(entity).ConfigureAwait(false);
        }
    }
}