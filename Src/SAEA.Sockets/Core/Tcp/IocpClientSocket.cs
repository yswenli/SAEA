﻿/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c) yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets.Core.Tcp
*文件名： IocpClientSocket
*版本号： v7.0.0.1
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Common;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

namespace SAEA.Sockets.Core.Tcp
{
    /// <summary>
    /// iocp 客户端 socket
    /// </summary>
    public class IocpClientSocket : IClientSocket, IDisposable
    {
        /// <summary>
        /// 套接字对象
        /// </summary>
        Socket _socket;

        /// <summary>
        /// 用户令牌
        /// </summary>
        IUserToken _userToken;

        /// <summary>
        /// 异步连接参数
        /// </summary>
        SocketAsyncEventArgs _connectArgs;

        /// <summary>
        /// 用户令牌工厂
        /// </summary>
        UserTokenFactory _userTokenFactory;

        /// <summary>
        /// 连接回调
        /// </summary>
        Action<SocketError> _connectCallBack;

        /// <summary>
        /// 连接事件
        /// </summary>
        AutoResetEvent _connectEvent = new AutoResetEvent(true);

        /// <summary>
        /// 套接字选项
        /// </summary>
        public ISocketOption SocketOption { get; set; }

        /// <summary>
        /// 远程终结点
        /// </summary>
        public string Endpoint { get => _socket?.RemoteEndPoint?.ToString(); }

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool Connected { get; private set; }

        /// <summary>
        /// 用户令牌
        /// </summary>
        public IUserToken UserToken { get => _userToken; private set => _userToken = value; }

        /// <summary>
        /// 是否已释放
        /// </summary>
        public bool IsDisposed { get; private set; } = false;

        /// <summary>
        /// 套接字对象
        /// </summary>
        public Socket Socket => _socket;

        /// <summary>
        /// 上下文对象
        /// </summary>
        public IContext<ICoder> Context { get; private set; }

        /// <summary>
        /// 错误事件
        /// </summary>
        public event OnErrorHandler OnError;

        /// <summary>
        /// 断开连接事件
        /// </summary>
        public event OnDisconnectedHandler OnDisconnected;

        /// <summary>
        /// 接收数据事件
        /// </summary>
        public event OnClientReceiveHandler OnReceive;

        /// <summary>
        /// 接收字节数据事件
        /// </summary>
        protected OnClientReceiveBytesHandler OnClientReceive = null;

        /// <summary>
        /// 触发错误事件
        /// </summary>
        /// <param name="id">错误ID</param>
        /// <param name="ex">异常对象</param>
        protected void RaiseOnError(string id, Exception ex)
        {
            OnError?.Invoke(id, ex);
        }

        /// <summary>
        /// iocp 客户端 socket
        /// </summary>
        /// <param name="socketOption">Socket选项</param>
        public IocpClientSocket(ISocketOption socketOption)
        {
            SocketOption = socketOption;

            Context = socketOption.Context;

            _userTokenFactory = new UserTokenFactory();

            if (SocketOption.UseIPV6)
            {
                _socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
            }
            else
            {
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            }
            if (SocketOption.ReusePort)
            {
                _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, SocketOption.ReusePort);
            }
            _socket.NoDelay = SocketOption.NoDelay;
            _socket.SendTimeout = _socket.ReceiveTimeout = SocketOption.TimeOut;
            _socket.KeepAlive(SocketOption.FreeTime, SocketOption.TimeOut);

            OnClientReceive = new OnClientReceiveBytesHandler(OnReceived);

            _connectArgs = new SocketAsyncEventArgs
            {
                RemoteEndPoint = new IPEndPoint(IPAddress.Parse(SocketOption.IP), SocketOption.Port)
            };
            _connectArgs.Completed += ConnectArgs_Completed;

            _userToken = _userTokenFactory.Create(socketOption.Context, socketOption.ReadBufferSize, IO_Completed);
        }

        /// <summary>
        /// iocp 客户端 socket
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="ip">IP地址</param>
        /// <param name="port">端口</param>
        /// <param name="bufferSize">缓冲区大小</param>
        /// <param name="timeOut">超时时间</param>
        public IocpClientSocket(IContext<ICoder> context, string ip = "127.0.0.1", int port = 39654, int bufferSize = 64 * 1024, int timeOut = 180 * 1000)
            : this(new SocketOption() { Context = context, IP = ip, Port = port, ReadBufferSize = bufferSize, WriteBufferSize = bufferSize, TimeOut = timeOut })
        {

        }

        /// <summary>
        /// 指定绑定ip
        /// </summary>
        /// <param name="ipEndPint">IP终结点</param>
        public void Bind(IPEndPoint ipEndPint)
        {
            _socket.Bind(ipEndPint);
        }

