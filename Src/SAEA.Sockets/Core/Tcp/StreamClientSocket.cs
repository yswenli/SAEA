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
*命名空间：SAEA.Sockets.Core.Tcp
*文件名： StreamClientSocket
*版本号： v26.4.23.1
*唯一标识：47c366de-f458-4c23-b895-a8ab052b75a9
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/01/23 11:16:53
*描述：StreamClientSocket接口
*
*=====================================================================
*修改标记
*修改时间：2019/01/23 11:16:53
*修改人： yswenli
*版本号： v26.4.23.1
*描述：StreamClientSocket接口
*
*****************************************************************************/
using SAEA.Common.Caching;
using SAEA.Sockets.Base;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;

using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets.Core.Tcp
{
    /// <summary>
    /// 流模式下的tcp client socket
    /// </summary>
    public class StreamClientSocket : IClientSocket
    {
        Socket _socket;

        bool _isSsl = false;

        public ISocketOption SocketOption { get; set; }

        Stream _stream;

        /// <summary>
        /// <see cref="SendAsync(ISocketProtocal)"/> 在未设置 <see cref="Context"/>（如包装
        /// <see cref="Stream"/> 的构造函数）时复用的回退编码器。惰性创建并缓存，避免每次发送都新建
        /// <see cref="BaseCoder"/> 而泄漏其持有的 <c>ArrayPool</c> 缓冲。
        /// </summary>
        private ICoder _sendFallbackCoder;

        public bool Connected
        {
            get; private set;
        } = false;

        public bool IsDisposed { get; private set; } = false;

        public string Endpoint
        {
            get
            {

                if (_socket != null && _socket.Connected)

                    return _socket?.LocalEndPoint?.ToString();

                return string.Empty;
            }
        }

        public Socket Socket => _socket;

        public IContext<ICoder> Context { get; private set; }

        public event OnDisconnectedHandler OnDisconnected;

        public event OnClientReceiveSpanHandler OnClientReceiveSpan;


        public event OnErrorHandler OnError;

        CancellationToken _cancellationToken;


        IPEndPoint _serverIPEndpint;

        /// <summary>
        /// 流模式下的tcp client socket
        /// </summary>
        /// <param name="socketOption"></param>
        /// <param name="cancellationToken"></param>
        public StreamClientSocket(ISocketOption socketOption, CancellationToken cancellationToken)
        {
            SocketOption = socketOption;
            _cancellationToken = cancellationToken;
        }

        /// <summary>
        /// 流模式下的tcp client socket
        /// </summary>
        /// <param name="socketOption"></param>
        public StreamClientSocket(ISocketOption socketOption) : this(socketOption, CancellationToken.None)
        {
            Context = SocketOption.Context;

            if (SocketOption.UseIPV6)
            {
                _socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);

                if (string.IsNullOrEmpty(SocketOption.IP))
                    _serverIPEndpint = (new IPEndPoint(IPAddress.IPv6Any, SocketOption.Port));
                else
                    _serverIPEndpint = (new IPEndPoint(IPAddress.Parse(SocketOption.IP), SocketOption.Port));
            }
            else
            {
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                if (string.IsNullOrEmpty(SocketOption.IP))
                    _serverIPEndpint = (new IPEndPoint(IPAddress.Any, SocketOption.Port));
                else
                    _serverIPEndpint = (new IPEndPoint(IPAddress.Parse(SocketOption.IP), SocketOption.Port));
            }

            if (SocketOption.ReusePort)
                _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, SocketOption.ReusePort);

            _socket.NoDelay = SocketOption.NoDelay;
        }

        /// <summary>
        /// 流模式下的tcp client socket
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="stream"></param>
        /// <param name="isSsl"></param>
        public StreamClientSocket(Socket socket, Stream stream, bool isSsl = false)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));

            _stream = stream ?? throw new ArgumentNullException(nameof(stream));

            _isSsl = isSsl;
        }

        /// <summary>
        /// 指定绑定ip
        /// </summary>
        /// <param name="ipEndPoint"></param>
        public void Bind(IPEndPoint ipEndPoint)
        {
            _socket.Bind(ipEndPoint);
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
        /// Connect
        /// </summary>
        public void Connect()
        {
            if (!Connected)
            {
                _socket.Connect(SocketOption.IP, SocketOption.Port);

                if (_isSsl)
                {
                    _stream = new SslStream(new NetworkStream(_socket, true), false, InternalUserCertificateValidationCallback);

                    ((SslStream)_stream).AuthenticateAsClient(SocketOption.IP, LoadCertificates(), SocketOption.SslProtocol, true);
                }
                else
                {
                    _stream = new NetworkStream(_socket, true);
                }

                _stream.ReadTimeout = SocketOption.ActionTimeout;
                _stream.WriteTimeout = SocketOption.ActionTimeout;

                Connected = true;
            }
        }

        /// <summary>
        /// 某些特定证书处理连接方法
        /// </summary>
        /// <param name="ucc"></param>
        /// <param name="func"></param>
        /// <param name="ignoreCerErrs"></param>
        /// <returns></returns>
        public async Task<Stream> ConnectAsync(RemoteCertificateValidationCallback ucc, Func<X509CertificateCollection> func, bool ignoreCerErrs)
        {
            if (!Connected)
            {

                await _socket.ConnectAsync(SocketOption.IP, SocketOption.Port).ConfigureAwait(false);

                var stream = new NetworkStream(_socket, true);

                if (_isSsl)
                {
                    var sslStream = new SslStream(stream, false, ucc);
                    await sslStream.AuthenticateAsClientAsync(SocketOption.IP, func.Invoke(), SocketOption.SslProtocol, !ignoreCerErrs).ConfigureAwait(false);
                    _stream = sslStream;
                }
                else
                {
                    _stream = stream;
                }
                Connected = true;
            }
            return _stream;
        }


        /// <summary>
        /// ConnectAsync
        /// </summary>
        /// <param name="callBack"></param>
        public void ConnectAsync(Action<SocketError> callBack)
        {
            try
            {
                var result = ConnectAsync().Result;

                callBack?.Invoke(result);
            }
            catch
            {
                callBack?.Invoke(SocketError.SocketError);
            }
        }

        /// <summary>
        /// ConnectAsync
        /// </summary>
        /// <returns></returns>
        public async Task<SocketError> ConnectAsync()
        {
            if (!Connected)
            {
                try
                {
                    // 使用ConnectTimeout设置连接超时
                    var connectTask = _socket.ConnectAsync(SocketOption.IP, SocketOption.Port);
                    var timeoutTask = Task.Delay(SocketOption.ConnectTimeout);
                    
                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                    
                    if (completedTask == timeoutTask)
                    {
                        // 连接超时
                        _socket.Close(SocketOption.ActionTimeout);
                        return SocketError.TimedOut;
                    }
                    
                    // 等待连接任务完成，以捕获任何连接错误
                    await connectTask;

                    if (_isSsl)
                    {
                        _stream = new SslStream(new NetworkStream(_socket, true), false, InternalUserCertificateValidationCallback);

                        ((SslStream)_stream).AuthenticateAsClient(SocketOption.IP, LoadCertificates(), SocketOption.SslProtocol, true);
                    }
                    else
                    {
                        _stream = new NetworkStream(_socket, true);
                    }

                    _stream.ReadTimeout = SocketOption.ActionTimeout;

                    _stream.WriteTimeout = SocketOption.ActionTimeout;

                    this.Connected = true;

                    return SocketError.Success;
                }
                catch
                {
                    return SocketError.SocketError;
                }
            }
            return SocketError.Success;
        }

        /// <summary>
        /// 同步发送（Span）。netstandard2.0 的 <see cref="Stream.Write(byte[], int, int)"/> 不接受
        /// <see cref="ReadOnlySpan{T}"/>，因此在本边界做一次 <c>byte[]</c> 拷贝后同步写入 <see cref="_stream"/>。
        /// </summary>
        /// <param name="data">数据</param>
        public void Send(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return;
            var copy = data.ToArray();
            _stream.Write(copy, 0, copy.Length);
        }

        /// <summary>
        /// 即发即忘异步发送（Memory）。netstandard2.0 的 <see cref="Stream.WriteAsync(byte[], int, int)"/> 不接受
        /// <see cref="ReadOnlyMemory{T}"/>，因此在本边界做一次 <c>byte[]</c> 拷贝后异步写入；失败时通过
        /// <see cref="OnError"/> 上报，保持即发即忘（不等待写入任务）语义。
        /// </summary>
        /// <param name="data">数据</param>
        public void SendAsync(ReadOnlyMemory<byte> data)
        {
            if (data.Length == 0) return;
            var copy = data.ToArray();
            Task.Run(async () =>
            {
                try
                {
                    await _stream.WriteAsync(copy, 0, copy.Length);
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(Endpoint, ex);
                }
            });
        }

        /// <summary>
        /// 编码并发送协议对象。编码器优先取 <see cref="Context"/> 中配置的 Unpacker；包装
        /// <see cref="Stream"/> 的构造函数不设置 Context，此时回退到缓存的 <see cref="BaseCoder"/>，
        /// 避免静默丢弃。编码写入 <see cref="PooledBufferWriter"/> 后，通过
        /// <see cref="SendAsync(ReadOnlyMemory{byte})"/> 发送其已写入区间，并确保写入器仅释放一次。
        /// </summary>
        /// <param name="protocal">协议对象</param>
        public void SendAsync(ISocketProtocal protocal)
        {
            if (protocal == null) return;
            var coder = Context?.Unpacker ?? (_sendFallbackCoder ??= new BaseCoder());
            using (var writer = new PooledBufferWriter(protocal.BodyLength > 0 && protocal.BodyLength < int.MaxValue - 64 ? (int)protocal.BodyLength + 64 : 64))
            {
                coder.Encode(protocal, writer);
                SendAsync(writer.WrittenMemory);
            }
        }

        /// <summary>
        /// 异步流发送（Memory）。netstandard2.0 下先在本边界做一次 <c>byte[]</c> 拷贝，随后直接把
        /// <paramref name="cancellationToken"/> 交给 <see cref="_stream"/> 的异步写入（不再用
        /// <see cref="Task.Run(Action, CancellationToken)"/> 间接调度）。
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>写入任务</returns>
        public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            if (data.Length == 0) return Task.CompletedTask;
            var copy = data.ToArray();
            return _stream.WriteAsync(copy, 0, copy.Length, cancellationToken);
        }

        /// <summary>
        /// GetStream
        /// </summary>
        /// <returns></returns>
        public Stream GetStream()
        {
            return _stream;
        }

        /// <summary>
        /// 断开
        /// </summary>
        /// <param name="ex"></param>
        public void Disconnect()
        {
            if (this.Connected)
            {
                try
                {
                    _socket.Shutdown(SocketShutdown.Both);
                    OnDisconnected?.Invoke(SocketOption.IP + ":" + SocketOption.Port, null);
                }
                catch (Exception ex)
                {
                    OnDisconnected?.Invoke(SocketOption.IP + ":" + SocketOption.Port, ex);
                }
                finally
                {
                    _socket.Close();
                }
                this.Connected = false;
            }
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            this.Disconnect();
            IsDisposed = true;
        }


        #region ssl
        private bool InternalUserCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return false;
        }


        private X509CertificateCollection LoadCertificates()
        {
            var certificates = new X509CertificateCollection();
            if (SocketOption.X509Certificate2 == null)
            {
                return certificates;
            }

            certificates.Add(SocketOption.X509Certificate2);

            return certificates;
        }
        #endregion
    }
}