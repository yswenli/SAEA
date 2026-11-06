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
*文件名： IMessageCodec
*版本号： v26.11.5.1
*唯一标识：6a0b0be1-0f6c-4bba-901f-24d6fcc5b6b7
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：第二层编解码：信封 payload 与业务实体之间转换
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：第二层编解码：信封 payload 与业务实体之间转换
*
*****************************************************************************/
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
        /// <param name="payload">信封携带的序列化字节。</param>
        /// <param name="targetType">目标 CLR 类型，运行期由类型注册表解析得到。</param>
        /// <returns>反序列化得到的业务实体。</returns>
        object Decode(ReadOnlyMemory<byte> payload, Type targetType);

        /// <summary>
        /// 序列化实体。必须返回独立数组，可直接交给信封持有；
        /// 不得返回池化切片，也不得让返回值指向临时缓冲。
        /// </summary>
        /// <param name="entity">待序列化的业务实体。</param>
        /// <returns>独立数组，可直接交给信封持有。</returns>
        byte[] Encode(object entity);
    }
}