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
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：处理器在链上的节点上下文
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
        /// <param name="msg">待传递给下一个入站处理器的消息。</param>
        /// <returns>下游处理完成后结束的任务。</returns>
        Task FireChannelReadAsync(object msg);

        /// <summary>
        /// 把消息交给下一个出站处理器（向链首方向）。必须等待返回的 Task；
        /// 出站异常原样冒泡，不得记录后返回成功。
        /// </summary>
        /// <param name="msg">待发送消息。</param>
        /// <returns>下一个出站处理器处理完成后结束的任务。</returns>
        Task WriteAsync(object msg);

        /// <summary>从当前节点之后向链尾传播异常，不会绕回当前处理器自身。</summary>
        /// <param name="ex">待传播的异常。</param>
        /// <returns>异常处理完成后结束的任务。</returns>
        Task FireExceptionCaughtAsync(System.Exception ex);
    }
}