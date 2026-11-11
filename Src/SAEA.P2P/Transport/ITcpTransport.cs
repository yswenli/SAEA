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
*命名空间：SAEA.P2P.Transport
*文件名： ITcpTransport
*版本号： v26.11.5.1
*唯一标识：e67e16d8-6993-4915-902d-b1a20e2869a5
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 12:10:00
*描述：TCP 传输契约
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 12:10:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TCP 传输契约
*
*****************************************************************************/
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.P2P.Transport
{
    /// <summary>
    /// 面向连接的字节流传输契约。连接侧负责主动连接，监听侧负责接受连接；
    /// 一个实例只承担其中一侧，另一侧的方法调用会抛 <see cref="NotSupportedException"/>。
    /// </summary>
    public interface ITcpTransport : ITransport
    {
        /// <summary>
        /// 字节流；分帧由上层约定（业务帧、RFC 4571 等），本层不保留消息边界
        /// </summary>
        Stream Stream { get; }

        /// <summary>
        /// 监听侧接受新连接时触发；订阅后应只在事件里取用传输对象，不要再调用 <see cref="AcceptAsync"/>
        /// </summary>
        event Action<ITcpTransport> Accepted;

        /// <summary>
        /// 连接侧连接到远端端点；监听侧不支持
        /// </summary>
        /// <param name="remoteEndPoint">目标端点</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>连接建立并拿到字节流后完成的任务</returns>
        Task ConnectAsync(IPEndPoint remoteEndPoint, CancellationToken cancellationToken);

        /// <summary>
        /// 监听侧等待下一个已接受的连接；连接侧不支持
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>已建立的连接侧传输对象</returns>
        Task<ITcpTransport> AcceptAsync(CancellationToken cancellationToken);
    }
}