using System.Collections.Generic;
using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;

namespace SAEA.P2P.Protocol
{
    public class P2PProtocol : BaseSocketProtocal
    {
        public P2PProtocol()
        {
        }
        
        public P2PProtocol(P2PMessageType messageType, byte[] content)
        {
            Type = (byte)messageType;
            Content = content;
            BodyLength = content?.Length ?? 0;
        }
        
        public static P2PProtocol Create(P2PMessageType messageType)
        {
            return new P2PProtocol(messageType, null);
        }
        
        public static P2PProtocol Create(P2PMessageType messageType, byte[] content)
        {
            return new P2PProtocol(messageType, content);
        }
        
        public static P2PProtocol Create(P2PMessageType messageType, string content)
        {
            byte[] data = null;
            if (!string.IsNullOrEmpty(content))
            {
                data = System.Text.Encoding.UTF8.GetBytes(content);
            }
            return new P2PProtocol(messageType, data);
        }
        
        public P2PMessageType GetMessageType()
        {
            return (P2PMessageType)Type;
        }
        
        public string GetContentAsString()
        {
            if (Content == null || Content.Length == 0)
            {
                return string.Empty;
            }
            return System.Text.Encoding.UTF8.GetString(Content);
        }
    }
}