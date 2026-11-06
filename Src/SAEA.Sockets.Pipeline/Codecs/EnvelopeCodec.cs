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
*文件名： EnvelopeCodec
*版本号： v26.11.5.1
*唯一标识：ca292187-e6e6-4e26-b6a6-5f6a0e698a3d
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：v1 生产默认 codec：把 [1B type][payload] 帧切成信封，不做任何序列化
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：v1 生产默认 codec：把 [1B type][payload] 帧切成信封，不做任何序列化
*
*****************************************************************************/
using System;
using System.Buffers;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// v1 生产默认 codec：把 <c>[1B type][payload]</c> 帧切成信封，不做任何序列化。
    /// 解码出的 payload 是独立数组，调用方可以安全地释放原帧背衬。
    /// </summary>
    public sealed class EnvelopeCodec : ICodec
    {
        /// <summary>切出首字节作为类型码，其余字节精确拷贝为 payload。</summary>
        /// <param name="frame">分帧器产出的完整帧，首字节为类型码。</param>
        /// <returns>持有独立 payload 数组的信封对象。</returns>
        public object Decode(ReadOnlyMemory<byte> frame)
        {
            if (frame.Length < FrameConstants.P_Type)
                throw new FrameProtocolException("帧长度不足以读取 type 字节。");

            var payload = new byte[frame.Length - FrameConstants.P_Type];
            frame.Slice(FrameConstants.P_Type).Span.CopyTo(payload);
            return new Envelope { Type = frame.Span[0], Payload = payload };
        }

        /// <summary>只写 body，不写包头；类型码由帧编码器负责写入。</summary>
        /// <param name="message">待编码的信封对象，必须是 <see cref="Envelope"/>。</param>
        /// <param name="writer">body 的输出目标。</param>
        public void Encode(object message, IBufferWriter<byte> writer)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            var envelope = message as Envelope;
            if (envelope == null)
                throw new InvalidOperationException("EnvelopeCodec 只接受 Envelope。");
            if (envelope.Payload != null)
                writer.Write(envelope.Payload);
        }
    }
}