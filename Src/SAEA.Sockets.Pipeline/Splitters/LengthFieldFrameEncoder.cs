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
*文件名： LengthFieldFrameEncoder
*版本号： v26.11.5.1
*唯一标识：c2d5f5a4-712d-4748-8968-951363e9584d
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：基于长度字段的出站分帧器：写入 8B 小端 body 长度、1B 类型码，再写入 body
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：基于长度字段的出站分帧器：写入 8B 小端 body 长度、1B 类型码，再写入 body
*
*****************************************************************************/
using System;
using System.Buffers;
using System.Buffers.Binary;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 基于长度字段的出站分帧器：写入 8B 小端 body 长度、1B 类型码，再写入 body。
    /// 与 <see cref="LengthFieldSplitter.LegacyCompatible"/> 描述的线格式互逆。
    /// 无状态、非线程安全。
    /// </summary>
    public sealed class LengthFieldFrameEncoder : IFrameEncoder
    {
        /// <inheritdoc />
        public void Encode(ReadOnlyMemory<byte> body, int typeCode, IBufferWriter<byte> writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (typeCode < 0 || typeCode > 255)
                throw new ArgumentOutOfRangeException(nameof(typeCode), "type code 必须落在 0..255。");

            var head = writer.GetSpan(FrameConstants.P_Head);
            BinaryPrimitives.WriteInt64LittleEndian(head, body.Length);
            head[FrameConstants.P_LEN] = (byte)typeCode;
            writer.Advance(FrameConstants.P_Head);
            writer.Write(body.Span);
        }
    }
}