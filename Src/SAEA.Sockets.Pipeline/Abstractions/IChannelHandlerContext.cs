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
*文件名： IChannelHandlerContext
*版本号： v26.11.5.1
*唯一标识：ff2319ca-87db-433d-8736-4585b3122ad0
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：处理器在链上的节点上下文
*
*=====================================================================
*修改标记
*修改时间：2026/11/9 11:39:22
*修改人： yswenli
*版本号： v26.11.5.1
*描述：明确 ctx.WriteAsync 仅限出站回调，先写后关在出站回调内拒绝
*
*****************************************************************************/
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
        /// <summary>
        /// 连接标识；尚未接入传输层或处于 <see cref="IChannelHandler.HandlerAdded"/> 阶段时为 null。
        /// </summary>
        string ChannelId { get; }

        /// <summary>
        /// 所属连接。处理器处于 <see cref="IChannelHandler.HandlerAdded"/> 阶段时节点尚未挂链，读取会抛
        /// <see cref="System.InvalidOperationException"/>；需要连接或回包的初始化应放到
        /// <see cref="IChannelInboundHandler.ChannelActiveAsync"/>。
        /// </summary>
        IChannel Channel { get; }

        /// <summary>
        /// 所属通道链。出站处理器回调内返回同操作守卫视图，禁止通过它发起独立业务写；
        /// 入站与连接级回调返回真实链。
        /// </summary>
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
        /// <param name="msg">待传递给下一个入站处理器的消息。</param>
        /// <returns>下游处理完成后结束的任务。</returns>
        Task FireChannelReadAsync(object msg);

        /// <summary>
        /// 把当前写消息交给下一个出站处理器（向链首方向），沿用本次业务写操作，不新增计数或占用额度。
        /// 只在出站处理器的 <see cref="IChannelOutboundHandler.WriteAsync"/> 回调内可用；
        /// 回调外调用、视图失效或同一节点对同一次写重复转发都抛 <see cref="System.InvalidOperationException"/>。
        /// 必须等待返回的 Task；出站异常原样冒泡，不得记录后返回成功。
        /// </summary>
        /// <param name="msg">待发送消息。</param>
        /// <returns>下一个出站处理器处理完成后结束的任务。</returns>
        Task WriteAsync(object msg);

        /// <summary>从当前节点之后向链尾传播异常，不会绕回当前处理器自身。</summary>
        /// <param name="ex">待传播的异常。</param>
        /// <returns>异常处理完成后结束的任务。</returns>
        Task FireExceptionCaughtAsync(System.Exception ex);

        /// <summary>
        /// 等本连接已提交的出站写全部交接给传输层后再收口；连接已关闭时为 no-op。
        /// 等待上限复用 <see cref="ChannelPipelineOptions.DrainTimeout"/>，零值按 5 秒处理，超时强制收口。
        /// 禁止在出站 <see cref="WriteAsync"/> 的回调栈内调用，否则当前写会自等。
        /// </summary>
        /// <param name="reason">关闭原因，写入日志用于区分收口来源。</param>
        /// <returns>已提交的写全部交接或等待超时后结束的任务。</returns>
        Task CloseAfterWriteAsync(string reason);
    }
}