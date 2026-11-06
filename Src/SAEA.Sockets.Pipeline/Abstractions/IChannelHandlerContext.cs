using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 处理器在链上的节点上下文。context 跨消息复用，
    /// 不得把单条消息的状态（包括取消令牌）存放在 context 上。
    /// </summary>
    public interface IChannelHandlerContext
    {
        /// <summary>连接标识，与 <see cref="IChannel.Id"/> 相同。</summary>
        string ChannelId { get; }

        /// <summary>所属连接。</summary>
        IChannel Channel { get; }

        /// <summary>所属通道链。</summary>
        IChannelPipeline Pipeline { get; }

        /// <summary>当前节点对应的处理器实例。</summary>
        IChannelHandler Handler { get; }

        /// <summary>
        /// 当前消息的执行令牌：连接进入收口或该消息超时时取消。
        /// 取消只是协作式请求，不会强行终止线程；处理器内所有可取消的等待都应传入它。
        /// 消息执行之外读取到的是连接级令牌。
        /// </summary>
        CancellationToken CancellationToken { get; }

        /// <summary>
        /// 把消息传给下一个入站处理器。必须等待返回的 Task；
        /// 处理器不得在返回后继续使用消息，除非已显式持有其所有权。
        /// </summary>
        Task FireChannelReadAsync(object msg);

        /// <summary>
        /// 把消息交给下一个出站处理器（向链首方向）。必须等待返回的 Task；
        /// 出站异常原样冒泡，不得记录后返回成功。
        /// </summary>
        Task WriteAsync(object msg);

        /// <summary>从当前节点之后向链尾传播异常，不会绕回当前处理器自身。</summary>
        Task FireExceptionCaughtAsync(System.Exception ex);
    }
}