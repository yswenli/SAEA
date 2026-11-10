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
*文件名： IChannelHandler
*版本号： v26.11.5.1
*唯一标识：07b74745-e316-4b07-b85b-46a2a6f08b11
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：通道处理器的基础契约
*
*=====================================================================
*修改标记
*修改时间：2026/11/9 10:48:29
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增挂链/摘链回调与出入站活动通知
*
*****************************************************************************/
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

        /// <summary>
        /// 挂上链之前同步调用，用于登记 ctx、绑定连接级状态。抛出时本次添加失败，节点不会挂链。
        /// 此时节点尚未链接，禁止传播消息、禁止写出、禁止读取 <see cref="IChannelHandlerContext.Channel"/>；
        /// 需要连接或回包的初始化放到 <see cref="IChannelInboundHandler.ChannelActiveAsync"/>。
        /// </summary>
        /// <param name="ctx">即将挂链的节点上下文；生命周期内与节点绑定。</param>
        void HandlerAdded(IChannelHandlerContext ctx);

        /// <summary>
        /// 从链上摘除之后同步调用，用于释放连接级状态、注销登记。异常只记录，不影响摘除结果。
        /// 只允许释放连接级状态，不得释放与当前消息相关的缓冲：摘链时上游处理器的调用可能仍在栈上。
        /// </summary>
        /// <param name="ctx">已摘链节点的上下文；读连接标识与连接仍然有效。</param>
        void HandlerRemoved(IChannelHandlerContext ctx);
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
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="msg">已解帧的消息；所有权归链，处理期间有效。</param>
        /// <returns>消息处理完成后结束的任务。</returns>
        Task ChannelReadAsync(IChannelHandlerContext ctx, object msg);

        /// <summary>
        /// 异常沿链传播的入口。默认实现继续向链尾传递；
        /// 位于链尾的处理器负责记录并按异常类型决定是否关闭连接。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="ex">待传播的异常。</param>
        /// <returns>异常处理完成后结束的任务。</returns>
        Task ChannelExceptionCaughtAsync(IChannelHandlerContext ctx, Exception ex);

        /// <summary>
        /// 一轮 Socket 读取的所有帧均已入队后的通知，一轮只触发一次，
        /// 不代表这些消息已经执行完成。处理器内只应做轻量同步工作（攒批、记指标），
        /// 禁止阻塞或业务 IO，否则会拖慢该连接的读取。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <returns>通知处理完成后结束的任务。</returns>
        Task ChannelReadCompleteAsync(IChannelHandlerContext ctx);

        /// <summary>
        /// 连接已启动、接收泵首次读取之前按链首到链尾逐条调用；此时允许回包。
        /// 必须幂等，重复通知不做第二次业务动作；抛出的异常只记录，不阻止后续处理器与接收泵启动。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <returns>连接级初始化完成后结束的任务。</returns>
        Task ChannelActiveAsync(IChannelHandlerContext ctx);

        /// <summary>
        /// 连接进入收口时、按断开策略处理在途消息之前调用；此时 <see cref="IChannel.IsActive"/> 已为 false，
        /// 只做通知与登记摘除，禁止回包。必须幂等；抛出的异常只记录，不阻断收口。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <returns>连接级清理完成后结束的任务。</returns>
        Task ChannelInactiveAsync(IChannelHandlerContext ctx);
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
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="msg">待发送消息；转发时所有权随链移交。</param>
        /// <returns>消息写入下一跳后结束的任务。</returns>
        Task WriteAsync(IChannelHandlerContext ctx, object msg);
    }
}