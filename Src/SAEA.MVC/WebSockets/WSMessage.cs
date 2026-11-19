/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.MVC.WebSockets
*文件名： WSMessage
*版本号： v26.11.5.1
*唯一标识：187f2359-b9ed-4b0e-9b29-b5160a2ab441
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/5 11:56:30
*描述：WebSocket 业务消息
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 11:29:23
*修改人： yswenli
*版本号： v26.11.16.1
*描述：随 WebSocket 实现迁入 WebSockets 目录并改用命名空间 SAEA.MVC.WebSockets，无逻辑变更
*
*****************************************************************************/
using System;
using System.Text;

namespace SAEA.MVC.WebSockets
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