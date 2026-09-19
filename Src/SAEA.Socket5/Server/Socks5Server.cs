/* SAEA.Socket5
 * 命名空间：SAEA.Socket5.Server
 * 文件名：Socks5Server.cs
 * 版本号：v26.9.20.1
 * 描述：SOCKS5 服务端（基于 SAEA.Sockets.IServerSocket）
 */

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Sockets;
using SAEA.Sockets.Model;
using SAEA.Socket5.Model;
using SAEA.Socket5.Protocol;

namespace SAEA.Socket5.Server
{
    /// <summary>
    /// SOCKS5 代理服务端
    /// </summary>
    public class Socks5Server : IDisposable
    {
        private readonly Socks5ServerOptions _options;

        private IServerSocket _server;

        /// <summary>
        /// 取代理与客户端建连时使用的本地终结点地址（规整为 IPv4），用于 BIND / UDP ASSOCIATE 的中继绑定与上报，
        /// 保证客户端能沿与控制连接相同的可达地址回访中继端口（而非 0.0.0.0 / ::1 这类不可达地址）。
        /// </summary>
        private static IPAddress GetBindAddress(ChannelInfo channel)
        {
            // 会话或套接字缺失时回退到回环地址，避免空引用
            if (channel?.ClientSocket?.LocalEndPoint is IPEndPoint ep)
            {
                return NormalizeToIPv4(ep.Address);
            }

            return IPAddress.Loopback;
        }

