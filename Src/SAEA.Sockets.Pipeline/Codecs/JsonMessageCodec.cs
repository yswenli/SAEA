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
*文件名： JsonMessageCodec
*版本号： v26.11.5.1
*唯一标识：4af66152-f920-4f14-b483-fc20762c865e
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：第二层编解码：payload 与业务实体之间用 UTF-8 JSON 转换
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：第二层编解码：payload 与业务实体之间用 UTF-8 JSON 转换
*
*****************************************************************************/
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