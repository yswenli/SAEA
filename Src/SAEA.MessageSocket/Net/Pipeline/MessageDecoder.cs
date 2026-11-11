/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MessageSocket
*文件名： MessageDecoder
*版本号： v26.11.5.1
*唯一标识：c7875bb7-cddb-42a7-b4f4-15f8567c23e2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 10:00:00
*描述：消息入站解码器，把完整帧反序列化成 ChatMessage
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 10:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增，解码器只做反序列化
*
*****************************************************************************/
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using SAEA.Common.Serialization;
using SAEA.MessageSocket.Model.Communication;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;

namespace SAEA.MessageSocket
{
    /// <summary>
    /// 消息入站解码器：把拆帧器产出的完整帧 <c>[8B bodyLength][1B type][body]</c>
    /// 反序列化成 <see cref="ChatMessage"/>。
    /// <para>
    /// 帧体复制成独立数组后再交给下游，消息生命周期不依赖池化帧。
    /// 心跳帧只用于保活，不产出业务消息。
    /// </para>
    /// </summary>
    public sealed class MessageDecoder : ByteToMessageDecoder
    {
        /// <summary>
        /// 解一帧：校验长度字段与实际帧长一致后反序列化帧体。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="frame">拆帧器切好的完整帧，基类不释放。</param>
        /// <param name="output">本次解码要按序派发的消息列表。</param>
        protected override void Decode(IChannelHandlerContext ctx, PooledMessage frame, List<object> output)
        {
            var span = frame.Frame.Span;

            if (span.Length < FrameConstants.P_Head)
                throw new FrameProtocolException("非法的消息帧长度:" + span.Length);

            var bodyLength = BinaryPrimitives.ReadInt64LittleEndian(span);

            if (bodyLength < 0)
                throw new FrameProtocolException("非法的消息帧体长度:" + bodyLength);

            // 拆帧器按长度字段切帧，这里只做一致性校验，防止拼接错误被当成合法帧投递。
            if (bodyLength != span.Length - FrameConstants.P_Head)
                throw new FrameProtocolException(
                    "消息帧长度字段与实际帧长不一致:" + bodyLength + "/" + (span.Length - FrameConstants.P_Head));

            var type = span[FrameConstants.P_LEN];

            // 心跳帧没有业务内容，接收方只要完成一次读取即可确认对端存活。
            if (type == (byte)SocketProtocalType.Heart)
                return;

            if (bodyLength == 0)
                return;

            var body = span.Slice(FrameConstants.P_Head, (int)bodyLength).ToArray();

            ChatMessage message;
            try
            {
                message = SerializeHelper.PBDeserialize<ChatMessage>(body);
            }
            catch (Exception ex)
            {
                throw new FrameProtocolException("消息帧反序列化失败，type:" + type, ex);
            }

            if (message != null)
                output.Add(message);
        }
    }
}