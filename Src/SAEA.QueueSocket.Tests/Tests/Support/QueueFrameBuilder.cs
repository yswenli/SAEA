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
*命名空间：SAEA.QueueSocket.Tests.Tests.Support
*文件名： QueueFrameBuilder
*版本号： net10.0
*唯一标识：7d2b9c41-63ae-4f18-8c05-b1e94a7d3f66
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 20:40:00
*描述：队列线格式帧拼装器，只用于测试输入
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 20:40:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，队列线格式帧拼装器
*
*****************************************************************************/
using System;
using System.Buffers.Binary;
using System.Text;
using SAEA.QueueSocket.Net;
using SAEA.QueueSocket.Type;

namespace SAEA.QueueSocket.Tests.Tests.Support
{
    /// <summary>
    /// 队列线格式 <c>[1B type][4B total 小端][4B nLen][name][4B tLen][topic][data]</c>
    /// 的拼装器，只用于测试输入。
    /// </summary>
    public static class QueueFrameBuilder
    {
        /// <summary>队列帧固定头部长度：类型 + 总长度字段。</summary>
        public const int HeaderLength = 5;

        /// <summary>长度字段之后的固定字段总长：4B 名称长度 + 4B 主题长度。</summary>
        public const int FixedTotalLength = 12;

        /// <summary>
        /// 用产品编码器把消息编成真实线格式字节。
        /// </summary>
        /// <param name="msg">待编码消息。</param>
        /// <returns>完整帧字节。</returns>
        public static byte[] Build(QueueSocketMsg msg)
        {
            return QueueMessageEncoder.Encode(msg);
        }

        /// <summary>
        /// 按给定字段拼出一帧，帧物理长度恒为 <c>1 + total</c>，允许长度字段与内容不一致，
        /// 用于边界与非法帧用例。
        /// </summary>
        /// <param name="type">消息类型字节。</param>
        /// <param name="total">写入长度字段的值，不含类型字节。</param>
        /// <param name="name">发送方名称，null 视为空。</param>
        /// <param name="topic">主题，null 视为空。</param>
        /// <param name="data">数据段，可为 null。</param>
        /// <param name="nameLength">写入名称长度字段的值，不传时按实际字节数。</param>
        /// <param name="topicLength">写入主题长度字段的值，不传时按实际字节数。</param>
        /// <returns>完整帧字节；放不下的字段不写入，交给解码器报越界。</returns>
        public static byte[] BuildRaw(
            byte type,
            int total,
            string name,
            string topic,
            byte[] data,
            int? nameLength = null,
            int? topicLength = null)
        {
            var nameBytes = string.IsNullOrEmpty(name) ? new byte[0] : Encoding.UTF8.GetBytes(name);
            var topicBytes = string.IsNullOrEmpty(topic) ? new byte[0] : Encoding.UTF8.GetBytes(topic);
            var payload = data ?? new byte[0];

            var frame = new byte[1 + total];
            var span = frame.AsSpan();

            span[0] = type;
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(1, 4), total);

            var nLen = nameLength ?? nameBytes.Length;
            var tLen = topicLength ?? topicBytes.Length;

            var offset = HeaderLength;
            if (offset + 4 > span.Length)
                return frame;

            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset, 4), nLen);
            offset += 4;

            if (nLen > 0)
            {
                // 长度字段与实际内容不一致时不再写入，留给解码器按越界处理。
                if (nameBytes.Length != nLen || offset + nLen > span.Length)
                    return frame;

                nameBytes.CopyTo(span.Slice(offset));
                offset += nLen;
            }

            if (offset + 4 > span.Length)
                return frame;

            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset, 4), tLen);
            offset += 4;

            if (tLen > 0)
            {
                if (topicBytes.Length != tLen || offset + tLen > span.Length)
                    return frame;

                topicBytes.CopyTo(span.Slice(offset));
                offset += tLen;
            }

            if (payload.Length > 0 && offset + payload.Length <= span.Length)
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
        /// 构造一条发布消息。
        /// </summary>
        /// <param name="name">发送方名称。</param>
        /// <param name="topic">主题。</param>
        /// <param name="data">数据段，可为 null。</param>
        /// <returns>发布消息实例。</returns>
        public static QueueSocketMsg Publish(string name, string topic, byte[] data)
        {
            return new QueueSocketMsg(QueueSocketMsgType.Publish, name, topic, data);
        }

        /// <summary>
        /// 构造一条心跳消息，名称、主题与数据均为空。
        /// </summary>
        /// <returns>心跳消息实例。</returns>
        public static QueueSocketMsg Ping()
        {
            return new QueueSocketMsg(QueueSocketMsgType.Ping);
        }
    }
}