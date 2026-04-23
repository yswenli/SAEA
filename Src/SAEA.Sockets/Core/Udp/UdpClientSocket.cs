/****************************************************************************
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*项目名称：SAEA.Sockets.Core.Udp
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Sockets.Core.Udp
*类 名 称：UdpClientSocket
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/28 18:01:43
*描述：
*=====================================================================
*修改时间：2020/12/28 18:01:43
*修 改 人： yswenli
*版本号： v26.4.23.1
*描    述：
*****************************************************************************/
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;

namespace SAEA.Sockets.Core.Udp
{
    public class UdpClientSocket : IClientSocket, IDisposable
    {
        const int SIO_UDP_CONNRESET = unchecked((int)0x9800000C);

        Socket _udpSocket;

        IUserToken _userToken;

        SocketAsyncEventArgs _connectArgs;

        UserTokenFactory _userTokenFactory;

        public ISocketOption SocketOption { get; set; }

        IPEndPoint _remoteEndPoint;

        public string Endpoint { get => _udpSocket?.RemoteEndPoint?.ToString(); }

        public bool Connected { get => _udpSocket.Connected; }
        public IUserToken UserToken { get => _userToken; private set => _userToken = value; }

        public bool IsDisposed { get; private set; } = false;

        public Socket Socket => _udpSocket;

        public IContext<ICoder> Context { get; private set; }

        public event OnErrorHandler OnError;

        public event OnDisconnectedHandler OnDisconnected;

        public event OnClientReceiveHandler OnReceive;

        protected OnClientReceiveBytesHandler OnClientReceive = null;

        /// <summary>
        /// 内部委托：接收数据时触发（Span版本）
        /// </summary>
        /// <param name="data">数据Span</param>
        internal delegate void OnClientReceiveSpanHandler(ReadOnlySpan<byte> data);

        /// <summary>
        /// 内部事件：客户端接收数据（Span版本）
        /// </summary>
        internal event OnClientReceiveSpanHandler OnClientReceiveSpan;

        /// <summary>
        /// 小数据阈值（4KB）
        /// </summary>
        internal const int SmallDataThreshold = 4 * 1024;

        protected void RaiseOnError(string id, Exception ex)
        {
            OnError?.Invoke(id, ex);
        }

        /// <summary>
        /// iocp 客户端 socket
        /// </summary>
        /// <param name="socketOption"></param>
        public UdpClientSocket(ISocketOption socketOption)
        {
            SocketOption = socketOption;

            Context = socketOption.Context;

            _userTokenFactory = new UserTokenFactory();

            _udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, SocketOption.ReusePort);
            _udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, SocketOption.Broadcasted);

