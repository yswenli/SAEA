/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.P2P.Tests.Nat
*文件名： NatBox
*版本号： net10.0
*唯一标识：e4cb2db4-ee50-4102-b77c-6ee5ddca3a8b
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 13:45:00
*描述：用户态 NAT 代理模拟器
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 13:45:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 UDP/TCP 用户态 NAT 代理
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.P2P.Tests.Nat
{
    /// <summary>
    /// 用户态 NAT 代理：内部主机把报文发到 <see cref="InternalGateway"/>，
    /// NAT 用绑定在 <see cref="ExternalAddress"/> 的套接字重新发出，回程按映射表反查改写。
    /// 只用 <see cref="System.Net.Sockets"/>，不引用被测实现，避免互相掩护。
    /// </summary>
    public sealed class NatBox : IDisposable
    {
        /// <summary>
        /// 单个数据报的最大接收长度
        /// </summary>
        private const int BufferSize = 65535;

        /// <summary>
        /// 保护映射表
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 映射表，UDP 与 TCP 共用
        /// </summary>
        private readonly List<NatMapping> _mappings = new List<NatMapping>();

        /// <summary>
        /// 关闭全部循环与套接字的取消源
        /// </summary>
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        /// <summary>
        /// 可注入时钟，用于把映射超时压到用例可接受的时长
        /// </summary>
        private readonly Func<DateTime> _clock;

        /// <summary>
        /// UDP 内部网关
        /// </summary>
        private readonly Socket _udpGateway;

        /// <summary>
        /// TCP 内部网关，承接内部主机的出向连接
        /// </summary>
        private readonly TcpListener _tcpGateway;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 对外地址：外部映射与静态监听都绑在这里
        /// </summary>
        public IPAddress ExternalAddress { get; }

        /// <summary>
        /// 行为配置
        /// </summary>
        public NatConfig Config { get; }

        /// <summary>
        /// 内部主机的 UDP 下一跳端点
        /// </summary>
        public IPEndPoint InternalGateway { get; }

        /// <summary>
        /// 内部主机的 TCP 下一跳端点
        /// </summary>
        public IPEndPoint InternalTcpGateway { get; }

        /// <summary>
        /// 当前映射表快照
        /// </summary>
        public IReadOnlyList<NatMapping> Mappings
        {
            get
            {
                lock (_sync) return _mappings.ToList();
            }
        }

        /// <summary>
        /// 创建 NAT 代理并开始在两个内部网关上收发
        /// </summary>
        /// <param name="externalAddress">对外地址</param>
        /// <param name="config">行为配置，默认全锥形</param>
        /// <param name="clock">可注入时钟，默认 <see cref="DateTime.UtcNow"/></param>
        public NatBox(IPAddress externalAddress, NatConfig config = null, Func<DateTime> clock = null)
        {
            ExternalAddress = externalAddress ?? throw new ArgumentNullException(nameof(externalAddress));
            Config = config ?? NatPreset.FullCone();
            _clock = clock ?? (() => DateTime.UtcNow);

            _udpGateway = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _udpGateway.Bind(new IPEndPoint(externalAddress, 0));
            InternalGateway = (IPEndPoint)_udpGateway.LocalEndPoint;

            _tcpGateway = new TcpListener(externalAddress, 0);
            _tcpGateway.Start(32);
            InternalTcpGateway = (IPEndPoint)_tcpGateway.LocalEndpoint;

            _ = Task.Run(() => UdpGatewayLoopAsync(_cts.Token));
            _ = Task.Run(() => TcpGatewayLoopAsync(_cts.Token));
        }

        /// <summary>
        /// 查找某个内部端点的 UDP 映射；地址相关映射可能有多条，返回第一条
        /// </summary>
        /// <param name="internalEndPoint">内部端点</param>
        /// <param name="mapping">命中的映射</param>
        /// <returns>找到时为 true</returns>
        public bool TryGetMapping(IPEndPoint internalEndPoint, out NatMapping mapping)
        {
            mapping = null;
            if (internalEndPoint == null) return false;

            lock (_sync)
            {
                mapping = _mappings.FirstOrDefault(m => m.Protocol == ProtocolType.Udp && m.InternalEndPoint.Equals(internalEndPoint));
                return mapping != null;
            }
        }

        /// <summary>
        /// 登记 TCP 静态端口映射：外部地址的指定端口转发到内部端点
        /// </summary>
        /// <param name="internalEndPoint">内部端点</param>
        /// <param name="externalPort">外部端口，0 表示由系统分配</param>
        /// <returns>静态映射记录，外部端点即监听端点</returns>
        public NatMapping AddStaticTcpMapping(IPEndPoint internalEndPoint, int externalPort = 0)
        {
            if (internalEndPoint == null) throw new ArgumentNullException(nameof(internalEndPoint));

            var listener = new TcpListener(ExternalAddress, externalPort);
            listener.Start(32);

            var mapping = new NatMapping(
                "T|static|" + internalEndPoint,
                ProtocolType.Tcp,
                internalEndPoint,
                (IPEndPoint)listener.LocalEndpoint,
                true,
                Now());
            mapping.Socket = listener.Server;

            lock (_sync)
            {
                if (_disposed)
                {
                    listener.Stop();
                    throw new ObjectDisposedException(GetType().Name);
                }
                _mappings.Add(mapping);
            }

            _ = Task.Run(() => StaticTcpAcceptLoopAsync(mapping, listener, _cts.Token));
            return mapping;
        }

        /// <summary>
        /// 清理已经空闲超时的映射，关闭其外部套接字
        /// </summary>
        /// <returns>被清理的映射条数</returns>
        public int PurgeExpiredMappings()
        {
            lock (_sync)
            {
                return PurgeExpiredLocked();
            }
        }

        /// <summary>
        /// 释放 NAT：停止全部循环并关闭映射套接字
        /// </summary>
        public void Dispose()
        {
            List<NatMapping> mappings;
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
                mappings = _mappings.ToList();
                _mappings.Clear();
            }

            try { _cts.Cancel(); } catch { }
            try { _udpGateway.Dispose(); } catch { }
            try { _tcpGateway.Stop(); } catch { }

            foreach (var mapping in mappings)
            {
                try { mapping.Socket?.Dispose(); } catch { }
            }

            _cts.Dispose();
        }

        /// <summary>
        /// 当前时刻
        /// </summary>
        /// <returns>注入时钟的读数</returns>
        private DateTime Now()
        {
            return _clock();
        }

        /// <summary>
        /// 按映射行为生成 UDP 映射键
        /// </summary>
        /// <param name="internalEndPoint">内部端点</param>
        /// <param name="destination">出向目标</param>
        /// <returns>映射键</returns>
        private string BuildUdpKey(IPEndPoint internalEndPoint, IPEndPoint destination)
        {
            switch (Config.Mapping)
            {
                case NatMappingBehavior.AddressDependent:
                    return "U|" + internalEndPoint + "|" + destination.Address;
                case NatMappingBehavior.AddressAndPortDependent:
                    return "U|" + internalEndPoint + "|" + destination;
                default:
                    return "U|" + internalEndPoint;
            }
        }

        /// <summary>
        /// 取或创建内部端点到目标的 UDP 映射
        /// </summary>
        /// <param name="internalEndPoint">内部端点</param>
        /// <param name="destination">出向目标</param>
        /// <returns>映射记录</returns>
        private NatMapping GetOrCreateUdpMapping(IPEndPoint internalEndPoint, IPEndPoint destination)
        {
            var key = BuildUdpKey(internalEndPoint, destination);

            lock (_sync)
            {
                PurgeExpiredLocked();

                var existing = _mappings.FirstOrDefault(m => m.Protocol == ProtocolType.Udp && m.Key == key);
                if (existing != null) return existing;

                var socket = BindExternalUdp(internalEndPoint.Port);
                var mapping = new NatMapping(key, ProtocolType.Udp, internalEndPoint, (IPEndPoint)socket.LocalEndPoint, false, Now());
                mapping.Socket = socket;
                _mappings.Add(mapping);

                _ = Task.Run(() => ExternalUdpLoopAsync(mapping, socket, _cts.Token));
                return mapping;
            }
        }

        /// <summary>
        /// 绑定外部 UDP 套接字；要求端口被占用时退回系统分配
        /// </summary>
        /// <param name="preferredPort">端口保持时的期望端口，0 表示直接由系统分配</param>
        /// <returns>已绑定的套接字</returns>
        private Socket BindExternalUdp(int preferredPort)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            try
            {
                // 端口保持只在与内部网关端口不冲突时尝试，冲突说明该端口已被 NAT 自己占用
                var port = Config.PortPreservation && preferredPort != 0 && preferredPort != InternalGateway.Port
                    ? preferredPort
                    : 0;
                socket.Bind(new IPEndPoint(ExternalAddress, port));
                return socket;
            }
            catch (SocketException)
            {
                socket.Dispose();
                var fallback = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                fallback.Bind(new IPEndPoint(ExternalAddress, 0));
                return fallback;
            }
        }

        /// <summary>
        /// 在锁内清理超时映射
        /// </summary>
        /// <returns>被清理的映射条数</returns>
        private int PurgeExpiredLocked()
        {
            var now = Now();
            var expired = _mappings.Where(m => m.IsExpired(now, Config.MappingLifetime)).ToList();
            foreach (var mapping in expired)
            {
                _mappings.Remove(mapping);
                try { mapping.Socket?.Dispose(); } catch { }
            }
            return expired.Count;
        }

        /// <summary>
        /// 按外部端点查找 UDP 映射
        /// </summary>
        /// <param name="externalEndPoint">外部端点</param>
        /// <returns>命中的映射，没有则为 null</returns>
        private NatMapping FindUdpByExternalLocked(IPEndPoint externalEndPoint)
        {
            return _mappings.FirstOrDefault(m => m.Protocol == ProtocolType.Udp && m.ExternalEndPoint.Equals(externalEndPoint));
        }

        /// <summary>
        /// 处理内部主机发出的 UDP 报文
        /// </summary>
        /// <param name="fromInternal">内部来源端点</param>
        /// <param name="destination">报文声明的目标端点</param>
        /// <param name="payload">载荷</param>
        private void ProcessOutbound(IPEndPoint fromInternal, IPEndPoint destination, ArraySegment<byte> payload)
        {
            // 目标是 NAT 自己的外部端口，按 hairpin 回环到另一台内部主机
            if (destination.Address.Equals(ExternalAddress))
            {
                NatMapping target;
                lock (_sync)
                {
                    PurgeExpiredLocked();
                    target = FindUdpByExternalLocked(destination);
                }

                if (target != null)
                {
                    ProcessHairpin(fromInternal, target, payload);
                    return;
                }

                // 目标是 NAT 自家外部地址却没有对应映射：外部从未开放过该端口，直接丢弃，
                // 否则会为本机地址再建一条出向映射，绕开入向过滤
                return;
            }

            var mapping = GetOrCreateUdpMapping(fromInternal, destination);

            mapping.Permit(destination);
            mapping.Touch(Now());

            mapping.Socket.SendTo(payload.Array, payload.Offset, payload.Count, SocketFlags.None, destination);
        }

        /// <summary>
        /// 处理同一 NAT 后两台主机的回环转发
        /// </summary>
        /// <param name="fromInternal">发起方内部端点</param>
        /// <param name="target">目标映射</param>
        /// <param name="payload">载荷</param>
        private void ProcessHairpin(IPEndPoint fromInternal, NatMapping target, ArraySegment<byte> payload)
        {
            if (!Config.Hairpinning) return;

            var source = GetOrCreateUdpMapping(fromInternal, target.ExternalEndPoint);

            // 目标主机的入向过滤仍然生效：它从未访问过发起方的外部端点时不允许回环直投
            if (!NatFilter.Allows(target, source.ExternalEndPoint, Config.Filter)) return;

            source.Permit(target.ExternalEndPoint);
            source.Touch(Now());
            target.Touch(Now());

            var packet = NatPacket.Encode(source.ExternalEndPoint, new ReadOnlySpan<byte>(payload.Array, payload.Offset, payload.Count));
            _udpGateway.SendTo(packet, target.InternalEndPoint);
        }

        /// <summary>
        /// 处理外部套接字收到的 UDP 报文，按映射表反查后转给内部主机
        /// </summary>
        /// <param name="mapping">命中的映射</param>
        /// <param name="remote">外部来源端点</param>
        /// <param name="payload">载荷</param>
        private void ProcessInbound(NatMapping mapping, IPEndPoint remote, ArraySegment<byte> payload)
        {
            lock (_sync)
            {
                PurgeExpiredLocked();

                if (!_mappings.Contains(mapping)) return;

                // 目标是自家外部地址（回环）时，hairpin 关闭就丢弃
                if (!Config.Hairpinning && remote.Address.Equals(ExternalAddress)) return;

                if (!NatFilter.Allows(mapping, remote, Config.Filter)) return;

                mapping.Touch(Now());
            }

            var packet = NatPacket.Encode(remote, new ReadOnlySpan<byte>(payload.Array, payload.Offset, payload.Count));
            _udpGateway.SendTo(packet, mapping.InternalEndPoint);
        }

        /// <summary>
        /// UDP 内部网关接收循环
        /// </summary>
        /// <param name="cancellationToken">关闭令牌</param>
        /// <returns>循环结束的任务</returns>
        private async Task UdpGatewayLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[BufferSize];
            EndPoint any = new IPEndPoint(IPAddress.Any, 0);

            while (!cancellationToken.IsCancellationRequested)
            {
                SocketReceiveFromResult result;
                try
                {
                    result = await _udpGateway.ReceiveFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None, any, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { continue; }

                if (result.ReceivedBytes < NatPacket.HeaderLength) continue;

                IPEndPoint destination;
                ArraySegment<byte> payload;
                if (!NatPacket.TryDecode(buffer, 0, result.ReceivedBytes, out destination, out payload)) continue;

                var from = result.RemoteEndPoint as IPEndPoint;
                if (from == null) continue;

                try
                {
                    ProcessOutbound(from, destination, payload);
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { }
            }
        }

        /// <summary>
        /// 某个映射的外部 UDP 接收循环
        /// </summary>
        /// <param name="mapping">所属映射</param>
        /// <param name="socket">外部套接字</param>
        /// <param name="cancellationToken">关闭令牌</param>
        /// <returns>循环结束的任务</returns>
        private async Task ExternalUdpLoopAsync(NatMapping mapping, Socket socket, CancellationToken cancellationToken)
        {
            var buffer = new byte[BufferSize];
            EndPoint any = new IPEndPoint(IPAddress.Any, 0);

            while (!cancellationToken.IsCancellationRequested)
            {
                SocketReceiveFromResult result;
                try
                {
                    result = await socket.ReceiveFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None, any, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { continue; }

                var remote = result.RemoteEndPoint as IPEndPoint;
                if (remote == null) continue;

                try
                {
                    ProcessInbound(mapping, remote, new ArraySegment<byte>(buffer, 0, result.ReceivedBytes));
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { }
            }
        }

        /// <summary>
        /// TCP 内部网关接收循环：处理内部主机的出向连接
        /// </summary>
        /// <param name="cancellationToken">关闭令牌</param>
        /// <returns>循环结束的任务</returns>
        private async Task TcpGatewayLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Socket client;
                try
                {
                    client = await _tcpGateway.AcceptSocketAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { continue; }

                _ = Task.Run(() => HandleOutboundTcpAsync(client));
            }
        }

        /// <summary>
        /// 读出向连接的目标端点并连接到外部，随后双向转发
        /// </summary>
        /// <param name="client">内部主机连接</param>
        /// <returns>处理结束的任务</returns>
        private async Task HandleOutboundTcpAsync(Socket client)
        {
            Socket external = null;
            try
            {
                var header = new byte[NatPacket.HeaderLength];
                var offset = 0;
                while (offset < header.Length)
                {
                    var read = await client.ReceiveAsync(new ArraySegment<byte>(header, offset, header.Length - offset), SocketFlags.None).ConfigureAwait(false);
                    if (read <= 0) return;
                    offset += read;
                }

                IPEndPoint destination;
                ArraySegment<byte> ignored;
                if (!NatPacket.TryDecode(header, 0, header.Length, out destination, out ignored)) return;

                // client 是内部主机连到网关的这条连接，内部端点在对端而不是本地端
                var internalEndPoint = (IPEndPoint)client.RemoteEndPoint;

                external = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                if (Config.PortPreservation && internalEndPoint.Port != InternalTcpGateway.Port)
                {
                    try { external.Bind(new IPEndPoint(ExternalAddress, internalEndPoint.Port)); }
                    catch (SocketException) { external.Bind(new IPEndPoint(ExternalAddress, 0)); }
                }
                else
                {
                    external.Bind(new IPEndPoint(ExternalAddress, 0));
                }

                await external.ConnectAsync(destination).ConfigureAwait(false);

                var key = "T|" + internalEndPoint + "|" + destination;
                var mapping = new NatMapping(key, ProtocolType.Tcp, internalEndPoint, (IPEndPoint)external.LocalEndPoint, false, Now());
                mapping.Socket = external;
                mapping.Permit(destination);

                lock (_sync)
                {
                    if (!_disposed) _mappings.Add(mapping);
                }

                await RelayAsync(client, external).ConfigureAwait(false);
            }
            catch (Exception)
            {
                try { client.Dispose(); } catch { }
                try { external?.Dispose(); } catch { }
            }
        }

        /// <summary>
        /// 静态 TCP 映射的接受循环
        /// </summary>
        /// <param name="mapping">静态映射</param>
        /// <param name="listener">外部监听器</param>
        /// <param name="cancellationToken">关闭令牌</param>
        /// <returns>循环结束的任务</returns>
        private async Task StaticTcpAcceptLoopAsync(NatMapping mapping, TcpListener listener, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Socket inbound;
                try
                {
                    inbound = await listener.AcceptSocketAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { continue; }

                _ = Task.Run(async () =>
                {
                    Socket internalSocket = null;
                    try
                    {
                        mapping.Touch(Now());

                        internalSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        await internalSocket.ConnectAsync(mapping.InternalEndPoint).ConfigureAwait(false);

                        await RelayAsync(inbound, internalSocket).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        try { inbound.Dispose(); } catch { }
                        try { internalSocket?.Dispose(); } catch { }
                    }
                });
            }
        }

        /// <summary>
        /// 双向转发两个已连接的字节流，任一侧结束即关闭两侧
        /// </summary>
        /// <param name="left">一侧套接字</param>
        /// <param name="right">另一侧套接字</param>
        /// <returns>两侧都结束的任务</returns>
        private static async Task RelayAsync(Socket left, Socket right)
        {
            try
            {
                var forward = CopyAsync(left, right);
                var backward = CopyAsync(right, left);
                await Task.WhenAny(forward, backward).ConfigureAwait(false);
            }
            finally
            {
                try { left.Shutdown(SocketShutdown.Both); } catch { }
                try { right.Shutdown(SocketShutdown.Both); } catch { }
                try { left.Dispose(); } catch { }
                try { right.Dispose(); } catch { }
            }
        }

        /// <summary>
        /// 单向复制字节流，连接异常或对端关闭时结束
        /// </summary>
        /// <param name="from">来源套接字</param>
        /// <param name="to">目标套接字</param>
        /// <returns>复制结束的任务</returns>
        private static async Task CopyAsync(Socket from, Socket to)
        {
            var buffer = new byte[8192];
            try
            {
                while (true)
                {
                    var read = await from.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None).ConfigureAwait(false);
                    if (read <= 0) break;

                    var offset = 0;
                    while (offset < read)
                    {
                        offset += await to.SendAsync(new ArraySegment<byte>(buffer, offset, read - offset), SocketFlags.None).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception) { }
        }
    }
}