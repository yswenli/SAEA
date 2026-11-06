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
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：单条连接独占的双向处理器链
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