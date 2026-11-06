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
 *修改时间：2026/11/6 12:20:16
 *修改人： yswenli
 *版本号： v26.11.5.1
 *描述：补全公开成员的 XML 注释
 *
 *****************************************************************************/
using System;
using SAEA.Sockets.Base;

namespace SAEA.P2P.Protocol
{
    /// <summary>P2P 协议（编码便捷类型；解码统一走 DecodedFrames + BaseSocketProtocal）。</summary>
    public class P2PProtocol : BaseSocketProtocal
    {
        /// <summary>
        /// 创建未设置类型与帧体的协议对象，类型字节为 0、帧体为空
        /// </summary>
        public P2PProtocol() { }

        /// <summary>
        /// 按消息类型与字节帧体创建协议对象
        /// </summary>
        /// <param name="messageType">消息类型，写入帧头类型字节</param>
        /// <param name="content">帧体负载，null 按空帧体处理</param>
        public P2PProtocol(P2PMessageType messageType, byte[] content)
            : base((byte)messageType, content == null ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte>(content))
        {
        }

        /// <summary>
        /// 创建只有消息类型、无帧体的协议对象
        /// </summary>
        /// <param name="messageType">消息类型</param>
        /// <returns>帧体为空的协议对象</returns>
        public static P2PProtocol Create(P2PMessageType messageType)
        {
            return new P2PProtocol(messageType, null);
        }

        /// <summary>
        /// 创建带字节帧体的协议对象
        /// </summary>
        /// <param name="messageType">消息类型</param>
        /// <param name="content">帧体负载，null 按空帧体处理</param>
        /// <returns>协议对象，帧体长度取自 content</returns>
        public static P2PProtocol Create(P2PMessageType messageType, byte[] content)
        {
            return new P2PProtocol(messageType, content);
        }

        /// <summary>
        /// 创建带 UTF-8 文本帧体的协议对象
        /// </summary>
        /// <param name="messageType">消息类型</param>
        /// <param name="content">文本负载，null 或空串按空帧体处理</param>
        /// <returns>协议对象，帧体为文本的 UTF-8 字节</returns>
        public static P2PProtocol Create(P2PMessageType messageType, string content)
        {
            byte[] data = null;
            if (!string.IsNullOrEmpty(content))
                data = System.Text.Encoding.UTF8.GetBytes(content);
            return new P2PProtocol(messageType, data);
        }

        /// <summary>
        /// 读取帧头类型字节并转为 P2P 消息类型，未定义的取值原样返回
        /// </summary>
        /// <returns>帧头中的 P2P 消息类型</returns>
        public P2PMessageType GetMessageType()
        {
            return (P2PMessageType)Type;
        }

        /// <summary>
        /// 按 UTF-8 解码帧体；帧体为空时返回空串
        /// </summary>
        /// <returns>帧体文本，帧体为空时为空串</returns>
        public string GetContentAsString()
        {
            if (Content.Length == 0) return string.Empty;
            return System.Text.Encoding.UTF8.GetString(Content.Span.ToArray());
        }
    }
}