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
        public FrameProtocolException(string message)
            : base(message)
        {
        }

        /// <summary>创建带说明与内部异常的协议异常。</summary>
        public FrameProtocolException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}