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