namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 处理器基类：默认不可跨连接共享。业务处理器应继承入站/出站适配器，
    /// 而不是直接实现接口，避免为无关成员填写样板代码。
    /// </summary>
    public abstract class ChannelHandlerAdapter : IChannelHandler
    {
        /// <summary>默认不可跨连接共享；无状态处理器可重写为 true。</summary>
        public virtual bool IsSharable
        {
            get { return false; }
        }
    }
}