        /// <summary>
        /// SOCKS5 服务端
        /// </summary>
        /// <param name="options">服务端配置</param>
        public Socks5Server(Socks5ServerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// 启动代理服务
        /// </summary>
        public void Start()
        {
            if (_server != null)
            {
                return;
            }

            var socketOption = SocketOptionBuilder.Instance
                .UseStream()
                .SetIP(_options.IP)
                .SetPort(_options.Port)
                .SetReadBufferSize(_options.BufferSize)
                .SetActionTimeOut(_options.ActionTimeout)
                .Build();

            _server = SocketFactory.CreateServerSocket(socketOption);
            _server.OnAccepted += OnClientAccepted;
            _server.Start();
        }

        /// <summary>
        /// 停止代理服务
        /// </summary>
        public void Stop()
        {
            try
            {
                _server?.Stop();
            }
            catch { }

            _server = null;
        }

        private void OnClientAccepted(object obj)
        {
            // SAEA 在会话登记失败时可能抛出 null（ChannelManager.Set 对空 id 返回 null），此处需容错
            if (obj is ChannelInfo channel && channel.Stream != null)
            {
                // 会话处理全程阻塞（握手 + 双向中继），使用专用线程而非线程池线程，
                // 避免大量并发会话时耗尽线程池导致吞吐急剧下降。
                Task.Factory.StartNew(() => HandleSession(channel), TaskCreationOptions.LongRunning);
            }
        }

        private void HandleSession(ChannelInfo channel)
        {
            if (channel == null || channel.Stream == null)
            {
                return;
            }

            var stream = channel.Stream;

            try
            {
                if (stream.CanTimeout)
                {
                    stream.ReadTimeout = _options.ActionTimeout;
                }

                var clientMethods = Socks5Codec.ReadMethodNegotiation(stream);

                var selected = ChooseMethod(clientMethods);

                Socks5Codec.WriteMethodSelection(stream, selected);

                if (selected == Socks5AuthMethod.NoAcceptable)
                {
                    return;
                }

                if (selected == Socks5AuthMethod.UserPass)
                {
                    Socks5Codec.ReadUserPassAuth(stream, out var userName, out var password);

                    var ok = _options.UserValidator?.Validate(userName, password) ?? false;

                    Socks5Codec.WriteUserPassStatus(stream, ok ? Socks5AuthStatus.Success : Socks5AuthStatus.Failure);

                    if (!ok)
                    {
                        return;
                    }
                }

                var request = Socks5Codec.ReadRequest(stream);

                switch (request.Command)
                {
                    case Socks5Command.Connect:
                        HandleConnect(channel, stream, request);
                        break;
                    case Socks5Command.Bind:
                        HandleBind(channel, stream, request);
                        break;
                    case Socks5Command.UdpAssociate:
                        HandleUdpAssociate(channel, stream, request);
                        break;
                    default:
                        WriteFailureReply(stream, Socks5ReplyCode.CommandNotSupported);
                        break;
                }
            }
            catch (EndOfStreamException)
            {
                // 客户端关闭连接
            }
            catch (Exception)
            {
                // 握手或处理异常，忽略
            }
            finally
            {
                // 兜底关闭本会话套接字；再交由 SAEA 回收会话。
                // 会话可能已被 Stop()/Clear() 回收或已被移除，Disconnect 内部已做判空保护。
                try { channel.ClientSocket?.Close(); } catch { }

                try
                {
                    _server?.Disconnect(channel.ID);
                }
                catch { }
            }
        }

        private Socks5AuthMethod ChooseMethod(Socks5AuthMethod[] clientMethods)
        {
            var allowed = _options.AllowedMethods ?? new[] { Socks5AuthMethod.NoAuth };

            if (allowed.Contains(Socks5AuthMethod.NoAuth) && clientMethods.Contains(Socks5AuthMethod.NoAuth))
            {
                return Socks5AuthMethod.NoAuth;
            }

            if (allowed.Contains(Socks5AuthMethod.UserPass) && clientMethods.Contains(Socks5AuthMethod.UserPass))
            {
                return Socks5AuthMethod.UserPass;
            }

            return Socks5AuthMethod.NoAcceptable;
        }

        private void HandleConnect(ChannelInfo channel, Stream stream, Socks5Request request)
        {
            TcpClient target = null;

            try
            {
                var candidates = ResolveTargets(request);

                if (candidates.Length == 0)
                {
                    WriteFailureReply(stream, Socks5ReplyCode.HostUnreachable);
                    return;
                }

                SocketException lastError = null;

                // 逐个尝试候选地址，避免首个地址（如 IPv6）不可达时直接失败
                foreach (var ip in candidates)
                {
                    try
                    {
                        target = new TcpClient(ip.AddressFamily);
                        target.Connect(ip, request.Port);
                        lastError = null;
                        break;
                    }
                    catch (SocketException sx)
                    {
                        lastError = sx;
                        try { target?.Close(); } catch { }
                        target = null;
                    }
                }

                if (target == null)
                {
                    WriteFailureReply(stream, lastError == null ? Socks5ReplyCode.GeneralFailure : MapSocketError(lastError));
                    return;
                }

                var targetStream = target.GetStream();

                if (targetStream.CanTimeout)
                {
                    targetStream.ReadTimeout = -1;
                    targetStream.WriteTimeout = -1;
                }

                Socks5Codec.WriteReply(stream, Socks5ReplyCode.Succeeded, Socks5AddressType.IPv4, "0.0.0.0", 0);

                Pump(stream, targetStream, channel);

                try { target.Close(); } catch { }
            }
            catch (SocketException sx)
            {
                WriteFailureReply(stream, MapSocketError(sx));

                try { target?.Close(); } catch { }
            }
            catch
            {
                WriteFailureReply(stream, Socks5ReplyCode.GeneralFailure);

                try { target?.Close(); } catch { }
            }
        }

        private void HandleBind(ChannelInfo channel, Stream stream, Socks5Request request)
        {
            TcpListener listener = null;

            try
            {
                var bindIP = GetBindAddress(channel);

                listener = new TcpListener(bindIP, 0);
                listener.Start();

                var localEP = (IPEndPoint)listener.LocalEndpoint;
                var reportIP = NormalizeToIPv4(localEP.Address);

                Socks5Codec.WriteReply(stream, Socks5ReplyCode.Succeeded, Socks5Codec.InferAddressType(reportIP.ToString()), reportIP.ToString(), localEP.Port);

                var acceptTask = listener.AcceptTcpClientAsync();

                if (!acceptTask.Wait(_options.ActionTimeout))
                {
                    throw new TimeoutException("BIND 等待远端连入超时");
                }

                var remote = acceptTask.Result;
                var peerEP = (IPEndPoint)remote.Client.RemoteEndPoint;
                var peerIP = NormalizeToIPv4(peerEP.Address);

                Socks5Codec.WriteReply(stream, Socks5ReplyCode.Succeeded, Socks5Codec.InferAddressType(peerIP.ToString()), peerIP.ToString(), peerEP.Port);

                var remoteStream = remote.GetStream();

                if (remoteStream.CanTimeout)
                {
                    remoteStream.ReadTimeout = -1;
                    remoteStream.WriteTimeout = -1;
                }

                Pump(stream, remoteStream, channel);

                try { remote.Close(); } catch { }
            }
            catch
            {
                WriteFailureReply(stream, Socks5ReplyCode.GeneralFailure);
            }
            finally
            {
                try { listener?.Stop(); } catch { }
            }
        }

        private void HandleUdpAssociate(ChannelInfo channel, Stream stream, Socks5Request request)
        {
            Socket relay = null;
            var cts = new CancellationTokenSource();

            try
            {
                var bindIP = GetBindAddress(channel);

                relay = new Socket(bindIP.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                relay.Bind(new IPEndPoint(bindIP, 0));

                var relayEP = (IPEndPoint)relay.LocalEndPoint;
                var reportIP = NormalizeToIPv4(relayEP.Address);

                Socks5Codec.WriteReply(stream, Socks5ReplyCode.Succeeded, Socks5Codec.InferAddressType(reportIP.ToString()), reportIP.ToString(), relayEP.Port);

                // 仅接受来自已认证 TCP 对端（客户端）IP 的数据报作为客户端端点，避免被任意外部源抢先注册
                var clientIP = channel.ClientSocket?.RemoteEndPoint is IPEndPoint clientRemote
                    ? NormalizeToIPv4(clientRemote.Address)
                    : IPAddress.Loopback;

                _ = Task.Run(() => UdpRelayLoop(relay, cts, clientIP));

                // 维持 TCP 控制连接存活，直到客户端关闭；控制连接不设超时（UDP 关联生命周期 == TCP 控制连接生命周期）
                if (stream.CanTimeout)
                {
                    stream.ReadTimeout = -1;
                }

                try
                {
                    var tmp = new byte[1];
                    stream.Read(tmp, 0, 1);
                }
                catch { }
            }
            catch
            {
                WriteFailureReply(stream, Socks5ReplyCode.GeneralFailure);
            }
            finally
            {
                try { cts.Cancel(); } catch { }
                try { relay?.Close(); } catch { }

                try { channel?.ClientSocket?.Close(); } catch { }
            }
        }

        private void UdpRelayLoop(Socket relay, CancellationTokenSource cts, IPAddress clientIP)
        {
            EndPoint clientUdpEP = null;

            var placeholder = relay.LocalEndPoint.AddressFamily == AddressFamily.InterNetworkV6
                ? (EndPoint)new IPEndPoint(IPAddress.IPv6Any, 0)
                : new IPEndPoint(IPAddress.Any, 0);

            var buffer = new byte[65535];

            try
            {
                while (!cts.IsCancellationRequested)
                {
                    EndPoint remoteEP = placeholder;
                    int n = relay.ReceiveFrom(buffer, ref remoteEP);

                    if (n <= 0)
                    {
                        continue;
                    }

                    var remoteIP = NormalizeToIPv4(((IPEndPoint)remoteEP).Address);

                    // 锁定客户端 UDP 端点：仅接受来自已认证 TCP 对端 IP 的首个数据报
                    if (clientUdpEP == null)
                    {
                        if (IsSameAddress(remoteIP, clientIP))
                        {
                            clientUdpEP = remoteEP;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    if (IsSameEndPoint(remoteEP, clientUdpEP))
                    {
                        // 来自客户端的数据报：转发到其指定的目标
                        if (Socks5Codec.TryDecodeUdpDatagram(SubArray(buffer, 0, n), out _, out var host, out var port, out var data))
                        {
                            var targetEP = new IPEndPoint(NormalizeToIPv4(IPAddress.Parse(host)), port);
                            relay.SendTo(data, targetEP);
                        }
                    }
                    else
                    {
                        // 来自目标主机的响应：封装后回传给客户端 UDP 端点
                        var fromEP = (IPEndPoint)remoteEP;
                        var fromIP = NormalizeToIPv4(fromEP.Address);
                        var wrapped = Socks5Codec.EncodeUdpDatagram(
                            Socks5Codec.InferAddressType(fromIP.ToString()),
                            fromIP.ToString(),
                            fromEP.Port,
                            SubArray(buffer, 0, n));

                        relay.SendTo(wrapped, clientUdpEP);
                    }
                }
            }
            catch
            {
                // 中继循环结束
            }
        }

        private void Pump(Stream client, Stream target, ChannelInfo channel)
        {
            if (client.CanTimeout)
            {
                client.ReadTimeout = -1;
                client.WriteTimeout = -1;
            }

            using var cts = new CancellationTokenSource();

            // 两个方向的中继都会长时间阻塞在 Stream.Read 上，改用专用线程，
            // 避免并发会话数上升时阻塞线程池线程（每会话 3 个）导致整体吞吐崩塌。
            var t1 = Task.Factory.StartNew(() => Relay(client, target, cts), TaskCreationOptions.LongRunning);
            var t2 = Task.Factory.StartNew(() => Relay(target, client, cts), TaskCreationOptions.LongRunning);

            Task.WaitAny(t1, t2);

            cts.Cancel();

            try { client.Close(); } catch { }
            try { target.Close(); } catch { }
        }

        private void Relay(Stream src, Stream dst, CancellationTokenSource cts)
        {
            var buf = new byte[_options.BufferSize];

            try
            {
                while (!cts.IsCancellationRequested)
                {
                    int n = src.Read(buf, 0, buf.Length);

                    if (n <= 0)
                    {
                        break;
                    }

                    dst.Write(buf, 0, n);
                }
            }
            catch
            {
                // 任意一侧断开
            }
            finally
            {
                try { cts.Cancel(); } catch { }
            }
        }

        private void WriteFailureReply(Stream stream, Socks5ReplyCode code)
        {
            try
            {
                Socks5Codec.WriteReply(stream, code, Socks5AddressType.IPv4, "0.0.0.0", 0);
            }
            catch { }
        }

        /// <summary>
        /// 解析目标地址；域名场景下按 IPv4 优先排序，避免在无 IPv6 路由的环境下首选 IPv6 而导致连接失败
        /// </summary>
        private static IPAddress[] ResolveTargets(Socks5Request request)
        {
            if (request.AddressType != Socks5AddressType.Domain)
            {
                return new[] { IPAddress.Parse(request.Host) };
            }

            try
            {
                return Dns.GetHostAddresses(request.Host)
                    .OrderByDescending(a => a.AddressFamily == AddressFamily.InterNetwork)
                    .ToArray();
            }
            catch (SocketException)
            {
                return new IPAddress[0];
            }
        }

        private static Socks5ReplyCode MapSocketError(SocketException sx)
        {
            switch (sx.SocketErrorCode)
            {
                case SocketError.ConnectionRefused:
                    return Socks5ReplyCode.ConnectionRefused;
                case SocketError.HostUnreachable:
                    return Socks5ReplyCode.HostUnreachable;
                case SocketError.NetworkUnreachable:
                    return Socks5ReplyCode.NetworkUnreachable;
                case SocketError.TimedOut:
                    return Socks5ReplyCode.TtlExpired;
                case SocketError.AccessDenied:
                    return Socks5ReplyCode.ConnectionNotAllowed;
                default:
                    return Socks5ReplyCode.GeneralFailure;
            }
        }

        private static bool IsSameEndPoint(EndPoint a, EndPoint b)
        {
            if (a is IPEndPoint x && b is IPEndPoint y)
            {
                return x.Address.Equals(y.Address) && x.Port == y.Port;
            }

            return a.Equals(b);
        }

        private static bool IsSameAddress(IPAddress a, IPAddress b)
        {
            return NormalizeToIPv4(a).Equals(NormalizeToIPv4(b));
        }

        /// <summary>
        /// 将 IPv6 映射的 IPv4 地址（[::ffff:x.x.x.x]）规整回纯 IPv4，避免向客户端报告不可达的映射地址。
        /// </summary>
        private static IPAddress NormalizeToIPv4(IPAddress address)
        {
            if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6)
            {
                return address.MapToIPv4();
            }

            return address;
        }

        private static byte[] SubArray(byte[] src, int index, int length)
        {
            var dst = new byte[length];
            Buffer.BlockCopy(src, index, dst, 0, length);
            return dst;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Stop();
        }
    }
}
