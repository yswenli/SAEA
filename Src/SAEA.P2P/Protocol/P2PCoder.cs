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
 *修改时间：2026/04/20 15:44:01
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：P2PCoder编解码类
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
    public class P2PCoder : BaseCoder
    {
        /// <summary>有状态解码（保留半包缓存）；BigData/File 对 P2P 静默丢弃。</summary>
        public DecodedFrames DecodeP2P(ReadOnlySequence<byte> data, Action<DateTime> onHeart = null)
        {
            return Decode(data, onHeart);
        }

        public DecodedFrames DecodeP2P(byte[] data, Action<DateTime> onHeart = null)
        {
            return Decode(new ReadOnlySequence<byte>(data ?? Array.Empty<byte>()), onHeart);
        }

        /// <summary>有状态解码（Span 直投，省去中间数组拷贝）；BigData/File 对 P2P 静默丢弃。</summary>
        public DecodedFrames DecodeP2P(ReadOnlySpan<byte> data, Action<DateTime> onHeart = null)
        {
            return DecodeSpan(data, onHeart);
        }

        public byte[] EncodeP2P(P2PMessageType messageType)
        {
            return EncodeToArray(P2PProtocol.Create(messageType));
        }

        public byte[] EncodeP2P(P2PMessageType messageType, byte[] content)
        {
            return EncodeToArray(P2PProtocol.Create(messageType, content));
        }

        public byte[] EncodeP2P(P2PMessageType messageType, string content)
        {
            return EncodeToArray(P2PProtocol.Create(messageType, content));
        }

        /// <summary>零拷贝重载。</summary>
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

        public static P2PMessageType GetP2PMessageType(byte[] data)
        {
            if (data == null || data.Length < P_LEN + 1)
                throw new ArgumentException("Data length is insufficient");
            return (P2PMessageType)data[P_LEN];
        }
    }
}