        /// <summary>
        /// 指定绑定ip
        /// </summary>
        /// <param name="ip">IP地址</param>
        public void Bind(string ip)
        {
            Bind(new IPEndPoint(IPAddress.Parse(ip), 0));
        }

        /// <summary>
        /// 异步连接到服务器
        /// </summary>
        /// <param name="callBack">连接回调</param>
        public void ConnectAsync(Action<SocketError> callBack = null)
        {
            _connectEvent.WaitOne();
            if (!Connected)
            {
                _connectCallBack = callBack;
                if (!_socket.ConnectAsync(_connectArgs))
                {
                    ProcessConnected(_connectArgs);
                }
            }
        }

        /// <summary>
        /// 同步连接到服务器
        /// </summary>
        public void Connect()
        {
            if (!Connected && !IsDisposed)
            {
                _socket.Connect(SocketOption.IP, SocketOption.Port);

                _userToken.ID = _socket.LocalEndPoint.ToString();
                _userToken.Socket = _socket;
                _userToken.Linked = _userToken.Actived = DateTimeHelper.Now;

                var readArgs = _userToken.ReadArgs;

                ProcessReceive(readArgs);

                Connected = true;
            }
        }

        /// <summary>
        /// 连接完成事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        void ConnectArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            if (e.LastOperation == SocketAsyncOperation.Connect)
                ProcessConnected(e);
            else
            {
                OnError?.Invoke("", new Exception($"SAEA SocketError:连接失败, {e.LastOperation}"));
            }
        }

        /// <summary>
        /// 处理连接完成
        /// </summary>
        /// <param name="e">事件参数</param>
        void ProcessConnected(SocketAsyncEventArgs e)
        {
            Connected = (e.SocketError == SocketError.Success);

            if (Connected)
            {
                _userToken.ID = e.ConnectSocket.LocalEndPoint.ToString();
                _userToken.Socket = _socket;
                _userToken.Linked = _userToken.Actived = DateTimeHelper.Now;

                var readArgs = _userToken.ReadArgs;
                ProcessReceive(readArgs);
            }
            _connectCallBack?.Invoke(e.SocketError);
            _connectEvent.Set();
        }

        /// <summary>
        /// IO完成事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        void IO_Completed(object sender, SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    ProcessReceived(e);
                    break;
                case SocketAsyncOperation.Send:
                    ProcessSended(e);
                    break;
                default:
                    Disconnect();
                    break;
            }
        }

        /// <summary>
        /// 处理接收到的数据
        /// </summary>
        /// <param name="data">接收到的数据</param>
        protected virtual void OnReceived(byte[] data)
        {
            OnReceive?.Invoke(data);
        }

        /// <summary>
        /// 处理接收操作
        /// </summary>
        /// <param name="readArgs">读取操作的SocketAsyncEventArgs对象</param>
        void ProcessReceive(SocketAsyncEventArgs readArgs)
        {
            if (_userToken != null && _userToken.Socket != null && _userToken.Socket.Connected)
            {
                if (!_userToken.Socket.ReceiveAsync(readArgs))
                {
                    ProcessReceived(readArgs);
                }
            }
            else
                OnError?.Invoke("", new Exception("SAEA SocketError:发送失败,当前连接已断开"));
        }

        /// <summary>
        /// 处理接收完成
        /// </summary>
        /// <param name="readArgs">读取操作的SocketAsyncEventArgs对象</param>
        void ProcessReceived(SocketAsyncEventArgs readArgs)
        {
            try
            {
                if (readArgs == null || readArgs.UserToken == null) return;

                if (readArgs.SocketError == SocketError.Success)
                {
                    _userToken.Actived = DateTimeHelper.Now;

                    if (readArgs.BytesTransferred > 0)
                    {
                        var buffer = readArgs.Buffer.AsSpan().Slice(readArgs.Offset, readArgs.BytesTransferred).ToArray();

                        try
                        {
                            OnClientReceive?.Invoke(buffer);
                        }
                        catch (Exception ex)
                        {
                            OnError?.Invoke(UserToken.ID, ex);
                        }
                    }

                    if (readArgs.SocketError == SocketError.Success && UserToken.Socket != null && UserToken.Socket.Connected)
                    {
                        ProcessReceive(readArgs);
                    }
                    else
                    {
                        Disconnect();
                    }
                }
                else
                {
                    ProcessDisconnected(new Exception("SAEA SocketError:远程服务器关闭连接"));
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(_userToken.ID, ex);
            }
        }

        /// <summary>
        /// 处理发送完成
        /// </summary>
        /// <param name="e">事件参数</param>
        void ProcessSended(SocketAsyncEventArgs e)
        {
            _userToken.Actived = DateTimeHelper.Now;
            _userToken.ReleaseWrite();
        }

        /// <summary>
        /// 处理断开连接
        /// </summary>
        /// <param name="ex">异常对象</param>
        void ProcessDisconnected(Exception ex)
        {
            if (Connected)
            {
                Connected = false;
                _connectEvent.Set();
                try
                {
                    _userToken.Clear();
                }
                catch { }
            }
        }

        /// <summary>
        /// 异步发送数据
        /// </summary>
        /// <param name="userToken">用户令牌</param>
        /// <param name="data">数据</param>
        public void SendAsync(IUserToken userToken, byte[] data)
        {
            try
            {
                if (userToken != null && userToken.Socket != null && userToken.Socket.Connected)
                {
                    if (userToken.WaitWrite(SocketOption.TimeOut))
                    {
                        var writeArgs = userToken.WriteArgs;

                        writeArgs.SetBuffer(data, 0, data.Length);

                        if (!userToken.Socket.SendAsync(writeArgs))
                        {
                            ProcessSended(writeArgs);
                        }
                    }
                    else
                    {
                        OnError?.Invoke($"SAEA SocketError:发送消息时发生异常,{userToken?.ID}", new TimeoutException("发送数据超时"));
                    }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(userToken.ID, ex);
                userToken.ReleaseWrite();
                Disconnect();
            }
        }

        /// <summary>
        /// 异步发送数据
        /// </summary>
        /// <param name="data">数据</param>
        public void SendAsync(byte[] data)
        {
            SendAsync(UserToken, data);
        }

        /// <summary>
        /// 同步发送数据
        /// </summary>
        /// <param name="data">数据</param>
        public void Send(byte[] data)
        {
            if (data == null) return;

            if (Connected)
            {
                try
                {
                    var offset = 0;
                    do
                    {
                        var iResult = _socket.BeginSend(data, offset, data.Length - offset, SocketFlags.None, null, null);

                        offset += _socket.EndSend(iResult);
                    }
                    while (offset < data.Length);

                    _userToken.Actived = DateTimeHelper.Now;
                }
                catch (Exception ex)
                {
                    ProcessDisconnected(ex);
                }
            }
            else
                OnError?.Invoke("", new Exception("SAEA SocketError:发送失败,当前连接已断开"));
        }

        /// <summary>
        /// 异步发送数据
        /// </summary>
        /// <param name="data">数据</param>
        public void BeginSend(byte[] data)
        {
            if (Connected)
            {
                _userToken.Socket.BeginSend(data, 0, data.Length, SocketFlags.None, null, null);
            }
        }

        /// <summary>
        /// 异步发送数据
        /// </summary>
        /// <param name="buffer">缓冲区</param>
        /// <param name="offset">偏移量</param>
        /// <param name="count">数量</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        public async Task SendAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                var data = new byte[count];

                Buffer.BlockCopy(buffer, offset, data, 0, count);

                Send(data);

            }, cancellationToken);
        }

        /// <summary>
        /// 异步接收数据
        /// </summary>
        /// <param name="buffer">缓冲区</param>
        /// <param name="offset">偏移量</param>
        /// <param name="count">数量</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        public Task<int> ReceiveAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new KernelException("SAEA SocketError:当前方法只在stream实现中才能使用!");
        }

        /// <summary>
        /// 获取流
        /// </summary>
        /// <returns></returns>
        public Stream GetStream()
        {
            throw new InvalidOperationException("SAEA SocketError:iocp暂不支持流模式");
        }

        /// <summary>
        /// 主动断开连接
        /// </summary>
        /// <param name="ex">异常对象</param>
        public void Disconnect(Exception ex)
        {
            this.Connected = false;

            var mex = ex;

            try
            {
                if (_userToken != null && _userToken.Socket != null)
                {
                    try
                    {
                        _userToken.Socket.Shutdown(SocketShutdown.Both);
                    }
                    catch { }
                    _userToken.Socket.Close();
                }
            }
            catch (Exception sex)
            {
                if (mex != null) mex = sex;
            }
            finally
            {
                if (mex == null)
                {
                    mex = new Exception("SAEA SocketError:当前连接已主动断开");
                }
                if (_userToken != null)
                    OnDisconnected?.Invoke(_userToken.ID, mex);
            }

            _userToken.Clear();
        }

        /// <summary>
        /// 处理会话超时
        /// </summary>
        /// <param name="obj">用户令牌</param>
        private void _sessionManager_OnTimeOut(IUserToken obj)
        {
            Disconnect();
        }

        /// <summary>
        /// 主动断开连接
        /// </summary>
        public void Disconnect()
        {
            this.Disconnect(null);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            IsDisposed = true;
            this.Disconnect();
        }
    }
}
