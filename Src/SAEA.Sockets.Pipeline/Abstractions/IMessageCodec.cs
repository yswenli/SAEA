using System;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 第二层编解码：信封 payload 与业务实体之间转换。
    /// 目标类型在运行期才确定，因此必须显式传入 CLR 类型。
    /// </summary>
    public interface IMessageCodec
    {
        /// <summary>按目标 CLR 类型反序列化 payload。</summary>
        object Decode(ReadOnlyMemory<byte> payload, Type targetType);

        /// <summary>
        /// 序列化实体。必须返回独立数组，可直接交给信封持有；
        /// 不得返回池化切片，也不得让返回值指向临时缓冲。
        /// </summary>
        byte[] Encode(object entity);
    }
}