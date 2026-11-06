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
 *命名空间：SAEA.P2P.Protocol
 *文件名： P2PCoder
 *版本号： v26.4.23.1
 *唯一标识：f38aa876-5936-46d9-8bfc-cbadd8b9aeae
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/04/20 15:44:01
 *描述：P2PCoder编解码类
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/6 12:20:16
 *修改人： yswenli
 *版本号： v26.11.5.1
 *描述：补全公开成员的 XML 注释
 *
 *****************************************************************************/
using System;
using System.Buffers;
using SAEA.Common.Caching;
using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

namespace SAEA.P2P.Protocol
{
    /// <summary>
    /// P2P 帧编解码器，沿用 SAEA.Sockets 的「8 字节小端长度前缀 + 1 字节类型」帧格式
    /// </summary>
    public class P2PCoder : BaseCoder
    {
        /// <summary>
        /// 有状态解码，保留半包缓存；BigData 与 File 帧对 P2P 静默丢弃
        /// </summary>
        /// <param name="data">待解码的连续字节序列，未消费完的半包留在解码器缓存中</param>
        /// <param name="onHeart">心跳帧回调，可为 null</param>
        /// <returns>解码出的帧批次，须 using 释放；帧内容仅在批次释放前有效</returns>
        public DecodedFrames DecodeP2P(ReadOnlySequence<byte> data, Action<DateTime> onHeart = null)
        {
            return Decode(data, onHeart);
        }

        /// <summary>
        /// 有状态解码，保留半包缓存；null 按空数据处理
        /// </summary>
        /// <param name="data">待解码的原始字节，可为 null</param>
        /// <param name="onHeart">心跳帧回调，可为 null</param>
        /// <returns>解码出的帧批次，须 using 释放；帧内容仅在批次释放前有效</returns>
        public DecodedFrames DecodeP2P(byte[] data, Action<DateTime> onHeart = null)
        {
            return Decode(new ReadOnlySequence<byte>(data ?? Array.Empty<byte>()), onHeart);
        }

        /// <summary>
        /// 有状态解码，Span 直投省去中间数组拷贝；BigData 与 File 帧对 P2P 静默丢弃
        /// </summary>
        /// <param name="data">待解码的字节跨度，未消费完的半包留在解码器缓存中</param>
        /// <param name="onHeart">心跳帧回调，可为 null</param>
        /// <returns>解码出的帧批次，须 using 释放；帧内容仅在批次释放前有效</returns>
        public DecodedFrames DecodeP2P(ReadOnlySpan<byte> data, Action<DateTime> onHeart = null)
        {
            return DecodeSpan(data, onHeart);
        }

        /// <summary>
        /// 按消息类型编码为完整帧，无帧体
        /// </summary>
        /// <param name="messageType">消息类型</param>
        /// <returns>编码结果，含 8 字节小端长度前缀与 1 字节类型</returns>
        public byte[] EncodeP2P(P2PMessageType messageType)
        {
            return EncodeToArray(P2PProtocol.Create(messageType));
        }

        /// <summary>
        /// 按消息类型与字节负载编码为完整帧
        /// </summary>
        /// <param name="messageType">消息类型</param>
        /// <param name="content">帧体负载，null 按空帧体处理</param>
        /// <returns>编码结果，含 8 字节小端长度前缀与 1 字节类型</returns>
        public byte[] EncodeP2P(P2PMessageType messageType, byte[] content)
        {
            return EncodeToArray(P2PProtocol.Create(messageType, content));
        }

        /// <summary>
        /// 按消息类型与 UTF-8 文本负载编码为完整帧
        /// </summary>
        /// <param name="messageType">消息类型</param>
        /// <param name="content">文本负载，null 或空串按空帧体处理</param>
        /// <returns>编码结果，含 8 字节小端长度前缀与 1 字节类型</returns>
        public byte[] EncodeP2P(P2PMessageType messageType, string content)
        {
            return EncodeToArray(P2PProtocol.Create(messageType, content));
        }

        /// <summary>
        /// 零拷贝编码：把帧头与帧体直接写入调用方的 buffer writer，帧体为空
        /// </summary>
        /// <param name="messageType">消息类型</param>
        /// <param name="writer">写入目标，由调用方负责刷新与归还</param>
        public void EncodeP2P(P2PMessageType messageType, IBufferWriter<byte> writer)
        {
            Encode(P2PProtocol.Create(messageType), writer);
        }

        private byte[] EncodeToArray(ISocketProtocal protocal)
        {
            using (var writer = new PooledBufferWriter(64))
            {
                Encode(protocal, writer);
                return writer.WrittenSpan.ToArray();
            }
        }

        /// <summary>
        /// 直接读取帧头中的消息类型字节，不做数值合法性校验；数据为 null 或不足 9 字节时抛 ArgumentException
        /// </summary>
        /// <param name="data">完整帧数据，长度至少为 8 字节长度前缀加 1 字节类型</param>
        /// <returns>帧头第 9 字节对应的消息类型</returns>
        public static P2PMessageType GetP2PMessageType(byte[] data)
        {
            if (data == null || data.Length < P_LEN + 1)
                throw new ArgumentException("Data length is insufficient");
            return (P2PMessageType)data[P_LEN];
        }
    }
}