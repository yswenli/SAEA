using System;
using System.Text;
using SAEA.Common.Newtonsoft.Json;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 第二层编解码：payload 与业务实体之间用 UTF-8 JSON 转换。
    /// 目标类型由路由层在运行期传入，因此按 <see cref="Type"/> 反序列化。
    /// </summary>
    public sealed class JsonMessageCodec : IMessageCodec
    {
        /// <summary>按目标 CLR 类型反序列化；空 payload 返回该类型的默认实例。</summary>
        public object Decode(ReadOnlyMemory<byte> payload, Type targetType)
        {
            if (targetType == null)
                throw new ArgumentNullException(nameof(targetType));
            if (payload.Length == 0)
                return Activator.CreateInstance(targetType);

            var text = Encoding.UTF8.GetString(payload.ToArray());
            return JsonConvert.DeserializeObject(text, targetType);
        }

        /// <summary>序列化为 UTF-8 JSON 字节，返回独立数组。</summary>
        public byte[] Encode(object entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(entity));
        }
    }
}