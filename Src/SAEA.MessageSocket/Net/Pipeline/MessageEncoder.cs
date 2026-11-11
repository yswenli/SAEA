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
*文件名： MessageEncoder
*版本号： v26.11.5.1
*唯一标识：3a3b6daf-fc63-4e30-acf3-68f7c0478f61
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 10:00:00
*描述：消息出站编码器，把 ChatMessage 编码成线格式原始帧
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 10:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增，消息编码为线格式帧
*
*****************************************************************************/
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Threading.Tasks;
using SAEA.Common.Caching;
using SAEA.Common.Serialization;
using SAEA.MessageSocket.Model.Communication;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;

namespace SAEA.MessageSocket
{
    /// <summary>
    /// 消息出站编码器：把 <see cref="ChatMessage"/> 编码成线格式帧 <see cref="RawFrame"/>。
    /// <para>
    /// 出站前缀节点，必须装在 <see cref="OutboundSerializingHandler"/> 左侧（更靠近链首），
    /// 编码得到的帧自带池化缓冲所有权，随帧移交给传输层后由传输层归还。
    /// </para>
    /// </summary>
    public sealed class MessageEncoder : OutboundHandlerAdapter
    {
        /// <summary>
        /// 编码一次出站写：业务消息写成 <c>[8B bodyLength][1B type][body]</c>，
        /// 已经是原始帧的消息（心跳）原样透传。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="msg">待发送消息，只接受 <see cref="ChatMessage"/> 与 <see cref="RawFrame"/>。</param>
        /// <returns>下游处理完成后结束的任务。</returns>
        public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
        {
            var chatMessage = msg as ChatMessage;
            if (chatMessage != null)
            {
                // protobuf 序列化只能做一次：先序列化拿到长度，再一次性写入帧头与帧体。
                var body = SerializeHelper.PBSerialize(chatMessage) ?? new byte[0];

                var frameLength = (long)FrameConstants.P_Head + body.Length;
                if (frameLength > int.MaxValue)
                    throw new InvalidOperationException("消息长度超出可编码上限:" + frameLength + "。");

                var writer = new PooledBufferWriter((int)frameLength);
                try
                {
                    WriteFrame(writer, body);
                    return ctx.WriteAsync(new RawFrame(writer.WrittenMemory, writer));
                }
                catch
                {
                    // 只在移交前失败时归还；成功交出后所有权随帧走，不能再由本节点释放。
                    writer.Dispose();
                    throw;
                }
            }

            if (msg is RawFrame)
                return ctx.WriteAsync(msg);

            throw new InvalidOperationException(
                "MessageEncoder 只接受 ChatMessage 或 RawFrame，实际为 "
                + (msg == null ? "(null)" : msg.GetType().FullName) + "。");
        }

        /// <summary>
        /// 把消息编码成独立字节数组，供不接链的离线场景使用。
        /// </summary>
        /// <param name="msg">待编码的消息，为 null 抛 ArgumentNullException</param>
        /// <returns>完整的消息线格式字节</returns>
        public static byte[] Encode(ChatMessage msg)
        {
            if (msg == null)
                throw new ArgumentNullException(nameof(msg));

            var body = SerializeHelper.PBSerialize(msg) ?? new byte[0];

            var writer = new PooledBufferWriter(FrameConstants.P_Head + body.Length);
            try
            {
                WriteFrame(writer, body);
                return writer.WrittenSpan.ToArray();
            }
            finally
            {
                writer.Dispose();
            }
        }

        /// <summary>
        /// 按消息线格式把已序列化的帧体写入缓冲写入器。
        /// </summary>
        /// <param name="writer">目标缓冲写入器，为 null 抛 ArgumentNullException</param>
        /// <param name="body">已序列化的帧体，为 null 抛 ArgumentNullException</param>
        private static void WriteFrame(IBufferWriter<byte> writer, byte[] body)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));
            if (body == null)
                throw new ArgumentNullException(nameof(body));

            // 帧体是 protobuf 编码后的业务消息；长度字段只计帧体，不含 9 字节帧头。
            var frameLength = FrameConstants.P_Head + body.Length;

            var span = writer.GetSpan(frameLength);

            BinaryPrimitives.WriteInt64LittleEndian(span, body.Length);
            span[FrameConstants.P_LEN] = (byte)SocketProtocalType.ChatMessage;

            if (body.Length > 0)
                new ReadOnlySpan<byte>(body).CopyTo(span.Slice(FrameConstants.P_Head, body.Length));

            writer.Advance(frameLength);
        }
    }
}