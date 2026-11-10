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
*命名空间：SAEA.RPC
*文件名： RpcMessageEncoder
*版本号： v26.11.5.1
*唯一标识：18612124-e3d7-4655-a323-3e7773208ff4
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 15:00:00
*描述：RPC 出站编码器，把 RSocketMsg 编码成 RawFrame
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 15:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增，RPC 消息编码为线格式帧
*
*****************************************************************************/
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using System.Threading.Tasks;
using SAEA.Common.Caching;
using SAEA.RPC.Net;
using SAEA.Sockets.Pipeline;

namespace SAEA.RPC
{
    /// <summary>
    /// RPC 出站编码器：把 <see cref="RSocketMsg"/> 编码成线格式帧 <see cref="RawFrame"/>。
    /// <para>
    /// 出站前缀节点，必须装在 <see cref="OutboundSerializingHandler"/> 左侧（更靠近链首），
    /// 编码得到的帧自带池化缓冲所有权，随帧移交给传输层后由传输层归还。
    /// </para>
    /// </summary>
    public sealed class RpcMessageEncoder : OutboundHandlerAdapter
    {
        /// <summary>
        /// 编码一次出站写：协议对象写成 <c>[1B type][4B total][8B sequence][4B sLen][service][4B mLen][method][data]</c>，
        /// 已经是原始帧的消息（如有）原样透传。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="msg">待发送消息，只接受 <see cref="RSocketMsg"/> 与 <see cref="RawFrame"/>。</param>
        /// <returns>下游处理完成后结束的任务。</returns>
        public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
        {
            var rpcMsg = msg as RSocketMsg;
            if (rpcMsg != null)
            {
                var capacity = GetEncodedLength(rpcMsg);

                var writer = new PooledBufferWriter(capacity);
                try
                {
                    EncodeTo(writer, rpcMsg);
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
                "RpcMessageEncoder 只接受 RSocketMsg 或 RawFrame，实际为 "
                + (msg == null ? "(null)" : msg.GetType().FullName) + "。");
        }

        /// <summary>
        /// 把消息编码成独立字节数组，供兼容入口 <see cref="Net.RpcCoder.Encode(RSocketMsg)"/> 使用。
        /// </summary>
        /// <param name="msg">待编码的消息，为 null 抛 ArgumentNullException</param>
        /// <returns>完整的 RPC 线格式字节</returns>
        public static byte[] Encode(RSocketMsg msg)
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
        /// 按 RPC 线格式把一个消息写入缓冲写入器。
        /// </summary>
        /// <param name="writer">目标缓冲写入器，为 null 抛 ArgumentNullException</param>
        /// <param name="msg">待编码的消息，为 null 抛 ArgumentNullException</param>
        public static void EncodeTo(IBufferWriter<byte> writer, RSocketMsg msg)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));
            if (msg == null)
                throw new ArgumentNullException(nameof(msg));

            var serviceBytes = string.IsNullOrEmpty(msg.ServiceName)
                ? null
                : Encoding.UTF8.GetBytes(msg.ServiceName);
            var methodBytes = string.IsNullOrEmpty(msg.MethodName)
                ? null
                : Encoding.UTF8.GetBytes(msg.MethodName);

            var serviceLength = serviceBytes == null ? 0 : serviceBytes.Length;
            var methodLength = methodBytes == null ? 0 : methodBytes.Length;
            var dataLength = msg.Data == null ? 0 : msg.Data.Length;

            // total 不含类型字节，但包含 4B 长度字段自身及其后全部字段。
            var total = 4L + 8L + 4L + serviceLength + 4L + methodLength + dataLength;
            if (total > int.MaxValue - 1)
                throw new InvalidOperationException("RPC 消息长度超出可编码上限:" + total + "。");

            var frameLength = (int)total + 1;
            var span = writer.GetSpan(frameLength);

            span[0] = msg.Type;
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(1, 4), (uint)total);
            BinaryPrimitives.WriteInt64LittleEndian(span.Slice(5, 8), msg.SequenceNumber);
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(13, 4), (uint)serviceLength);

            var offset = 17;
            if (serviceLength > 0)
            {
                new ReadOnlySpan<byte>(serviceBytes).CopyTo(span.Slice(offset, serviceLength));
                offset += serviceLength;
            }

            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(offset, 4), (uint)methodLength);
            offset += 4;

            if (methodLength > 0)
            {
                new ReadOnlySpan<byte>(methodBytes).CopyTo(span.Slice(offset, methodLength));
                offset += methodLength;
            }

            if (dataLength > 0)
            {
                new ReadOnlySpan<byte>(msg.Data).CopyTo(span.Slice(offset, dataLength));
                offset += dataLength;
            }

            writer.Advance(offset);
        }

        /// <summary>
        /// 计算编码后的整帧字节数。
        /// </summary>
        /// <param name="msg">待编码的消息，为 null 抛 ArgumentNullException</param>
        /// <returns>含类型字节的整帧长度</returns>
        private static int GetEncodedLength(RSocketMsg msg)
        {
            if (msg == null)
                throw new ArgumentNullException(nameof(msg));

            var serviceLength = string.IsNullOrEmpty(msg.ServiceName)
                ? 0
                : Encoding.UTF8.GetByteCount(msg.ServiceName);
            var methodLength = string.IsNullOrEmpty(msg.MethodName)
                ? 0
                : Encoding.UTF8.GetByteCount(msg.MethodName);
            var dataLength = msg.Data == null ? 0 : msg.Data.Length;

            var total = 1L + 4L + 8L + 4L + serviceLength + 4L + methodLength + dataLength;
            if (total > int.MaxValue)
                throw new InvalidOperationException("RPC 消息长度超出可编码上限:" + total + "。");

            return (int)total;
        }
    }
}