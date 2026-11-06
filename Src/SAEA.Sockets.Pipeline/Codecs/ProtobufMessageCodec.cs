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
*文件名： ProtobufMessageCodec
*版本号： v26.11.5.1
*唯一标识：5133a204-0c84-4cfb-adc0-2cb733367d1e
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：第二层编解码：payload 与业务实体之间用 protobuf 转换
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：第二层编解码：payload 与业务实体之间用 protobuf 转换
*
*****************************************************************************/
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