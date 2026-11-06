using System;
using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 通道处理器的基础契约。所有处理器都挂在每条连接独占的通道链上。
    /// </summary>
    public interface IChannelHandler
    {
        /// <summary>
        /// 实例可跨连接共享时返回 true。默认实现返回 false；
        /// 无连接状态的处理器才能声明为 true，否则连接之间会互相污染状态。
        /// </summary>
        bool IsSharable { get; }
    }

    /// <summary>
    /// 入站处理器：消息沿链从链首向链尾传播。
    /// </summary>
    public interface IChannelInboundHandler : IChannelHandler
    {
        /// <summary>
        /// 处理一条已解帧的消息。往下游传递必须使用
        /// <see cref="IChannelHandlerContext.FireChannelReadAsync"/> 并等待返回的 Task；
        /// 不等待下游等价于提前放行，可能破坏消息顺序。
        /// 处理器不得在调用返回后继续使用消息，除非先显式持有其所有权。
        /// </summary>
        Task ChannelReadAsync(IChannelHandlerContext ctx, object msg);

        /// <summary>
        /// 异常沿链传播的入口。默认实现继续向链尾传递；
        /// 位于链尾的处理器负责记录并按异常类型决定是否关闭连接。
        /// </summary>
        Task ChannelExceptionCaughtAsync(IChannelHandlerContext ctx, Exception ex);

        /// <summary>
        /// 一轮 Socket 读取的所有帧均已入队后的通知，一轮只触发一次，
        /// 不代表这些消息已经执行完成。处理器内只应做轻量同步工作（攒批、记指标），
        /// 禁止阻塞或业务 IO，否则会拖慢该连接的读取。
        /// </summary>
        Task ChannelReadCompleteAsync(IChannelHandlerContext ctx);
    }

    /// <summary>
    /// 出站处理器：写操作沿链从链尾向链首传播。
    /// </summary>
    public interface IChannelOutboundHandler : IChannelHandler
    {
        /// <summary>
        /// 处理一条待发送消息。继续向链首传递必须调用
        /// <see cref="IChannelHandlerContext.WriteAsync"/> 并等待；不转发即视为截断出站链。
        /// 出站异常不得被吞掉，必须原样冒泡给发起方。
        /// </summary>
        Task WriteAsync(IChannelHandlerContext ctx, object msg);
    }
}