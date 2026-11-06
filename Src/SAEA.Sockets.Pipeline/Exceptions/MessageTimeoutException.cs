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
*文件名： MessageTimeoutException
*版本号： v26.11.5.1
*唯一标识：8a670f61-cf1f-4f52-9132-6eec20c47337
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：单条消息执行超时
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：单条消息执行超时
*
*****************************************************************************/
using System;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 单条消息执行超时。只影响该消息，不代表连接需要关闭。
    /// </summary>
    public sealed class MessageTimeoutException : Exception
    {
        /// <summary>发生超时的连接标识。</summary>
        public string ChannelId { get; }

        /// <summary>该消息配置的超时时长。</summary>
        public TimeSpan Timeout { get; }

        /// <summary>按连接标识与超时时长创建异常。</summary>
        public MessageTimeoutException(string channelId, TimeSpan timeout)
            : base("消息处理超时：channel=" + channelId + "，timeout=" + timeout + "。")
        {
            ChannelId = channelId;
            Timeout = timeout;
        }
    }
}