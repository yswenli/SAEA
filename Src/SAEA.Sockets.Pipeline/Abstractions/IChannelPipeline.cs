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
*文件名： IChannelPipeline
*版本号： v26.11.5.1
*唯一标识：0d69decf-5664-43d2-a366-6c350b29481f
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：单条连接独占的双向处理器链
*
*=====================================================================
*修改标记
*修改时间：2026/11/9 11:39:22
*修改人： yswenli
*版本号： v26.11.5.1
*描述：业务写入口成为唯一写入路径，并与先写后关共用关闭状态机
*
*****************************************************************************/
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
        /// <param name="handler">待追加的处理器实例；不可共享的实例只能挂到一条连接上。</param>
        /// <returns>当前链，便于链式调用。</returns>
        IChannelPipeline AddLast(IChannelHandler handler);

        /// <summary>把处理器插入链首，返回当前链本身以便链式调用。</summary>
        /// <param name="handler">待插入的处理器实例。</param>
        /// <returns>当前链，便于链式调用。</returns>
        IChannelPipeline AddFirst(IChannelHandler handler);

        /// <summary>从链上摘除指定处理器实例并归还注册计数。</summary>
        /// <param name="handler">待摘除的处理器实例；不在链上时不抛异常。</param>
        /// <returns>当前链，便于链式调用。</returns>
        IChannelPipeline Remove(IChannelHandler handler);

        /// <summary>当前链上处理器的快照，用于校验与日志；遍历期间链可安全修改。</summary>
        IReadOnlyList<IChannelHandler> Handlers { get; }

        /// <summary>
        /// 入站唯一入口，只允许接收泵/执行器调用。处理器内部往下游传递
        /// 必须使用 <see cref="IChannelHandlerContext.FireChannelReadAsync"/>。
        /// </summary>
        /// <param name="msg">待处理的入站消息。</param>
        /// <returns>整条入站链执行完成后结束的任务。</returns>
        Task FireChannelReadAsync(object msg);

        /// <summary>每轮读取至多一次，按链首到链尾的顺序只通知入站处理器。</summary>
        /// <returns>所有入站处理器通知完成后结束的任务。</returns>
        Task FireChannelReadCompleteAsync();

        /// <summary>从链首开始向链尾传播异常。</summary>
        /// <param name="ex">待传播的异常。</param>
        /// <returns>异常处理完成后结束的任务。</returns>
        Task FireExceptionCaughtAsync(Exception ex);

        /// <summary>
        /// 业务写入口：从出站前缀最右侧节点启动一次发送，先排队再编码，保证同一连接的写顺序。
        /// 不得在出站处理器回调栈内发起并等待新的业务写，否则会排在当前写之后造成自死锁；
        /// 当前写加工只能使用 <see cref="IChannelHandlerContext.WriteAsync"/>。
        /// </summary>
        /// <param name="msg">待发送的业务消息；所有权随链移交。</param>
        /// <returns>该次写完成、失败或取消后结束的任务。</returns>
        Task WriteAsync(object msg);

        /// <summary>
        /// 请求关闭连接，幂等。返回只表示已进入收口流程，不表示资源回收完成；
        /// 收口在后台继续执行，不会等待调用方自身。
        /// </summary>
        /// <param name="reason">关闭原因，写入日志用于区分收口来源。</param>
        /// <returns>进入收口流程后即完成的任务，不等待资源回收。</returns>
        Task CloseAsync(string reason);
    }
}