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
*唯一标识：84df156a-3f95-43da-802a-243117081271
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/4/20 15:44:01
*描述：P2P协议编解码器，处理消息的编码和解码
*
*=====================================================================
*修改标记
*修改时间：2026/4/20 15:44:01
*修改人： yswenli
*版本号： v26.4.23.1
*描述：P2P协议编解码器，处理消息的编码和解码
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

namespace SAEA.P2P.Protocol
{
    public class P2PCoder : BaseCoder
    {
        public List<P2PProtocol> DecodeP2P(ReadOnlySpan<byte> data, Action<DateTime> onHeart = null)
        {
            var result = new List<P2PProtocol>();
            var protocols = Decode(data, onHeart);
            
            foreach (var protocol in protocols)
            {
                var p2pProtocol = new P2PProtocol
                {
                    BodyLength = protocol.BodyLength,
                    Type = protocol.Type,
                    Content = protocol.Content
                };
                result.Add(p2pProtocol);
            }
            
            return result;
        }
        
        public List<P2PProtocol> DecodeP2P(byte[] data, Action<DateTime> onHeart = null)
        {
            return DecodeP2P(data.AsSpan(), onHeart);
        }
        
        public byte[] EncodeP2P(P2PMessageType messageType)
        {
            var protocol = P2PProtocol.Create(messageType);
            return Encode(protocol);
        }
        
        public byte[] EncodeP2P(P2PMessageType messageType, byte[] content)
        {
            var protocol = P2PProtocol.Create(messageType, content);
            return Encode(protocol);
        }
        
        public byte[] EncodeP2P(P2PMessageType messageType, string content)
        {
            var protocol = P2PProtocol.Create(messageType, content);
            return Encode(protocol);
        }
        
        public static P2PMessageType GetP2PMessageType(byte[] data)
        {
            if (data == null || data.Length < P_LEN + 1)
            {
                throw new ArgumentException("Data length is insufficient");
            }
            
            return (P2PMessageType)data[P_LEN];
        }
    }
}