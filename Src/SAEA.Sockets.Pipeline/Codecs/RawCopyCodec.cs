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
*文件名： RawCopyCodec
*版本号： v26.11.5.1
*唯一标识：f894e00d-8637-4141-a2b0-0539f2c594e9
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：调试与基准用 codec：把帧拷贝成信封，不做任何解析
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：调试与基准用 codec：把帧拷贝成信封，不做任何解析
*
*****************************************************************************/
using System;
using System.Buffers;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 调试与基准用 codec：把帧拷贝成信封，不做任何解析。
    /// 与零拷贝透传不同，它返回独立数组，不与池化帧体共享所有权。
    /// </summary>
    public sealed class RawCopyCodec : ICodec
    {
        /// <summary>首字节仍作为类型码，其余字节精确拷贝为 payload。</summary>
        public object Decode(ReadOnlyMemory<byte> frame)
        {
            if (frame.Length < FrameConstants.P_Type)
                throw new FrameProtocolException("帧长度不足以读取 type 字节。");

            var payload = new byte[frame.Length - FrameConstants.P_Type];
            frame.Slice(FrameConstants.P_Type).Span.CopyTo(payload);
            return new Envelope { Type = frame.Span[0], Payload = payload };
        }

        /// <summary>只写 body，不写包头。</summary>
        public void Encode(object message, IBufferWriter<byte> writer)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            var envelope = message as Envelope;
            if (envelope == null)
                throw new InvalidOperationException("RawCopyCodec 只接受 Envelope。");
            if (envelope.Payload != null)
                writer.Write(envelope.Payload);
        }
    }
}