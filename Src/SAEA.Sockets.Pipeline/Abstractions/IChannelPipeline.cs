using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 单条连接独占的双向处理器链。实例不跨连接复用。
    /// </summary>
    public interface IChannelPipeline : IDisposable
    {
        /// <summary>把处理器追加到链尾，返回当前链本身以便链式调用。</summary>
        IChannelPipeline AddLast(IChannelHandler handler);

        /// <summary>把处理器插入链首，返回当前链本身以便链式调用。</summary>
        IChannelPipeline AddFirst(IChannelHandler handler);

        /// <summary>从链上摘除指定处理器实例并归还注册计数。</summary>
        IChannelPipeline Remove(IChannelHandler handler);

        /// <summary>当前链上处理器的快照，用于校验与日志；遍历期间链可安全修改。</summary>
        IReadOnlyList<IChannelHandler> Handlers { get; }

        /// <summary>
        /// 入站唯一入口，只允许接收泵/执行器调用。处理器内部往下游传递
        /// 必须使用 <see cref="IChannelHandlerContext.FireChannelReadAsync"/>。
        /// </summary>
        Task FireChannelReadAsync(object msg);

        /// <summary>每轮读取至多一次，按链首到链尾的顺序只通知入站处理器。</summary>
        Task FireChannelReadCompleteAsync();

        /// <summary>从链首开始向链尾传播异常。</summary>
        Task FireExceptionCaughtAsync(Exception ex);

        /// <summary>
        /// 请求关闭连接，幂等。返回只表示已进入收口流程，不表示资源回收完成；
        /// 收口在后台继续执行，不会等待调用方自身。
        /// </summary>
        Task CloseAsync(string reason);
    }
}