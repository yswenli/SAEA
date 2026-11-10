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
*命名空间：SAEA.QueueSocket
*文件名： QueueMessageEncoder
*版本号： v26.11.5.1
*唯一标识：094e625a-c88c-4c6a-aa13-143336484474
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 20:10:00
*描述：QueueSocket 出站编码器与线格式写入器
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 20:10:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增，QueueSocket 出站编码器与线格式写入器
*
*****************************************************************************/
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using System.Threading.Tasks;
using SAEA.Common.Caching;
using SAEA.QueueSocket.Net;
using SAEA.QueueSocket.Type;
using SAEA.Sockets.Pipeline;

namespace SAEA.QueueSocket
{
    /// <summary>
    /// QueueSocket 出站编码器：把 <see cref="QueueSocketMsg"/> 编码成线格式帧 <see cref="RawFrame"/>。
    /// <para>
    /// 出站前缀节点，必须装在 <see cref="OutboundSerializingHandler"/> 左侧（更靠近链首），
    /// 编码得到的帧自带池化缓冲所有权，随帧移交给传输层后由传输层归还。
    /// </para>
    /// </summary>
    public sealed class QueueMessageEncoder : OutboundHandlerAdapter
    {
        /// <summary>
        /// 编码一次出站写：协议对象写成 <c>[1B type][4B total][4B nameLen][name][4B topicLen][topic][data]</c>，
        /// 已经是原始帧的消息（批量投递）原样透传。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="msg">待发送消息，只接受 <see cref="QueueSocketMsg"/> 与 <see cref="RawFrame"/>。</param>
        /// <returns>下游处理完成后结束的任务。</returns>
        public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
        {
            var queueMsg = msg as QueueSocketMsg;
            if (queueMsg != null)
            {
                var writer = new PooledBufferWriter(GetEncodedLength(queueMsg));
                try
                {
                    EncodeTo(writer, queueMsg);
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
                "QueueMessageEncoder 只接受 QueueSocketMsg 或 RawFrame，实际为 "
                + (msg == null ? "(null)" : msg.GetType().FullName) + "。");
        }

        /// <summary>
        /// 把消息编码成独立字节数组，供兼容入口 <see cref="Net.QueueCoder.Encode(QueueSocketMsg)"/> 使用。
        /// </summary>
        /// <param name="msg">待编码的消息，为 null 抛 ArgumentNullException</param>
        /// <returns>完整的一帧字节</returns>
        public static byte[] Encode(QueueSocketMsg msg)
        {
            if (msg == null)
                throw new ArgumentNullException(nameof(msg));

            var writer = new PooledBufferWriter(GetEncodedLength(msg));
            try
            {
                EncodeTo(writer, msg);
                return writer.WrittenSpan.ToArray();
            }
            finally
            {
                writer.Dispose();
            }
        }

        /// <summary>
        /// 按 QueueSocket 线格式把一条消息写入缓冲写入器。
        /// </summary>
        /// <param name="writer">目标缓冲写入器，为 null 抛 ArgumentNullException</param>
        /// <param name="msg">待编码的消息，为 null 抛 ArgumentNullException</param>
        public static void EncodeTo(IBufferWriter<byte> writer, QueueSocketMsg msg)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));
            if (msg == null)
                throw new ArgumentNullException(nameof(msg));

            var nameBytes = string.IsNullOrEmpty(msg.Name) ? null : Encoding.UTF8.GetBytes(msg.Name);
            var topicBytes = string.IsNullOrEmpty(msg.Topic) ? null : Encoding.UTF8.GetBytes(msg.Topic);

            WriteFrameTo(writer, msg.Type, nameBytes, topicBytes, msg.Data.Span);
        }

        /// <summary>
        /// 按 QueueSocket 线格式把一帧写入缓冲写入器，名称与主题字节由调用方缓存复用，
        /// 供批量组帧与客户端发布路径避免重复编码。
        /// </summary>
        /// <param name="writer">目标缓冲写入器，为 null 抛 ArgumentNullException</param>
        /// <param name="type">消息类型</param>
        /// <param name="nameBytes">已编码的名称，为 null 时按空处理</param>
        /// <param name="topicBytes">已编码的主题，为 null 时按空处理</param>
        /// <param name="data">数据</param>
        internal static void WriteFrameTo(IBufferWriter<byte> writer, QueueSocketMsgType type, byte[] nameBytes, byte[] topicBytes, ReadOnlySpan<byte> data)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            var nlen = nameBytes == null ? 0 : nameBytes.Length;
            var tlen = topicBytes == null ? 0 : topicBytes.Length;
            var dlen = data.Length;

            // total 不含类型字节，但包含 4B 长度字段自身及其后全部字段。
            var total = 12 + nlen + tlen + dlen;

            var span = writer.GetSpan(1 + total);
            var offset = 0;
            span[offset++] = (byte)type;
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset, 4), total);
            offset += 4;
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset, 4), nlen);
            offset += 4;
            if (nlen > 0)
            {
                nameBytes.AsSpan().CopyTo(span.Slice(offset));
                offset += nlen;
            }
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset, 4), tlen);
            offset += 4;
            if (tlen > 0)
            {
                topicBytes.AsSpan().CopyTo(span.Slice(offset));
                offset += tlen;
            }
            if (dlen > 0)
            {
                data.CopyTo(span.Slice(offset));
                offset += dlen;
            }
            writer.Advance(offset);
        }

        /// <summary>
        /// 计算编码后的整帧字节数。
        /// </summary>
        /// <param name="msg">待编码的消息，为 null 抛 ArgumentNullException</param>
        /// <returns>含类型字节的整帧长度</returns>
        private static int GetEncodedLength(QueueSocketMsg msg)
        {
            if (msg == null)
                throw new ArgumentNullException(nameof(msg));

            var nameLength = string.IsNullOrEmpty(msg.Name) ? 0 : Encoding.UTF8.GetByteCount(msg.Name);
            var topicLength = string.IsNullOrEmpty(msg.Topic) ? 0 : Encoding.UTF8.GetByteCount(msg.Topic);
            var dataLength = msg.Data.Length;

            var total = 1L + 12L + nameLength + topicLength + dataLength;
            if (total > int.MaxValue)
                throw new InvalidOperationException("QueueSocket 消息长度超出可编码上限:" + total + "。");

            return (int)total;
        }
    }
}