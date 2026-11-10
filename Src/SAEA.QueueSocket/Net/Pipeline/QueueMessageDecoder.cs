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
*文件名： QueueMessageDecoder
*版本号： v26.11.5.1
*唯一标识：7b963ebf-3a78-429a-b1b5-6de82675bdbd
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 20:10:00
*描述：QueueSocket 入站解码器
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 20:10:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增，QueueSocket 入站解码器
*
*****************************************************************************/
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using SAEA.Common.Caching;
using SAEA.QueueSocket.Model;
using SAEA.QueueSocket.Type;
using SAEA.Sockets.Pipeline;

namespace SAEA.QueueSocket
{
    /// <summary>
    /// QueueSocket 入站解码器：把拆帧器产出的完整帧
    /// <c>[1B type][4B total][4B nameLen][name][4B topicLen][topic][data]</c>
    /// 解成 <see cref="QueueMsg"/>。
    /// <para>
    /// 帧体复制成独立池化缓冲后再交给下游，消息生命周期不依赖池化帧；
    /// 下游处理完必须把消息归还 <see cref="QueueMsgPool"/>，否则缓冲不会释放。
    /// </para>
    /// </summary>
    public sealed class QueueMessageDecoder : ByteToMessageDecoder
    {
        /// <summary>固定字段长度：4B total + 4B nameLen + 4B topicLen。</summary>
        private const int FixedTotalLength = 12;

        /// <summary>
        /// 解一帧：校验长度字段与实际帧长一致后逐字段还原消息。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="frame">拆帧器切好的完整帧，基类不释放。</param>
        /// <param name="output">本次解码要按序派发的消息列表。</param>
        protected override void Decode(IChannelHandlerContext ctx, PooledMessage frame, List<object> output)
        {
            var bytes = frame.Frame;
            if (bytes.Length < QueueFrameSplitter.HeaderLength)
                throw new FrameProtocolException("队列帧长度不足帧头:" + bytes.Length);

            var span = bytes.Span;
            var typeValue = span[0];
            if (typeValue < (byte)QueueSocketMsgType.Ping || typeValue > (byte)QueueSocketMsgType.Data)
                throw new FrameProtocolException("非法的队列消息类型:" + typeValue);

            var total = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(1, 4));

            // total 至少覆盖固定字段，防止错位数据把后续长度字段解释成负数。
            if (total < FixedTotalLength)
                throw new FrameProtocolException("非法的队列帧长度:" + total);

            // 拆帧器按长度字段切帧，这里只做一致性校验，防止拼接错误被当成合法帧投递。
            if (span.Length != 1L + total)
                throw new FrameProtocolException(
                    "队列帧长度字段与实际帧长不一致:" + total + "/" + (span.Length - 1));

            var offset = QueueFrameSplitter.HeaderLength;

            var nameLength = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4));
            offset += 4;
            if (nameLength < 0 || nameLength > span.Length - offset)
                throw new FrameProtocolException("队列帧名称长度越界:" + nameLength);

            var name = nameLength == 0
                ? null
                : Encoding.UTF8.GetString(span.Slice(offset, nameLength).ToArray());
            offset += nameLength;

            if (offset + 4 > span.Length)
                throw new FrameProtocolException("队列帧缺少主题长度字段:" + span.Length);

            var topicLength = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4));
            offset += 4;
            if (topicLength < 0 || topicLength > span.Length - offset)
                throw new FrameProtocolException("队列帧主题长度越界:" + topicLength);

            var topic = topicLength == 0
                ? null
                : Encoding.UTF8.GetString(span.Slice(offset, topicLength).ToArray());
            offset += topicLength;

            var dataLength = span.Length - offset;
            if (dataLength < 0)
                throw new FrameProtocolException("队列帧数据长度越界:" + dataLength);

            var msg = QueueMsgPool.Rent();
            msg.Type = (QueueSocketMsgType)typeValue;
            msg.Name = name;
            msg.Topic = topic;

            if (dataLength > 0)
            {
                var pool = MemoryPoolManager.RentPooled(dataLength);
                span.Slice(offset, dataLength).CopyTo(pool.Buffer);
                msg.SetOwner(pool);
            }
            else
            {
                msg.Data = ReadOnlyMemory<byte>.Empty;
            }

            output.Add(msg);
        }
    }
}