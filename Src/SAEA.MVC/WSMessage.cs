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
 *命名空间：SAEA.MVC
 *文件名： WSMessage
 *描述：WebSocket 业务消息
 *
 *****************************************************************************/
using System;
using System.Text;

namespace SAEA.MVC
{
    /// <summary>
    /// WebSocket 消息类型
    /// </summary>
    public enum WSMessageType
    {
        /// <summary>
        /// 文本消息
        /// </summary>
        Text = 1,

        /// <summary>
        /// 二进制消息
        /// </summary>
        Binary = 2
    }

    /// <summary>
    /// WebSocket 业务消息。分片已在解码层重组完成，业务代码不会看到 Continuation。
    /// </summary>
    public sealed class WSMessage
    {
        /// <summary>
        /// 消息类型
        /// </summary>
        public WSMessageType Type { get; }

        /// <summary>
        /// 是否为文本消息
        /// </summary>
        public bool IsText => Type == WSMessageType.Text;

        /// <summary>
        /// 文本内容（仅文本消息有值）
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// 原始字节内容
        /// </summary>
        public byte[] Data { get; }

        internal WSMessage(WSMessageType type, byte[] data)
        {
            Type = type;
            Data = data ?? Array.Empty<byte>();
            if (type == WSMessageType.Text)
                Text = Encoding.UTF8.GetString(Data);
        }
    }
}