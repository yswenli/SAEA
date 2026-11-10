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
*命名空间：SAEA.FileSocket
*文件名： FileMessageDecoder
*版本号： v26.11.5.1
*唯一标识：9984bd48-612b-4ee7-be07-1c87f62333af
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 11:20:00
*描述：文件传输入站解码器
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 11:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增，把完整帧解成文件传输协议对象
*
*****************************************************************************/
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using SAEA.Sockets.Base;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;

namespace SAEA.FileSocket
{
    /// <summary>
    /// 文件传输入站解码器：把拆帧器产出的完整帧 <c>[8B bodyLength][1B type][body]</c>
    /// 解成 <see cref="BaseSocketProtocal"/>。
    /// <para>
    /// 帧体复制成独立数组后再交给下游，消息生命周期不依赖池化帧；
    /// 心跳帧（类型 Heart 且帧体为空）按既有语义在此丢弃，不产生业务消息。
    /// </para>
    /// </summary>
    public sealed class FileMessageDecoder : ByteToMessageDecoder
    {
        /// <summary>
        /// 解一帧：校验帧头与帧体长度一致，非心跳帧产出协议对象。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="frame">拆帧器切好的完整帧，基类不释放。</param>
        /// <param name="output">本次解码要按序派发的消息列表。</param>
        protected override void Decode(IChannelHandlerContext ctx, PooledMessage frame, List<object> output)
        {
            var bytes = frame.Frame;
            if (bytes.Length < FrameConstants.P_Head)
                throw new FrameProtocolException("文件帧长度不足帧头:" + bytes.Length);

            var span = bytes.Span;
            var bodyLength = BinaryPrimitives.ReadInt64LittleEndian(span.Slice(0, FrameConstants.P_LEN));
            var type = span[FrameConstants.P_LEN];

            // 拆帧器按长度字段切帧，这里只做一致性校验，防止拼接错误被当成合法帧投递。
            if (bodyLength != bytes.Length - FrameConstants.P_Head)
                throw new FrameProtocolException(
                    "文件帧长度字段与实际帧体不一致:" + bodyLength + "/" + (bytes.Length - FrameConstants.P_Head));

            if (bodyLength == 0 && type == (byte)SocketProtocalType.Heart)
                return;

            ReadOnlyMemory<byte> content = ReadOnlyMemory<byte>.Empty;
            if (bodyLength > 0)
            {
                // 帧体复制成独立数组，下游可跨回调持有，不受池化帧归还影响。
                content = new ReadOnlyMemory<byte>(bytes.Slice(FrameConstants.P_Head).ToArray());
            }

            output.Add(new BaseSocketProtocal(bodyLength, type, content));
        }
    }
}