/* SAEA.Socket5
 * 命名空间：SAEA.Socket5.Client
 * 文件名：Socks5Client.cs
 * 版本号：v26.9.20.1
 * 描述：SOCKS5 客户端（基于 SAEA.Sockets.IClientSocket）
 */

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

using SAEA.Sockets;
using SAEA.Socket5.Model;
using SAEA.Socket5.Protocol;

namespace SAEA.Socket5.Client
{
    /// <summary>
    /// SOCKS5 客户端。
    /// 一个实例对应一条到代理的 TCP 连接，仅支持发起一次命令（CONNECT / BIND / UDP ASSOCIATE），
    /// 这是 SOCKS5 协议本身的限制（一条连接只处理一个请求）；需要多个目标时请创建多个实例。
    /// </summary>
    public class Socks5Client : IDisposable
    {
        private readonly Socks5ClientOptions _options;

        private IClientSocket _client;

        private Stream _stream;

        private bool _commandIssued;

        /// <summary>
        /// SOCKS5 客户端
        /// </summary>
        /// <param name="options">客户端配置</param>
        public Socks5Client(Socks5ClientOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// 命令前置处理：校验单次命令约束 → 建立到代理的连接 → 方法协商与鉴权
        /// </summary>
        private void PrepareForCommand()
        {
            if (_commandIssued)
            {
                throw new InvalidOperationException("一个 Socks5Client 实例仅支持发起一次 SOCKS5 命令（CONNECT/BIND/UDP ASSOCIATE），请为每个命令创建新的实例");
            }

            // 先建连（失败可重试），连上后再锁定为单次命令，避免在已协商的流上重复协商
            EnsureConnected();

            _commandIssued = true;

            NegotiateAndAuthenticate();
        }

        private void EnsureConnected()
        {
            if (_client != null && _client.Connected)
            {
                return;
            }

            var socketOption = SocketOptionBuilder.Instance
                .UseStream()
                .SetIP(_options.ProxyHost)
                .SetPort(_options.ProxyPort)
                .SetActionTimeOut(_options.Timeout)
                .Build();

            _client = SocketFactory.CreateClientSocket(socketOption);

            if (!string.IsNullOrEmpty(_options.BindIP))
            {
                _client.Bind(_options.BindIP);
            }

            _client.Connect();
            _stream = _client.GetStream();
        }

        private void NegotiateAndAuthenticate()
        {
            var methods = string.IsNullOrEmpty(_options.UserName)
                ? new[] { Socks5AuthMethod.NoAuth }
                : new[] { Socks5AuthMethod.NoAuth, Socks5AuthMethod.UserPass };

            Socks5Codec.WriteMethodNegotiation(_stream, methods);

            var selected = Socks5Codec.ReadMethodSelection(_stream);

            if (selected == Socks5AuthMethod.NoAcceptable)
            {
                throw new InvalidOperationException("SOCKS5 代理拒绝所有认证方式");
            }

            if (selected == Socks5AuthMethod.UserPass)
            {
                if (string.IsNullOrEmpty(_options.UserName))
                {
                    throw new InvalidOperationException("SOCKS5 代理要求用户名/密码认证，但客户端未提供凭据");
                }

                Socks5Codec.WriteUserPassAuth(_stream, _options.UserName, _options.Password);

                var status = Socks5Codec.ReadUserPassStatus(_stream);

                if (status != Socks5AuthStatus.Success)
                {
                    throw new InvalidOperationException("SOCKS5 用户名/密码认证失败");
                }
            }
        }

        /// <summary>
        /// 通过代理建立到目标主机的 TCP 连接，返回透明隧道流
        /// </summary>
        public Stream Connect(string host, int port)
        {
            if (string.IsNullOrEmpty(host)) throw new ArgumentNullException(nameof(host));

            PrepareForCommand();

            var addressType = Socks5Codec.InferAddressType(host);

            Socks5Codec.WriteRequest(_stream, Socks5Command.Connect, addressType, host, port);

            var reply = Socks5Codec.ReadReply(_stream);

            if (reply.ReplyCode != Socks5ReplyCode.Succeeded)
            {
                throw new InvalidOperationException("SOCKS5 CONNECT 失败：" + reply.ReplyCode);
            }

            if (_stream.CanTimeout)
            {
                _stream.ReadTimeout = -1;
                _stream.WriteTimeout = -1;
            }

            return _stream;
        }

        /// <summary>
        /// 异步建立 CONNECT 隧道
        /// </summary>
        public Task<Stream> ConnectAsync(string host, int port)
        {
            return Task.Run(() => Connect(host, port));
        }

        /// <summary>
        /// 通过代理发起 BIND 请求，返回首次响应与等待二次响应的句柄
        /// </summary>
        public Socks5BindResult Bind(string host, int port)
        {
            PrepareForCommand();

            var addressType = Socks5Codec.InferAddressType(host);

            Socks5Codec.WriteRequest(_stream, Socks5Command.Bind, addressType, host, port);

            var firstReply = Socks5Codec.ReadReply(_stream);

            if (firstReply.ReplyCode != Socks5ReplyCode.Succeeded)
            {
                throw new InvalidOperationException("SOCKS5 BIND 失败：" + firstReply.ReplyCode);
            }

            if (_stream.CanTimeout)
            {
                _stream.ReadTimeout = -1;
                _stream.WriteTimeout = -1;
            }

            return new Socks5BindResult
            {
                ControlStream = _stream,
                FirstReply = firstReply,
                WaitForBindComplete = () => Socks5Codec.ReadReply(_stream)
            };
        }

        /// <summary>
        /// 异步发起 BIND
        /// </summary>
        public Task<Socks5BindResult> BindAsync(string host, int port)
        {
            return Task.Run(() => Bind(host, port));
        }

        /// <summary>
        /// 通过代理发起 UDP 关联，返回可用于收发 UDP 数据报的客户端
        /// </summary>
        public Socks5UdpClient UdpAssociate()
        {
            PrepareForCommand();

            Socks5Codec.WriteRequest(_stream, Socks5Command.UdpAssociate, Socks5AddressType.IPv4, "0.0.0.0", 0);

            var reply = Socks5Codec.ReadReply(_stream);

            if (reply.ReplyCode != Socks5ReplyCode.Succeeded)
            {
                throw new InvalidOperationException("SOCKS5 UDP ASSOCIATE 失败：" + reply.ReplyCode);
            }

            var relayEndPoint = new IPEndPoint(IPAddress.Parse(reply.Host), reply.Port);

            var udp = new UdpClient(0);

            if (_stream.CanTimeout)
            {
                _stream.ReadTimeout = -1;
                _stream.WriteTimeout = -1;
            }

            return new Socks5UdpClient(this, udp, relayEndPoint);
        }

        /// <summary>
        /// 异步发起 UDP 关联
        /// </summary>
        public Task<Socks5UdpClient> UdpAssociateAsync()
        {
            return Task.Run(() => UdpAssociate());
        }

        /// <inheritdoc />
        public void Dispose()
        {
            try
            {
                _client?.Dispose();
            }
            catch { }

            _client = null;
            _stream = null;
        }
    }
}
