/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.RPC.Tests.Tests.Support
*文件名： RpcFrameBuilder
*版本号： net10.0
*唯一标识：6ddab1b2-78a3-42bc-92cc-2b69e65e66cb
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 17:05:00
*描述：按 RPC 线格式拼装测试用原始帧
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 17:05:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，按 RPC 线格式拼装测试用原始帧
*
*****************************************************************************/
using System;
using System.Buffers.Binary;
using System.Text;
using SAEA.RPC.Model;
using SAEA.RPC.Net;

namespace SAEA.RPC.Tests.Tests.Support
{
    /// <summary>
    /// RPC 线格式 <c>[1B type][4B total 小端][8B sequence][4B sLen][service][4B mLen][method][data]</c>
    /// 的拼装器，只用于测试输入。
    /// </summary>
    public static class RpcFrameBuilder
    {
        /// <summary>RPC 帧固定头部长度：类型 + 总长度字段。</summary>
        public const int HeaderLength = 5;

        /// <summary>
        /// 用产品编码器把消息编成真实线格式字节。
        /// </summary>
        /// <param name="msg">待编码消息。</param>
        /// <returns>完整帧字节。</returns>
        public static byte[] Build(RSocketMsg msg)
        {
            return RpcMessageEncoder.Encode(msg);
        }

        /// <summary>
        /// 按给定字段拼出一帧，允许写入与内容不一致的 <paramref name="total"/> 与长度字段，用于边界与非法帧用例。
        /// </summary>
        /// <param name="type">消息类型字节。</param>
        /// <param name="total">写入长度字段的值。</param>
        /// <param name="sequence">请求序号。</param>
        /// <param name="serviceName">服务名，null 视为空串。</param>
        /// <param name="serviceLength">写入服务名长度字段的值，不传时按实际字节数。</param>
        /// <param name="methodName">方法名，null 视为空串。</param>
        /// <param name="methodLength">写入方法名长度字段的值，不传时按实际字节数。</param>
        /// <param name="data">数据段，可为 null。</param>
        /// <returns>完整帧字节；帧总长恒为 <c>1 + 实际内容长度</c>，与 <paramref name="total"/> 无关。</returns>
        public static byte[] BuildRaw(
            byte type,
            uint total,
            long sequence,
            string serviceName,
            string methodName,
            byte[] data,
            uint? serviceLength = null,
            uint? methodLength = null)
        {
            var serviceBytes = string.IsNullOrEmpty(serviceName) ? new byte[0] : Encoding.UTF8.GetBytes(serviceName);
            var methodBytes = string.IsNullOrEmpty(methodName) ? new byte[0] : Encoding.UTF8.GetBytes(methodName);
            var payload = data ?? new byte[0];

            var sLen = serviceLength ?? (uint)serviceBytes.Length;
            var mLen = methodLength ?? (uint)methodBytes.Length;

            var frame = new byte[HeaderLength + 8 + 4 + serviceBytes.Length + 4 + methodBytes.Length + payload.Length];

            var span = frame.AsSpan();
            span[0] = type;
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(1, 4), total);
            BinaryPrimitives.WriteInt64LittleEndian(span.Slice(5, 8), sequence);
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(13, 4), sLen);

            var offset = 17;
            serviceBytes.CopyTo(span.Slice(offset));
            offset += serviceBytes.Length;

            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(offset, 4), mLen);
            offset += 4;

            methodBytes.CopyTo(span.Slice(offset));
            offset += methodBytes.Length;

            payload.CopyTo(span.Slice(offset));

            return frame;
        }

        /// <summary>
        /// 拼出一段指定长度的可预测数据段：第 i 个字节为 <c>(byte)i</c>。
        /// </summary>
        /// <param name="length">数据段长度。</param>
        /// <returns>填充后的数据段。</returns>
        public static byte[] Payload(int length)
        {
            var body = new byte[length];
            for (var i = 0; i < length; i++)
                body[i] = (byte)i;

            return body;
        }

        /// <summary>
        /// 构造一条请求消息。
        /// </summary>
        /// <param name="sequence">请求序号。</param>
        /// <param name="serviceName">服务名。</param>
        /// <param name="methodName">方法名。</param>
        /// <param name="data">数据段，可为 null。</param>
        /// <returns>请求消息实例。</returns>
        public static RSocketMsg Request(long sequence, string serviceName, string methodName, byte[] data)
        {
            return new RSocketMsg(RSocketMsgType.Request, serviceName, methodName, data)
            {
                SequenceNumber = sequence
            };
        }

        /// <summary>
        /// 构造一条响应消息。
        /// </summary>
        /// <param name="sequence">请求序号。</param>
        /// <param name="data">数据段，可为 null。</param>
        /// <returns>响应消息实例。</returns>
        public static RSocketMsg Response(long sequence, byte[] data)
        {
            return new RSocketMsg(RSocketMsgType.Response, null, null, data)
            {
                SequenceNumber = sequence
            };
        }

        /// <summary>
        /// 构造一条心跳消息。
        /// </summary>
        /// <returns>心跳消息实例。</returns>
        public static RSocketMsg Ping()
        {
            return new RSocketMsg(RSocketMsgType.Ping);
        }
    }
}