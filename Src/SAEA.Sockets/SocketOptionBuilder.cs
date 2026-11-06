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
*文件名： SocketOptionBuilder
*版本号： v26.4.23.1
*唯一标识：4425d3be-dfc6-4f9d-86b4-910a0c32df12
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/02/11 17:03:06
*描述：SocketOptionBuilder接口
*
*=====================================================================
*修改标记
*修改时间：2019/02/11 17:03:06
*修改人： yswenli
*版本号： v26.4.23.1
*描述：SocketOptionBuilder接口
*
*****************************************************************************/
using SAEA.Common.IO;
using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using SAEA.Sockets.Ssl;

using System;
using System.IO.Pipelines;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets
{
    /// <summary>
    /// socket选项创造器
    /// </summary>
    public class SocketOptionBuilder
    {

        SocketOption _socketOption;

        /// <summary>
        /// socket选项创造器
        /// </summary>
        public SocketOptionBuilder()
        {
            _socketOption = new SocketOption();
        }

        /// <summary>
        /// 返回一个SocketOptionBuilder实例
        /// </summary>
        public static SocketOptionBuilder Instance
        {
            get
            {
                return new SocketOptionBuilder();
            }
        }

        /// <summary>
        /// 设置socket类型
        /// </summary>
        /// <param name="socketType"></param>
        /// <returns></returns>
        public SocketOptionBuilder SetSocket(SAEASocketType socketType = SAEASocketType.Tcp)
        {
            _socketOption.SocketType = socketType;
            return this;
        }

        /// <summary>
        /// 启用包模式
        /// </summary>
        /// <returns></returns>
        public SocketOptionBuilder UsePacket<T>() where T : class, ICoder
        {
            _socketOption.Context = (BaseContext<T>)Activator.CreateInstance(typeof(BaseContext<T>));
            _socketOption.UsePacket = true;
            _socketOption.UseStream = false;
            return this;
        }
        /// <summary>
        /// 启用包模式
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public SocketOptionBuilder UsePacket(IContext<ICoder> context)
        {
            _socketOption.Context = context;
            _socketOption.UsePacket = true;
            _socketOption.UseStream = false;
            return this;
        }

        /// <summary>
        /// 启用包模式
        /// </summary>
        /// <returns></returns>
        public SocketOptionBuilder UsePacket()
        {
            _socketOption.Context = new BaseContext<BaseCoder>();
            _socketOption.UsePacket = true;
            _socketOption.UseStream = false;
            return this;
        }

        /// <summary>
        /// 使用流模式
        /// </summary>
        /// <returns></returns>
        public SocketOptionBuilder UseStream()
        {
            if (_socketOption.SocketType == SAEASocketType.Udp) throw new NotSupportedException("udp不支持流");
            _socketOption.SocketType = SAEASocketType.Tcp;
            _socketOption.UseStream = true;
            _socketOption.UsePacket = false;

            // 流模式默认使用 StreamUserToken，便于消费者通过令牌访问 Stream / Input。
            if (_socketOption.Context == null)
            {
                var context = new BaseContext<BaseCoder>();
                context.UserToken = new StreamUserToken { Coder = context.Unpacker };
                _socketOption.Context = context;
            }

            return this;
        }

        /// <summary>
        /// 注册连接建立回调（通道登记完成后、通知 OnAccepted 之前同步调用，禁止阻塞 IO）。
        /// </summary>
        /// <param name="callback">回调</param>
        /// <returns></returns>
        public SocketOptionBuilder UseConnectionOpened(Action<ChannelInfo> callback)
        {
            _socketOption.ConnectionOpened = callback;
            return this;
        }

        /// <summary>
        /// 注册输入处理器，由它独占该连接的 PipeReader 并接管读循环。
        /// </summary>
        /// <param name="handler">输入处理器</param>
        /// <returns></returns>
        public SocketOptionBuilder UseInputHandler(
            Func<IUserToken, PipeReader, IReceiveFlowControl, CancellationToken, Task> handler)
        {
            _socketOption.InputHandler = handler;
            return this;
        }

        /// <summary>
        /// 是否延时
        /// </summary>
        /// <returns></returns>
        public SocketOptionBuilder SetDelay()
        {
            if (_socketOption.SocketType == SAEASocketType.Udp) throw new NotSupportedException("udp不支持Nagle");
            _socketOption.NoDelay = false;
            return this;
        }

        /// <summary>
        /// 使用ssl
        /// </summary>
        /// <param name="sslProtocols"></param>
        /// <param name="pfxFilePath"></param>
        /// <param name="pwd"></param>
        /// <returns></returns>
        public SocketOptionBuilder WithSsl(SslProtocols sslProtocols, string pfxFilePath = null, string pwd = "")
        {
            if (_socketOption.SocketType == SAEASocketType.Udp) throw new NotSupportedException("udp不支持ssl");

            _socketOption.Tls.Protocols = sslProtocols;

            if (!string.IsNullOrEmpty(pfxFilePath))
            {
                if (!FileHelper.Exists(pfxFilePath))
                {
                    throw new NotSupportedException("cenFilePath设置有误，找不到该证书文件!");
                }

                _socketOption.Tls.ServerCertificate = SaeaCertificateLoader.FromPfx(pfxFilePath, pwd);
                _socketOption.Tls.Enabled = true;
            }

            return this;
        }

        /// <summary>
        /// 使用ssl
        /// </summary>
        /// <param name="x509Certificate2"></param>
        /// <param name="sslProtocols"></param>
        /// <returns></returns>
        public SocketOptionBuilder WithSsl(X509Certificate2 x509Certificate2,
            SslProtocols sslProtocols = SaeaTlsProtocols.Default)
        {
            if (_socketOption.SocketType == SAEASocketType.Udp) throw new NotSupportedException("udp不支持ssl");

            _socketOption.Tls.Protocols = sslProtocols;

            if (x509Certificate2 != null)
            {
                _socketOption.Tls.ServerCertificate = x509Certificate2;
                _socketOption.Tls.Enabled = true;
            }

            return this;
        }

        /// <summary>
        /// 使用统一的 TLS 配置入口。
        /// </summary>
        /// <param name="configure"></param>
        /// <returns></returns>
        public SocketOptionBuilder WithSsl(Action<SaeaTlsOptions> configure)
        {
            if (_socketOption.SocketType == SAEASocketType.Udp) throw new NotSupportedException("udp不支持ssl");
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            configure(_socketOption.Tls);
            _socketOption.Tls.Enabled = true;
            return this;
        }

        /// <summary>
        /// 设置服务端 SNI 证书选择器。
        /// </summary>
        /// <param name="selector"></param>
        /// <returns></returns>
        public SocketOptionBuilder UseServerCertificateSelector(Func<string, X509Certificate2> selector)
        {
            _socketOption.Tls.ServerCertificateSelector = selector;
            return this;
        }

        /// <summary>
        /// 添加客户端证书（用于 mTLS）。
        /// </summary>
        /// <param name="certificate"></param>
        /// <returns></returns>
        public SocketOptionBuilder UseClientCertificate(X509Certificate2 certificate)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));

            if (_socketOption.Tls.ClientCertificates == null)
                _socketOption.Tls.ClientCertificates = new X509Certificate2Collection();

            _socketOption.Tls.ClientCertificates.Add(certificate);
            return this;
        }

        /// <summary>
        /// 服务端是否要求客户端证书。
        /// </summary>
        /// <param name="required"></param>
        /// <returns></returns>
        public SocketOptionBuilder RequireClientCertificate(bool required = true)
        {
            _socketOption.Tls.ClientCertificateRequired = required;
            return this;
        }

        /// <summary>
        /// 设置自定义证书校验回调。
        /// </summary>
        /// <param name="callback"></param>
        /// <returns></returns>
        public SocketOptionBuilder SetRemoteCertificateValidation(RemoteCertificateValidationCallback callback)
        {
            _socketOption.Tls.CertificateValidationCallback = callback;
            return this;
        }

        /// <summary>
        /// 设置客户端目标主机名（SNI 与证书主机名校验）。
        /// </summary>
        /// <param name="targetHost"></param>
        /// <returns></returns>
        public SocketOptionBuilder SetTlsTargetHost(string targetHost)
        {
            _socketOption.Tls.TargetHost = targetHost;
            return this;
        }

        /// <summary>
        /// 设置握手超时。
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public SocketOptionBuilder SetHandshakeTimeout(TimeSpan timeout)
        {
            _socketOption.Tls.HandshakeTimeout = timeout;
            return this;
        }

        /// <summary>
        /// 注册 TLS 握手回调。
        /// </summary>
        /// <param name="started"></param>
        /// <param name="completed"></param>
        /// <param name="failed"></param>
        /// <returns></returns>
        public SocketOptionBuilder OnTlsHandshake(
            Action<SaeaTlsHandshakeContext> started = null,
            Action<SaeaTlsHandshakeContext> completed = null,
            Action<SaeaTlsHandshakeContext> failed = null)
        {
            _socketOption.Tls.HandshakeStarted = started;
            _socketOption.Tls.HandshakeCompleted = completed;
            _socketOption.Tls.HandshakeFailed = failed;
            return this;
        }

        /// <summary>
        /// 使用ipv6
        /// </summary>
        /// <returns></returns>
        public SocketOptionBuilder UseIPv6()
        {
            _socketOption.UseIPV6 = true;
            return this;
        }

        /// <summary>
        /// 设置ip
        /// </summary>
        /// <param name="ip"></param>
        /// <returns></returns>
        public SocketOptionBuilder SetIP(string ip)
        {
            _socketOption.IP = ip;
            return this;
        }

        /// <summary>
        /// 设置端口
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        public SocketOptionBuilder SetPort(int port = 39654)
        {
            _socketOption.Port = port;
            return this;
        }

        /// <summary>
        /// 设置ip/port
        /// </summary>
        /// <param name="endPoint"></param>
        /// <returns></returns>
        public SocketOptionBuilder SetIPEndPoint(IPEndPoint endPoint)
        {
            _socketOption.IP = endPoint.Address.ToString();
            _socketOption.Port = endPoint.Port;
            if (endPoint.AddressFamily == AddressFamily.InterNetworkV6)
            {
                _socketOption.UseIPV6 = true;
            }
            else
            {
                _socketOption.UseIPV6 = false;
            }
            return this;
        }

        /// <summary>
        /// 读取缓冲区大小
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public SocketOptionBuilder SetReadBufferSize(int size = 64 * 1024)
        {
            _socketOption.ReadBufferSize = size;
            return this;
        }

        /// <summary>
        /// 写入缓冲区大小
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public SocketOptionBuilder SetWriteBufferSize(int size = 64 * 1024)
        {
            _socketOption.WriteBufferSize = size;
            return this;
        }

        /// <summary>
        /// 服务器中支持的客户端数
        /// </summary>
        /// <param name="maxConnects"></param>
        /// <returns></returns>
        public SocketOptionBuilder SetMaxConnects(int maxConnects = 1000)
        {
            _socketOption.MaxConnects = maxConnects;
            return this;
        }

        /// <summary>
        /// 是否端口复用
        /// </summary>
        /// <param name="reusePort"></param>
        /// <returns></returns>
        public SocketOptionBuilder ReusePort(bool reusePort = true)
        {
            _socketOption.ReusePort = reusePort;
            return this;
        }

        /// <summary>
        /// 操作超时
        /// </summary>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public SocketOptionBuilder SetActionTimeOut(int timeOut = 180 * 1000)
        {
            _socketOption.ActionTimeout = timeOut;
            return this;
        }

        /// <summary>
        /// 空闲时长
        /// </summary>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public SocketOptionBuilder SetFreeTime(int timeOut = 180 * 1000)
        {
            _socketOption.FreeTime = timeOut;
            return this;
        }

        /// <summary>
        /// 连接超时
        /// </summary>
        /// <param name="connectTimeout"></param>
        /// <returns></returns>
        public SocketOptionBuilder SetConnectTimeout(int connectTimeout = 2 * 1000)
        {
            _socketOption.ConnectTimeout = connectTimeout;
            return this;
        }

        /// <summary>
        /// 广播
        /// </summary>
        /// <returns></returns>
        public SocketOptionBuilder UseBroadcast()
        {
            if (_socketOption.SocketType == SAEASocketType.Tcp) throw new NotSupportedException("不支持此模式下的广播");
            _socketOption.Broadcasted = true;
            return this;
        }

        /// <summary>
        /// 设置组播
        /// </summary>
        /// <param name="multiCastHost"></param>
        /// <returns></returns>
        public SocketOptionBuilder SetMultiCastHost(string multiCastHost)
        {
            if (_socketOption.SocketType == SAEASocketType.Tcp) throw new NotSupportedException("不支持此模式下的组播");
            _socketOption.MultiCastHost = multiCastHost;
            return this;
        }

        public ISocketOption Build()
        {
            var inputHandler = _socketOption.InputHandler;
            var connectionOpened = _socketOption.ConnectionOpened;

            // 1. 输入处理器需要 UseStream 提供的 PipeReader，纯 saea 事件模式没有。
            if (inputHandler != null && !_socketOption.UseStream)
                throw new InvalidOperationException("钩子要求 .UseStream()：通道链需要 UseStream 提供的 PipeReader，纯 saea 事件模式没有；请先调用 .UseStream()。");

            // 2. 连接建立钩子同样依赖 UseStream 的连接门面。
            if (connectionOpened != null && !_socketOption.UseStream)
                throw new InvalidOperationException("钩子要求 .UseStream()：UseConnectionOpened 需要 UseStream 提供的连接门面，纯 saea 事件模式没有；请先调用 .UseStream()。");

            // 3. 输入处理器必须与连接建立钩子成对配置，否则拿不到通道登记信息。
            if (inputHandler != null && connectionOpened == null)
                throw new InvalidOperationException("UseInputHandler 必须与 UseConnectionOpened 成对配置：输入处理器需要连接建立钩子登记的通道信息。");

            return _socketOption;
        }
    }
}
