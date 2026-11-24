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
*命名空间：SAEA.Sockets.Pipeline
*文件名： FrameProtocolException
*版本号： v26.11.5.1
*唯一标识：b9b4d984-103b-4f40-8326-f26f38b35ded
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：分帧或协议级错误：帧长非法、超过上限等
*
*=====================================================================
*修改标记
*修改时间：2026/11/24 16:54:47
*修改人： yswenli
*版本号： v26.11.24.1
*描述：新增 UnreadBodyBytes，供 413 收口计算有界排空字节数
*
*****************************************************************************/
using System;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 分帧或协议级错误：帧长非法、超过上限等。
    /// 由接收泵捕获后按断开收口处理。
    /// </summary>
    public sealed class FrameProtocolException : Exception
    {
        /// <summary>创建带说明的协议异常。</summary>
        /// <param name="message">异常说明。</param>
        public FrameProtocolException(string message)
            : base(message)
        {
        }

        /// <summary>创建带说明与内部异常的协议异常。</summary>
        /// <param name="message">异常说明。</param>
        /// <param name="innerException">引发本次协议错误的原始异常。</param>
        public FrameProtocolException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// 判定协议错误时尚未到达的请求体字节数；-1 表示分帧器无法推断剩余长度（例如 chunked），
        /// 0 表示请求体已经全部到达。仅 413 收口用于计算有界排空，不参与异常语义。
        /// </summary>
        public long UnreadBodyBytes { get; set; } = -1;
    }
}