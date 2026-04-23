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
*文件名： P2PProtocol
*版本号： v26.4.23.1
*唯一标识：fb75edcb-d15a-4990-98ac-8b2124b190c0
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 15:44:01
*描述：P2PProtocol协议类
*
*=====================================================================
*修改标记
*修改时间：2026/04/20 15:44:01
*修改人： yswenli
*版本号： v26.4.23.1
*描述：P2PProtocol协议类
*
*****************************************************************************/
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