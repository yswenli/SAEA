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