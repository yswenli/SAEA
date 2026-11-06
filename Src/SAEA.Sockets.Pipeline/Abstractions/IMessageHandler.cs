using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 业务消息处理器：由消息分发器按消息的精确 CLR 类型装配并调用。
    /// </summary>
    public interface IMessageHandler
    {
        /// <summary>
        /// 处理一条业务消息。<paramref name="ctx"/> 是该消息所在链节点的上下文，
        /// 回包必须使用它，不要另建指向链首的上下文。
        /// </summary>
        Task HandleAsync(IChannelHandlerContext ctx, object message);
    }
}