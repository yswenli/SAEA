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
*修改时间：2019/08/22 10:00:37
*修改人： yswenli
*版本号： v26.4.23.1
*描述：IClientSocket接口
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
        IContext<ICoder> Context { get; }

        ISocketOption SocketOption { get; set; }

        /// <summary>
        /// 端地址
        /// </summary>
        string Endpoint { get; }

        /// <summary>
        /// socket
        /// </summary>
        Socket Socket { get; }

        /// <summary>
        /// 连接
        /// </summary>
        bool Connected { get; }

        /// <summary>
        /// 接收数据事件
        /// </summary>
        event OnClientReceiveHandler OnReceive;

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
        /// 导步发送
        /// </summary>
        /// <param name="data"></param>
        void BeginSend(byte[] data);

        /// <summary>
        /// 同步发送
        /// </summary>
        /// <param name="data"></param>
        void Send(byte[] data);

        /// <summary>
        /// iocp发送
        /// </summary>
        /// <param name="data"></param>
        void SendAsync(byte[] data);

        /// <summary>
        /// 异步流发送
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task SendAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);

        /// <summary>
        /// 异步流接收
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<int> ReceiveAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);

        /// <summary>
        /// 网络流
        /// </summary>
        /// <returns></returns>
        Stream GetStream();        

        /// <summary>
        /// 断开连接
        /// </summary>
        void Disconnect();
    }
}
