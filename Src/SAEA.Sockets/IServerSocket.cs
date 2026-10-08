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
*文件名： IServerSocket
*版本号： v26.4.23.1
*唯一标识：8b6f5d87-f1bd-415c-ada0-1dff8fc7f24c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/08/22 10:00:37
*描述：IServerSocket接口
*
*=====================================================================
*修改标记
*修改时间：2019/08/22 10:00:37
*修改人： yswenli
*版本号： v26.4.23.1
*描述：IServerSocket接口
*
*****************************************************************************/
using System;
using System.Net;

using SAEA.Sockets.Core;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;

namespace SAEA.Sockets
{
    /// <summary>
    /// 服务器端
    /// </summary>
    public interface IServerSocket : IDisposable
    {
        /// <summary>
        /// 配置项
        /// </summary>
        ISocketOption SocketOption { get; set; }

        /// <summary>
        /// 建立连接事件
        /// </summary>
        event OnAcceptedHandler OnAccepted;

        /// <summary>
        /// 异常事件
        /// </summary>
        event OnErrorHandler OnError;

        /// <summary>
        /// 接收数据事件（Span 版本）。data 仅在回调期间有效。
        /// </summary>
        event OnServerReceiveSpanHandler OnServerReceiveSpan;

        /// <summary>
        /// 客户端断开事件
        /// </summary>
        event OnDisconnectedHandler OnDisconnected;

        /// <summary>
        /// 会话管理
        /// </summary>
        SessionManager SessionManager { get; }

        /// <summary>
        /// 启动
        /// </summary>
        /// <param name="backlog"></param>
        void Start(int backlog = 10 * 1000);

        /// <summary>
        /// 客户端连接数
        /// </summary>
        int ClientCounts { get; }

        /// <summary>
        /// 获取当前UserToken或Channel对象
        /// </summary>
        /// <param name="sessionID"></param>
        /// <returns></returns>
        object GetCurrentObj(string sessionID);

        /// <summary>
        /// 同步发送（Span）。ns2.0 下会租用池化缓冲区拷贝一次后异步发送。
        /// </summary>
        /// <param name="sessionID">会话ID</param>
        /// <param name="data">数据</param>
        void Send(string sessionID, ReadOnlySpan<byte> data);

        /// <summary>
        /// 异步发送（Memory）。可用时零拷贝，否则租用池化缓冲区拷贝一次。
        /// </summary>
        /// <param name="sessionID">会话ID</param>
        /// <param name="data">数据</param>
        void SendAsync(string sessionID, ReadOnlyMemory<byte> data);

        /// <summary>
        /// 编码并发送协议对象（零拷贝优先）。
        /// </summary>
        /// <param name="sessionID">会话ID</param>
        /// <param name="protocal">协议对象</param>
        void SendAsync(string sessionID, ISocketProtocal protocal);

        /// <summary>
        /// 携带所有权对象的异步发送：发送完成后由实现方归还/释放 <paramref name="owner"/>。
        /// </summary>
        /// <param name="sessionID">会话ID</param>
        /// <param name="data">数据</param>
        /// <param name="owner">数据的所有者，发送完成或失败时释放</param>
        void SendAsync(string sessionID, ReadOnlyMemory<byte> data, IDisposable owner);

        /// <summary>
        /// http end。
        /// </summary>
        /// <param name="sessionID">会话ID</param>
        /// <param name="data">数据</param>
        void End(string sessionID, ReadOnlyMemory<byte> data);

        /// <summary>
        /// 定向发送（Memory）。
        /// </summary>
        /// <param name="ipEndPoint">目标地址</param>
        /// <param name="data">数据</param>
        void SendAsync(IPEndPoint ipEndPoint, ReadOnlyMemory<byte> data);

        /// <summary>
        /// 停止
        /// </summary>
        void Stop();

        /// <summary>
        /// 断开指定会话
        /// </summary>
        /// <param name="sessionID"></param>
        void Disconnect(string sessionID);

        bool IsDisposed { get; set; }
    }
}
