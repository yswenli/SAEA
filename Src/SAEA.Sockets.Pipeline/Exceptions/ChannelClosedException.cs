using System;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 连接已关闭或已进入收口，仍尝试写数据时抛出。
    /// </summary>
    public sealed class ChannelClosedException : Exception
    {
        /// <summary>创建带说明的已关闭异常。</summary>
        public ChannelClosedException(string message)
            : base(message)
        {
        }

        /// <summary>创建带说明与内部异常的已关闭异常。</summary>
        public ChannelClosedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}