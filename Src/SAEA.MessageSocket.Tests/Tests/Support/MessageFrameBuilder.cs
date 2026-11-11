/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MessageSocket.Tests.Tests.Support
*文件名： MessageFrameBuilder
*版本号： net10.0
*唯一标识：8d47c9e1-2f60-4b3a-a17e-9c5b3e8d2f04
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 11:30:00
*描述：消息线格式帧拼装器，只用于测试输入
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 11:30:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，消息线格式帧拼装器
*
*****************************************************************************/
using System;
using System.Buffers.Binary;
using SAEA.MessageSocket.Model.Communication;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;

namespace SAEA.MessageSocket.Tests.Tests.Support
{
    /// <summary>
    /// 消息线格式 <c>[8B bodyLength 小端][1B type][body]</c> 的拼装器，只用于测试输入。
    /// </summary>
    public static class MessageFrameBuilder
    {
        /// <summary>完整帧头长度：8B 长度字段 + 1B 类型。</summary>
        public const int HeaderLength = FrameConstants.P_Head;

        /// <summary>
        /// 用产品编码器把消息编成真实线格式字节。
        /// </summary>
        /// <param name="msg">待编码消息。</param>
        /// <returns>完整帧字节。</returns>
        public static byte[] Build(ChatMessage msg)
        {
            return MessageEncoder.Encode(msg);
        }

        /// <summary>
        /// 按给定字段拼出一帧，帧物理长度恒为 <c>9 + body.Length</c>，
        /// 允许长度字段与实际帧体不一致，用于边界与非法帧用例。
        /// </summary>
        /// <param name="declaredBodyLength">写入长度字段的值，只计帧体。</param>
        /// <param name="type">帧类型字节。</param>
        /// <param name="body">帧体，可为 null。</param>
        /// <returns>完整帧字节。</returns>
        public static byte[] BuildRaw(long declaredBodyLength, byte type, byte[] body)
        {
            var payload = body ?? new byte[0];

            var frame = new byte[HeaderLength + payload.Length];
            var span = frame.AsSpan();

            BinaryPrimitives.WriteInt64LittleEndian(span.Slice(0, FrameConstants.P_LEN), declaredBodyLength);
            span[FrameConstants.P_LEN] = type;

            if (payload.Length > 0)
                payload.CopyTo(span.Slice(HeaderLength));

            return frame;
        }

        /// <summary>
        /// 拼一个心跳帧：长度字段为 0、类型为 <see cref="SocketProtocalType.Heart"/>，无帧体。
        /// </summary>
        /// <returns>完整心跳帧字节。</returns>
        public static byte[] Heartbeat()
        {
            return BuildRaw(0, (byte)SocketProtocalType.Heart, null);
        }

        /// <summary>
        /// 构造一条纯文本消息信封。
        /// </summary>
        /// <param name="type">协议类型。</param>
        /// <param name="content">消息内容。</param>
        /// <returns>消息实例。</returns>
        public static ChatMessage Text(ChatMessageType type, string content)
        {
            return new ChatMessage(type, content);
        }
    }
}