            //设置多播
            if (!string.IsNullOrEmpty(SocketOption.MultiCastHost) && !string.IsNullOrEmpty(SocketOption.IP))
            {
                _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);
                MulticastOption mcastOption = new MulticastOption(IPAddress.Parse(SocketOption.MultiCastHost), IPAddress.Parse(SocketOption.IP));
                _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, mcastOption);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _udpSocket.IOControl(SIO_UDP_CONNRESET, new byte[4], new byte[4]);
            }

            _udpSocket.SendTimeout = _udpSocket.ReceiveTimeout = SocketOption.ActionTimeout;
            _udpSocket.SendBufferSize = SocketOption.WriteBufferSize;
            _udpSocket.ReceiveBufferSize = SocketOption.ReadBufferSize;

            OnClientReceive = new OnClientReceiveBytesHandler(OnReceived);

            _connectArgs = new SocketAsyncEventArgs
            {
                RemoteEndPoint = new IPEndPoint(IPAddress.Parse(SocketOption.IP), SocketOption.Port)
            };
            _connectArgs.Completed += ConnectArgs_Completed;

            _userToken = _userTokenFactory.Create(socketOption.Context, socketOption.ReadBufferSize, IO_Completed);

            _remoteEndPoint = new IPEndPoint(IPAddress.Parse(SocketOption.IP), SocketOption.Port);

            _userToken.Socket = _udpSocket;
        }

        /// <summary>
        /// 指定绑定ip
        /// </summary>
        /// <param name="ip"></param>
        public void Bind(IPEndPoint ipEndPint)
        {
            _udpSocket.Bind(ipEndPint);
        }

        /// <summary>
        /// 指定绑定ip
        /// </summary>
        /// <param name="ip"></param>
        public void Bind(string ip)
        {
            Bind(new IPEndPoint(IPAddress.Parse(ip), 0));
        }

        /// <summary>
        /// 连接到服务器
        /// </summary>
        /// <param name="callBack"></param>
        public void ConnectAsync(Action<SocketError> callBack = null)
        {
            if (!_udpSocket.ConnectAsync(_connectArgs))
            {
                ProcessConnected(_connectArgs);
            }
        }

        /// <summary>
        /// 连接到服务器
        /// </summary>
        /// <param name="timeOut"></param>
        public void Connect()
        {
            ConnectAsync();
        }


        void ConnectArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            if (e.LastOperation == SocketAsyncOperation.Connect)
                ProcessConnected(e);
            else
            {
                OnError?.Invoke("", new Exception($"udp creation failed： {e.LastOperation}"));
            }
        }


        void ProcessConnected(SocketAsyncEventArgs e)
        {
            _userToken.ID = _udpSocket.LocalEndPoint.ToString();
            _userToken.Socket = _udpSocket;
            _userToken.Linked = _userToken.Actived = DateTimeHelper.Now;

            var readArgs = _userToken.ReadArgs;
            ProcessReceive(readArgs);
        }


        void IO_Completed(object sender, SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                case SocketAsyncOperation.ReceiveFrom:
                    ProcessReceived(e);
                    break;
                case SocketAsyncOperation.Send:
                case SocketAsyncOperation.SendTo:
                    ProcessSended(e);
                    break;
                default:
                    Disconnect();
                    break;
            }
        }


        protected virtual void OnReceived(byte[] data)
        {
            OnReceive?.Invoke(data);
        }


        void ProcessReceive(SocketAsyncEventArgs readArgs)
        {
            if (_userToken.Socket != null)
            {
                if (!_userToken.Socket.ReceiveAsync(readArgs))
                {
                    // 使用线程池避免直接递归调用导致栈溢出
                    ThreadPool.QueueUserWorkItem((state) =>
                    {
                        ProcessReceived(readArgs);
                    });
                }
            }

        }


        void ProcessReceived(SocketAsyncEventArgs readArgs)
        {
            try
            {
                if (readArgs.BytesTransferred > 0 && readArgs.SocketError == SocketError.Success)
                {
                    _userToken.Actived = DateTimeHelper.Now;

                    // 使用Span获取数据，避免立即复制
                    var dataSpan = readArgs.Buffer.AsSpan(readArgs.Offset, readArgs.BytesTransferred);

                    // 触发内部Span事件
                    OnClientReceiveSpan?.Invoke(dataSpan);

                    // 复制到精确大小的数组，避免内存池返回的超大数组导致下游逻辑错误
                    var data = dataSpan.ToArray();

                    try
                    {
                        OnClientReceive?.Invoke(data);
                    }
                    finally
                    {
                    }

                    ProcessReceive(readArgs);
                }
                else
                {
                    ProcessDisconnected(new Exception($"SocketError:{readArgs.SocketError},the remote server closed udp"));
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(_userToken.ID, ex);
                Disconnect();
            }
        }


        void ProcessSended(SocketAsyncEventArgs e)
        {
            _userToken.Actived = DateTimeHelper.Now;
            _userToken.ReleaseWrite();
        }



        void ProcessDisconnected(Exception ex)
        {
            try
            {
                _userToken.Clear();
            }
            finally
            {
                if (_userToken != null && !string.IsNullOrEmpty(_userToken.ID))
                    OnDisconnected?.Invoke(_userToken.ID, ex);
            }
        }



        /// <summary>
        /// 异步发送
        /// </summary>
        /// <param name="ipEndPoint"></param>
        /// <param name="data"></param>
        public void SendAsync(IPEndPoint ipEndPoint, byte[] data)
        {
            try
            {
                if (data == null || !data.Any() || data.Length > Model.SocketOption.UDPMaxLength) throw new ArgumentOutOfRangeException("SendAsync Incorrect length of data sent");

                if (UserToken.WaitWrite(SocketOption.ActionTimeout))
                {
                    var writeArgs = UserToken.WriteArgs;

                    writeArgs.SetBuffer(data, 0, data.Length);

                    writeArgs.RemoteEndPoint = ipEndPoint;

                    if (!UserToken.Socket.SendToAsync(writeArgs))
                    {
                        ProcessSended(writeArgs);
                    }
                }
                else
                {
                    OnError?.Invoke($"An exception occurs when a message is sending:{ipEndPoint.ToString()}", new TimeoutException("Sending data timeout"));
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(_remoteEndPoint.ToString(), ex);
                UserToken.ReleaseWrite();
                Disconnect();
            }
        }

        /// <summary>
        /// iocp发送
        /// </summary>
        /// <param name="data"></param>
        public void SendAsync(byte[] data)
        {
            SendAsync(_remoteEndPoint, data);
        }

        /// <summary>
        /// 同步发送
        /// </summary>
        /// <param name="data"></param>
        public void Send(byte[] data)
        {
            if (data == null || !data.Any() || data.Length > Model.SocketOption.UDPMaxLength) throw new ArgumentOutOfRangeException("Send Incorrect length of data sent");

            try
            {
                var offset = 0;
                do
                {
                    var iResult = _udpSocket.BeginSendTo(data, offset, data.Length - offset, SocketFlags.None, _remoteEndPoint, null, null);

                    offset += _udpSocket.EndSend(iResult);
                }
                while (offset < data.Length);

                _userToken.Actived = DateTimeHelper.Now;
            }
            catch (Exception ex)
            {
                ProcessDisconnected(ex);
            }
        }

        public void BeginSend(byte[] data)
        {
            if (data == null || !data.Any() || data.Length > Model.SocketOption.UDPMaxLength) throw new ArgumentOutOfRangeException("BeginSend Incorrect length of data sent");

            _userToken.Socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, _remoteEndPoint, null, null);

            _userToken.Actived = DateTimeHelper.Now;
        }


        public Task SendAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                // 根据数据大小选择分配策略
                byte[] data;
                if (count < SmallDataThreshold)
                {
                    // 小数据：直接分配
                    data = new byte[count];
                }
                else
                {
                    // 大数据：从内存池租用
                    data = MemoryPoolManager.Rent(count);
                }

                try
                {
                    Buffer.BlockCopy(buffer, offset, data, 0, count);

                    if (data == null || !data.Any() || data.Length > Model.SocketOption.UDPMaxLength) throw new ArgumentOutOfRangeException("SendAsync Incorrect length of data sent");

                    Send(data);
                }
                finally
                {
                    // 如果是大数据，归还到内存池
                    if (count >= SmallDataThreshold)
                    {
                        MemoryPoolManager.Return(data, count);
                    }
                }

            }, cancellationToken);
        }

        public Task<int> ReceiveAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("The current method can only be used in stream implementation!");
        }

        public Stream GetStream()
        {
            throw new NotSupportedException("UDP does not support streaming mode");
        }

        public void Disconnect(Exception ex)
        {
            var mex = ex;

            try
            {
                if (_userToken != null && _userToken.Socket != null)
                {
                    _userToken.Socket.Close();
                }
            }
            catch (Exception sex)
            {
                if (mex != null) mex = sex;
            }
            if (mex == null)
            {
                mex = new Exception("The current udp has been actively closed");
            }

            if (_userToken != null && !string.IsNullOrEmpty(_userToken.ID))
                OnDisconnected?.Invoke(_userToken.ID, mex);

            _userToken.Clear();
        }

        public void Disconnect()
        {
            this.Disconnect(null);
        }

        public void Dispose()
        {
            this.Disconnect();
            IsDisposed = true;
        }
    }
}
