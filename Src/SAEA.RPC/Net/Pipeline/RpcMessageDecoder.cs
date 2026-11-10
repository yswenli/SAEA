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
*文件名： RpcMessageDecoder
*版本号： v26.11.5.1
*唯一标识：2eeac507-a43f-4432-907d-bbfbceb5668b
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 15:00:00
*描述：RPC 入站解码器，把线格式帧解成 RSocketMsg
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 15:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增，RPC 帧解码为 RSocketMsg
*
*****************************************************************************/
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using SAEA.RPC.Model;
using SAEA.RPC.Net;
using SAEA.Sockets.Pipeline;

namespace SAEA.RPC
{
    /// <summary>
    /// RPC 入站解码器：把拆帧器产出的完整帧
    /// <c>[1B type][4B total][8B sequence][4B sLen][service][4B mLen][method][data]</c>
    /// 解成 <see cref="RSocketMsg"/>。
    /// <para>
    /// 帧体复制成独立数组后再交给下游，消息生命周期不依赖池化帧。
    /// </para>
    /// </summary>
    public sealed class RpcMessageDecoder : ByteToMessageDecoder
    {
        /// <summary>固定字段长度：4B total + 8B sequence + 4B sLen + 4B mLen。</summary>
        private const uint FixedTotalLength = 20;

        /// <summary>
        /// 解一帧：校验长度字段与实际帧长一致后逐字段还原消息。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="frame">拆帧器切好的完整帧，基类不释放。</param>
        /// <param name="output">本次解码要按序派发的消息列表。</param>
        protected override void Decode(IChannelHandlerContext ctx, PooledMessage frame, List<object> output)
        {
            var bytes = frame.Frame;
            if (bytes.Length < RpcFrameSplitter.HeaderLength)
                throw new FrameProtocolException("RPC 帧长度不足帧头:" + bytes.Length);

            var span = bytes.Span;
            var type = span[0];
            var total = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(1, 4));

            // total 至少覆盖固定字段，防止错位数据把后续长度字段解释成负数。
            if (total < FixedTotalLength)
                throw new FrameProtocolException("非法的RPC帧长度:" + total);

            // 拆帧器按长度字段切帧，这里只做一致性校验，防止拼接错误被当成合法帧投递。
            if (span.Length != 1L + total)
                throw new FrameProtocolException(
                    "RPC帧长度字段与实际帧长不一致:" + total + "/" + (span.Length - 1));

            var offset = RpcFrameSplitter.HeaderLength;

            if (offset + 8 > span.Length)
                throw new FrameProtocolException("RPC帧缺少序号字段:" + span.Length);

            var sequenceNumber = BinaryPrimitives.ReadInt64LittleEndian(span.Slice(offset, 8));
            offset += 8;

            if (offset + 4 > span.Length)
                throw new FrameProtocolException("RPC帧缺少服务名长度字段:" + span.Length);

            var serviceLength = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4));
            offset += 4;

            if (serviceLength > (uint)(span.Length - offset))
                throw new FrameProtocolException("RPC帧服务名长度越界:" + serviceLength);

            var serviceName = serviceLength == 0
                ? string.Empty
                : Encoding.UTF8.GetString(span.Slice(offset, (int)serviceLength).ToArray());
            offset += (int)serviceLength;

            if (offset + 4 > span.Length)
                throw new FrameProtocolException("RPC帧缺少方法名长度字段:" + span.Length);

            var methodLength = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4));
            offset += 4;

            if (methodLength > (uint)(span.Length - offset))
                throw new FrameProtocolException("RPC帧方法名长度越界:" + methodLength);

            var methodName = methodLength == 0
                ? string.Empty
                : Encoding.UTF8.GetString(span.Slice(offset, (int)methodLength).ToArray());
            offset += (int)methodLength;

            var dataLength = (long)total - (offset - 1);
            if (dataLength < 0)
                throw new FrameProtocolException("RPC帧数据长度越界:" + dataLength);

            var msg = new RSocketMsg((RSocketMsgType)type)
            {
                Total = total,
                SequenceNumber = sequenceNumber,
                SLen = serviceLength,
                ServiceName = serviceName,
                MLen = methodLength,
                MethodName = methodName
            };

            if (dataLength > 0)
            {
                // 帧体复制成独立数组，下游可跨回调持有，不受池化帧归还影响。
                msg.Data = span.Slice(offset, (int)dataLength).ToArray();
                msg.IsPooled = false;
            }

            output.Add(msg);
        }
    }
}