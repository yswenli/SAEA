/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Sockets
*文件名： IClientSocket
*版本号： v26.4.23.1
*唯一标识：11bd0b0e-284f-4da2-bf47-5d10efcaaa3f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/08/22 10:00:37
*描述：IClientSocket接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/9 10:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：携带所有权对象的发送重载改为返回可等待的真实完成 Task
*
*****************************************************************************/
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets
{
    /// <summary>
    /// 客户端
    /// </summary>
    public interface IClientSocket : IDisposable
    {
        /// <summary>
        /// 连接上下文：持有用户令牌与编解码器
        /// </summary>
        IContext<ICoder> Context { get; }

        /// <summary>
        /// 套接字配置选项
        /// </summary>
        ISocketOption SocketOption { get; set; }

        /// <summary>
        /// 端地址
        /// </summary>
        string Endpoint { get; }

        /// <summary>
        /// 底层套接字对象
        /// </summary>
        Socket Socket { get; }

        /// <summary>
        /// 连接
        /// </summary>
        bool Connected { get; }

        /// <summary>
        /// 接收数据事件（Span 版本）。data 仅在回调期间有效，消费方如需跨回调保存必须自行复制。
        /// </summary>
        event OnClientReceiveSpanHandler OnClientReceiveSpan;

        /// <summary>
        /// 断开事件
        /// </summary>
        event OnDisconnectedHandler OnDisconnected;

        /// <summary>
        /// 异常事件
        /// </summary>
        event OnErrorHandler OnError;

        /// <summary>
        /// 连接
        /// </summary>
        void Connect();

        /// <summary>
        /// 指定绑定ip
        /// </summary>
        /// <param name="ipEndPint"></param>
        void Bind(IPEndPoint ipEndPint);
        /// <summary>
        /// 指定绑定ip
        /// </summary>
        /// <param name="ip"></param>
        void Bind(string ip);

        /// <summary>
        /// 连接
        /// </summary>
        /// <param name="callBack"></param>
        void ConnectAsync(Action<SocketError> callBack = null);        

        /// <summary>
        /// 同步发送（Span）。ns2.0 下会租用池化缓冲区拷贝一次后发送。
        /// </summary>
        /// <param name="data">数据</param>
        void Send(ReadOnlySpan<byte> data);

        /// <summary>
        /// saea 发送（Memory）。可用时零拷贝，否则租用池化缓冲区拷贝一次。
        /// </summary>
        /// <param name="data">数据</param>
        void SendAsync(ReadOnlyMemory<byte> data);

        /// <summary>
        /// 携带所有权对象的异步发送：返回的 Task 在本次数据完成底层交接后完成，
        /// 失败（未连接、连接断开、超时等）以异常形式可观察；<paramref name="owner"/>
        /// 由实现方在完成或失败时释放，调用方不得再释放。
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="owner">数据的所有者，发送完成或失败时由实现方释放</param>
        /// <returns>底层交接完成的任务；未连接等同步失败直接抛出，其余失败为故障 Task</returns>
        Task SendAsync(ReadOnlyMemory<byte> data, IDisposable owner);

        /// <summary>
        /// 编码并发送协议对象（零拷贝优先）。
        /// </summary>
        /// <param name="protocal">协议对象</param>
        void SendAsync(ISocketProtocal protocal);

        /// <summary>
        /// 异步流发送（Memory）。
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken);

        /// <summary>
        /// 获取连接的双工流门面（仅 <c>UseStream()</c> 或启用 TLS 时可用）。
        /// </summary>
        /// <returns>连接门面的 <see cref="Stream"/>；未启用流模式与 TLS 时抛 <see cref="InvalidOperationException"/></returns>
        Stream GetStream();        

        /// <summary>
        /// 断开连接
        /// </summary>
        void Disconnect();
    }
}
