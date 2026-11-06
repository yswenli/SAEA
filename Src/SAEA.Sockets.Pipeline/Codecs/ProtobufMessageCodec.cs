using System;
using System.IO;
using ProtoBuf.Meta;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 第二层编解码：payload 与业务实体之间用 protobuf 转换。
    /// 使用默认类型模型，实体需标注 <c>[ProtoContract]</c> / <c>[ProtoMember]</c>。
    /// </summary>
    public sealed class ProtobufMessageCodec : IMessageCodec
    {
        /// <summary>按目标 CLR 类型反序列化；空 payload 返回该类型的默认实例。</summary>
        public object Decode(ReadOnlyMemory<byte> payload, Type targetType)
        {
            if (targetType == null)
                throw new ArgumentNullException(nameof(targetType));
            if (payload.Length == 0)
                return Activator.CreateInstance(targetType);

            var bytes = payload.ToArray();
            using (var stream = new MemoryStream(bytes, false))
            {
                var seed = targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
                return RuntimeTypeModel.Default.Deserialize(stream, seed, targetType);
            }
        }

        /// <summary>序列化为 protobuf 字节，返回独立数组。</summary>
        public byte[] Encode(object entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            using (var stream = new MemoryStream())
            {
                RuntimeTypeModel.Default.Serialize(stream, entity);
                return stream.ToArray();
            }
        }
    }
}