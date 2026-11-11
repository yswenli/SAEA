/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net8.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.P2PTest.Server
*文件名： ServerHost
*版本号： net8.0
*唯一标识：46ee0b66-af64-4b1d-81af-6796e5137850
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 10:00:00
*描述：按 JSON 配置启动 STUN 与 TURN 监听的自测服务端宿主
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 10:30:00
*修改人： yswenli
*版本号： net8.0
*描述：CreateTurnServer 增加 DefaultLifetimeSeconds，让默认生命周期随配置上限收敛
*
*****************************************************************************/
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Common.Newtonsoft.Json;
using SAEA.P2P.StunServer;
using SAEA.P2P.TurnServer;

namespace SAEA.P2PTest.Server
{
    /// <summary>
    /// 服务端 JSON 配置：键名与设计里的配置项一一对应，口令与共享密钥只在这里读取，不进入日志与异常消息
    /// </summary>
    internal sealed class ServerConfig
    {
        /// <summary>
        /// 默认监听地址
        /// </summary>
        internal const string DefaultListenAddress = "0.0.0.0";

        /// <summary>
        /// 监听地址；默认监听全部 IPv4 地址
        /// </summary>
        public string ListenAddress { get; set; } = DefaultListenAddress;

        /// <summary>
        /// STUN 独立监听端口；为 0 时不单独监听，与 TURN 同端口时由 TURN 监听应答 Binding
        /// </summary>
        public int StunPort { get; set; } = 3478;

        /// <summary>
        /// TURN 的 UDP 与 TCP 监听端口；端口相同不冲突，因为两者分属不同传输协议
        /// </summary>
        public int TurnPort { get; set; } = 3478;

        /// <summary>
        /// TURN over TLS 监听端口；为 0 时不启用 TLS
        /// </summary>
        public int TurnTlsPort { get; set; }

        /// <summary>
        /// 是否改用 IPv6 监听；监听地址仍为默认的 0.0.0.0 时改为监听 IPv6Any
        /// </summary>
        public bool EnableIpv6 { get; set; }

        /// <summary>
        /// 是否要求双栈监听；当前监听层没有 DualMode 开关，只按地址族监听并给出提示
        /// </summary>
        public bool EnableDualMode { get; set; }

        /// <summary>
        /// 认证域，必须与签发临时凭据的一侧一致
        /// </summary>
        public string Realm { get; set; } = string.Empty;

        /// <summary>
        /// 共享密钥，必须与签发临时凭据的一侧一致
        /// </summary>
        public string SharedSecret { get; set; } = string.Empty;

        /// <summary>
        /// 凭据有效期，单位秒
        /// </summary>
        public int CredentialTtlSeconds { get; set; } = 600;

        /// <summary>
        /// nonce 有效期，单位秒
        /// </summary>
        public int NonceLifetimeSeconds { get; set; } = 600;

        /// <summary>
        /// 中继端口区间
        /// </summary>
        public RelayPortRangeConfig RelayPortRange { get; set; } = new RelayPortRangeConfig();

        /// <summary>
        /// 并发分配总数上限
        /// </summary>
        public int MaxAllocations { get; set; } = 1024;

        /// <summary>
        /// 单用户并发分配数上限
        /// </summary>
        public int MaxAllocationsPerUser { get; set; } = 8;

        /// <summary>
        /// 单个分配的累计中继字节上限，0 表示不限制
        /// </summary>
        public long MaxBytes { get; set; }

        /// <summary>
        /// 分配生命周期上限，单位秒
        /// </summary>
        public uint MaxLifetimeSeconds { get; set; } = 3600;

        /// <summary>
        /// PFX 证书文件路径；启用 TLS 时必填
        /// </summary>
        public string? TlsCertificatePath { get; set; }

        /// <summary>
        /// 证书口令；无口令时为 null
        /// </summary>
        public string? TlsCertificatePassword { get; set; }

        /// <summary>
        /// RFC 5780 的第二地址；与主地址在同一端口上监听，为 null 时只监听主地址
        /// </summary>
        public string? ChangeRequestAddressPair { get; set; }

        /// <summary>
        /// 从 JSON 文件加载配置
        /// </summary>
        /// <param name="path">配置文件路径</param>
        /// <returns>配置对象</returns>
        /// <exception cref="ArgumentException">路径为空</exception>
        /// <exception cref="FileNotFoundException">文件不存在</exception>
        /// <exception cref="InvalidOperationException">解析失败或内容为空</exception>
        public static ServerConfig Load(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("必须提供配置文件路径", nameof(path));
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("找不到配置文件", path);
            }

            var text = File.ReadAllText(path);
            ServerConfig? config;
            try
            {
                config = JsonConvert.DeserializeObject<ServerConfig>(text);
            }
            catch (Exception ex)
            {
                // 只带路径与解析摘要：共享密钥与证书口令不能随文件内容一起进异常消息或日志
                throw new InvalidOperationException(
                    "解析配置文件 " + path + " 失败：" + ex.GetType().Name + "：" + ex.Message, ex);
            }

            if (config == null)
            {
                throw new InvalidOperationException("配置文件 " + path + " 没有可用的配置对象");
            }

            if (config.RelayPortRange == null)
            {
                // JSON 里显式写 null 时补默认区间，避免后续解引用空对象
                config.RelayPortRange = new RelayPortRangeConfig();
            }

            return config;
        }

        /// <summary>
        /// 校验取值范围与必填项；异常消息只提字段名，不回显字段值
        /// </summary>
        /// <exception cref="ArgumentException">必填项缺失、地址为空或 TLS 证书不可用</exception>
        /// <exception cref="ArgumentOutOfRangeException">端口、端口区间、TTL 或上限越界</exception>
        internal void Validate()
        {
            if (string.IsNullOrEmpty(ListenAddress))
            {
                throw new ArgumentException("必须配置 ListenAddress", nameof(ListenAddress));
            }

            ValidatePort(StunPort, nameof(StunPort));
            ValidatePort(TurnPort, nameof(TurnPort));
            ValidatePort(TurnTlsPort, nameof(TurnTlsPort));

            var relay = RelayPortRange;
            if (relay == null)
            {
                throw new ArgumentException("必须配置 RelayPortRange", nameof(RelayPortRange));
            }

            if (relay.Min < 1 || relay.Max < relay.Min || relay.Max > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(RelayPortRange),
                    "中继端口区间不合法：Min 不能小于 1，Max 不能小于 Min 且不能大于 65535");
            }

            if (string.IsNullOrEmpty(Realm))
            {
                throw new ArgumentException("必须配置 Realm", nameof(Realm));
            }

            if (string.IsNullOrEmpty(SharedSecret))
            {
                throw new ArgumentException("必须配置 SharedSecret", nameof(SharedSecret));
            }

            if (CredentialTtlSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(CredentialTtlSeconds), "凭据有效期必须大于 0");
            }

            if (NonceLifetimeSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(NonceLifetimeSeconds), "nonce 有效期必须大于 0");
            }

            if (MaxAllocations <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxAllocations), "并发分配总数上限必须大于 0");
            }

            if (MaxAllocationsPerUser <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxAllocationsPerUser), "单用户并发分配上限必须大于 0");
            }

            if (MaxBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxBytes), "中继字节上限不能小于 0");
            }

            if (MaxLifetimeSeconds == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxLifetimeSeconds), "分配生命周期上限必须大于 0");
            }

            if (TurnTlsPort > 0)
            {
                if (string.IsNullOrEmpty(TlsCertificatePath))
                {
                    throw new ArgumentException("启用 TURN over TLS 时必须配置 TlsCertificatePath", nameof(TlsCertificatePath));
                }

                if (!File.Exists(TlsCertificatePath))
                {
                    throw new ArgumentException("TLS 证书文件不存在：" + TlsCertificatePath, nameof(TlsCertificatePath));
                }
            }
        }

        /// <summary>
        /// 校验端口落在 0 到 65535 之间
        /// </summary>
        /// <param name="value">端口值</param>
        /// <param name="name">字段名</param>
        private static void ValidatePort(int value, string name)
        {
            if (value < 0 || value > 65535)
            {
                throw new ArgumentOutOfRangeException(name, "端口必须在 0 到 65535 之间");
            }
        }
    }

    /// <summary>
    /// 中继端口区间配置；字段名即 JSON 键名
    /// </summary>
    internal sealed class RelayPortRangeConfig
    {
        /// <summary>
        /// 下界
        /// </summary>
        public int Min = 49152;

        /// <summary>
        /// 上界
        /// </summary>
        public int Max = 65535;
    }

    /// <summary>
    /// 服务端宿主：按配置启动 STUN 与 TURN 监听并定时清理过期分配。
    /// 构造阶段只校验配置与解析地址，端口在 <see cref="StartAsync"/> 里绑定。
    /// </summary>
    internal sealed class ServerHost : IDisposable
    {
        /// <summary>
        /// 过期分配清理周期，单位毫秒
        /// </summary>
        private const int PurgeIntervalMilliseconds = 30000;

        /// <summary>
        /// 服务端配置
        /// </summary>
        private readonly ServerConfig _config;

        /// <summary>
        /// 解析后的监听地址
        /// </summary>
        private readonly IPAddress _listenAddress;

        /// <summary>
        /// RFC 5780 的第二地址；未配置时为 null
        /// </summary>
        private readonly IPAddress? _alternateAddress;

        /// <summary>
        /// 中继地址
        /// </summary>
        private readonly IPAddress _relayAddress;

        /// <summary>
        /// 保护启动状态与释放状态
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 独立 STUN 监听；StunPort 为 0 或与 TURN 同端口时为 null
        /// </summary>
        private StunServer? _stun;

        /// <summary>
        /// TURN 的 UDP 监听，同时应答 Binding
        /// </summary>
        private StunServer? _turnUdp;

        /// <summary>
        /// TURN 的 TCP 监听
        /// </summary>
        private TurnTcpListener? _turnTcp;

        /// <summary>
        /// TURN 的 TLS 监听
        /// </summary>
        private TurnTlsListener? _turnTls;

        /// <summary>
        /// 过期分配清理定时器
        /// </summary>
        private Timer? _purgeTimer;

        /// <summary>
        /// 是否已启动完成
        /// </summary>
        private bool _started;

        /// <summary>
        /// 是否正在启动
        /// </summary>
        private bool _starting;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 按配置创建宿主；只做配置校验与地址解析，不绑定端口
        /// </summary>
        /// <param name="config">服务端配置</param>
        /// <exception cref="ArgumentNullException">config 为 null</exception>
        /// <exception cref="ArgumentException">配置项非法或地址无法解析</exception>
        public ServerHost(ServerConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            config.Validate();
            _config = config;
            _listenAddress = ResolveListenAddress(config);
            _alternateAddress = ResolveAlternateAddress(config, _listenAddress);
            _relayAddress = ResolveRelayAddress(_listenAddress);
            Server = CreateTurnServer(config, _relayAddress);
        }

        /// <summary>
        /// TURN 服务端；构造后即可签发临时凭据
        /// </summary>
        public TurnServer Server { get; }

        /// <summary>
        /// STUN 独立监听的实际端点；未启用或与 TURN 共用端口时为 null
        /// </summary>
        public IPEndPoint? StunEndPoint { get; private set; }

        /// <summary>
        /// TURN UDP 监听的实际端点；启动前为 null
        /// </summary>
        public IPEndPoint? TurnUdpEndPoint { get; private set; }

        /// <summary>
        /// TURN TCP 监听的实际端点；启动前为 null
        /// </summary>
        public IPEndPoint? TurnTcpEndPoint { get; private set; }

        /// <summary>
        /// TURN TLS 监听的实际端点；未启用时为 null
        /// </summary>
        public IPEndPoint? TurnTlsEndPoint { get; private set; }

        /// <summary>
        /// 按 STUN、TURN UDP、TURN TCP、TURN TLS 的顺序启动监听；任一失败会释放已启动的监听后再抛出
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>启动完成的任务</returns>
        /// <exception cref="ObjectDisposedException">已释放</exception>
        /// <exception cref="InvalidOperationException">重复启动</exception>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                if (_started || _starting)
                {
                    throw new InvalidOperationException("服务端只能启动一次");
                }
                _starting = true;
            }

            try
            {
                Log("监听地址：" + _listenAddress + "，地址族：" + _listenAddress.AddressFamily);
                if (_config.EnableDualMode)
                {
                    // 监听层没有 DualMode 开关，这里不能声称已开双栈，只能按地址族监听
                    Log("已配置 EnableDualMode，但 SaeaUdpTransport 与 TurnTcpListener 尚未暴露 DualMode 开关，"
                        + "双栈监听需后续阶段支持；本次仅按地址族监听");
                }

                if (_alternateAddress != null)
                {
                    Log("RFC 5780 第二地址：" + _alternateAddress + "，与主地址同端口监听");
                    if (_config.TurnPort == 0)
                    {
                        // 备用端点与主端点必须同端口，端口为 0 时两个监听会各自随机分配，换地址探测必然失败
                        throw new ArgumentException("配置 ChangeRequestAddressPair 时 TurnPort 不能为 0",
                            nameof(ServerConfig.TurnPort));
                    }
                }

                await StartStunAsync(cancellationToken).ConfigureAwait(false);
                await StartTurnUdpAsync(cancellationToken).ConfigureAwait(false);
                StartTurnTcp();
                StartTurnTls();
                StartPurgeTimer();
                LogSummary();

                lock (_sync)
                {
                    _started = true;
                }
            }
            catch
            {
                ReleaseListeners();
                throw;
            }
            finally
            {
                lock (_sync)
                {
                    _starting = false;
                }
            }
        }

        /// <summary>
        /// 释放监听与资源；可重复调用，先停清理定时器，再按 TLS、TCP、TURN UDP、STUN 逆序释放
        /// </summary>
        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
            }

            var timer = _purgeTimer;
            _purgeTimer = null;
            if (timer != null)
            {
                timer.Dispose();
            }

            ReleaseListeners();
            Server.Dispose();
        }

        /// <summary>
        /// 启动独立 STUN 监听；端口为 0 或与 TURN 同端口时跳过
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>启动完成的任务</returns>
        private async Task StartStunAsync(CancellationToken cancellationToken)
        {
            if (_config.StunPort <= 0)
            {
                Log("STUN 独立监听未启用：StunPort 为 0");
                return;
            }

            if (_config.StunPort == _config.TurnPort)
            {
                // 同一端点重复绑定必然失败，Binding 交给 TURN 的 UDP 监听应答即可
                Log("STUN 与 TURN 共用端口 " + _config.TurnPort + "，Binding 由 TURN 监听应答");
                return;
            }

            var options = new StunServerOptions(
                new IPEndPoint(_listenAddress, _config.StunPort),
                BuildAlternateEndPoint(_config.StunPort));
            var server = new StunServer(options);
            try
            {
                await server.StartAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                server.Dispose();
                throw;
            }

            _stun = server;
            StunEndPoint = FirstLocalEndPoint(server);
        }

        /// <summary>
        /// 启动 TURN 的 UDP 监听，并把非 Binding 请求与原始数据报分派给 TURN 服务端
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>启动完成的任务</returns>
        private async Task StartTurnUdpAsync(CancellationToken cancellationToken)
        {
            var options = new StunServerOptions(
                new IPEndPoint(_listenAddress, _config.TurnPort),
                BuildAlternateEndPoint(_config.TurnPort))
            {
                RequestHandler = Server.HandleRequest,
                DatagramHandler = Server.HandleDatagram
            };

            var server = new StunServer(options);
            try
            {
                await server.StartAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                server.Dispose();
                throw;
            }

            _turnUdp = server;
            TurnUdpEndPoint = FirstLocalEndPoint(server);
        }

        /// <summary>
        /// 启动 TURN over TCP 监听；与 UDP 使用同一端口号不冲突
        /// </summary>
        private void StartTurnTcp()
        {
            var listener = new TurnTcpListener(new IPEndPoint(_listenAddress, _config.TurnPort), Server);
            try
            {
                listener.Start();
            }
            catch
            {
                listener.Dispose();
                throw;
            }

            _turnTcp = listener;
            TurnTcpEndPoint = listener.LocalEndPoint;
        }

        /// <summary>
        /// 启动 TURN over TLS 监听；TurnTlsPort 为 0 时跳过
        /// </summary>
        private void StartTurnTls()
        {
            if (_config.TurnTlsPort <= 0)
            {
                return;
            }

            var certificatePath = _config.TlsCertificatePath;
            if (string.IsNullOrEmpty(certificatePath))
            {
                // 配置校验已拦截，这里兜底避免把空路径传进监听器
                throw new ArgumentException("启用 TURN over TLS 时必须配置 TlsCertificatePath",
                    nameof(ServerConfig.TlsCertificatePath));
            }

            var listener = new TurnTlsListener(
                new IPEndPoint(_listenAddress, _config.TurnTlsPort),
                Server,
                certificatePath,
                _config.TlsCertificatePassword ?? string.Empty);
            try
            {
                listener.Start();
            }
            catch
            {
                listener.Dispose();
                throw;
            }

            _turnTls = listener;
            TurnTlsEndPoint = listener.LocalEndPoint;
        }

        /// <summary>
        /// 启动过期分配清理定时器
        /// </summary>
        private void StartPurgeTimer()
        {
            _purgeTimer = new Timer(OnPurgeTimer, null, PurgeIntervalMilliseconds, PurgeIntervalMilliseconds);
        }

        /// <summary>
        /// 定时清理过期分配；回调里的异常会终结进程，必须就地吞掉并只留日志
        /// </summary>
        /// <param name="state">定时器状态，未使用</param>
        private void OnPurgeTimer(object? state)
        {
            try
            {
                Server.PurgeExpired();
            }
            catch (Exception ex)
            {
                Log("清理过期分配失败：" + ex.GetType().Name + "：" + ex.Message);
            }
        }

        /// <summary>
        /// 按 TLS、TCP、TURN UDP、STUN 的逆序释放已启动的监听，并清空对外端点
        /// </summary>
        private void ReleaseListeners()
        {
            DisposeQuietly(_turnTls, "TURN TLS 监听");
            _turnTls = null;
            TurnTlsEndPoint = null;

            DisposeQuietly(_turnTcp, "TURN TCP 监听");
            _turnTcp = null;
            TurnTcpEndPoint = null;

            DisposeQuietly(_turnUdp, "TURN UDP 监听");
            _turnUdp = null;
            TurnUdpEndPoint = null;

            DisposeQuietly(_stun, "STUN 监听");
            _stun = null;
            StunEndPoint = null;
        }

        /// <summary>
        /// 释放单个监听；失败只记日志，避免启动失败路径被释放异常覆盖
        /// </summary>
        /// <param name="resource">监听实例；可为 null</param>
        /// <param name="name">监听名称，用于日志</param>
        private static void DisposeQuietly(IDisposable? resource, string name)
        {
            if (resource == null)
            {
                return;
            }

            try
            {
                resource.Dispose();
            }
            catch (Exception ex)
            {
                Log(name + " 释放失败：" + ex.GetType().Name + "：" + ex.Message);
            }
        }

        /// <summary>
        /// 解析监听地址；开启 IPv6 且地址仍为默认值时改用 IPv6Any
        /// </summary>
        /// <param name="config">服务端配置</param>
        /// <returns>监听地址</returns>
        private static IPAddress ResolveListenAddress(ServerConfig config)
        {
            if (config.EnableIpv6 && config.ListenAddress == ServerConfig.DefaultListenAddress)
            {
                return IPAddress.IPv6Any;
            }

            return ParseAddress(config.ListenAddress, nameof(ServerConfig.ListenAddress));
        }

        /// <summary>
        /// 解析 RFC 5780 第二地址；未配置或为空白时为 null
        /// </summary>
        /// <param name="config">服务端配置</param>
        /// <param name="listenAddress">主监听地址，用于校验地址族</param>
        /// <returns>第二地址；未配置时为 null</returns>
        /// <exception cref="ArgumentException">地址非法或与主地址不同族</exception>
        private static IPAddress? ResolveAlternateAddress(ServerConfig config, IPAddress listenAddress)
        {
            var text = config.ChangeRequestAddressPair;
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var alternate = ParseAddress(text.Trim(), nameof(ServerConfig.ChangeRequestAddressPair));
            if (alternate.AddressFamily != listenAddress.AddressFamily)
            {
                // STUN 服务端只按同一地址族组合备用端点，这里提前给出明确错误
                throw new ArgumentException("ChangeRequestAddressPair 必须与监听地址属于同一地址族",
                    nameof(ServerConfig.ChangeRequestAddressPair));
            }

            return alternate;
        }

        /// <summary>
        /// 推导中继地址：监听具体地址时用它，监听 Any 时取本机同族且非回环的第一个地址
        /// </summary>
        /// <param name="listenAddress">监听地址</param>
        /// <returns>中继地址</returns>
        private static IPAddress ResolveRelayAddress(IPAddress listenAddress)
        {
            if (!IsAnyAddress(listenAddress))
            {
                return listenAddress;
            }

            var fallback = listenAddress.AddressFamily == AddressFamily.InterNetworkV6
                ? IPAddress.IPv6Loopback
                : IPAddress.Loopback;

            try
            {
                var addresses = Dns.GetHostAddresses(Dns.GetHostName());
                for (var i = 0; i < addresses.Length; i++)
                {
                    var candidate = addresses[i];
                    if (candidate.AddressFamily != listenAddress.AddressFamily)
                    {
                        continue;
                    }
                    if (IPAddress.IsLoopback(candidate))
                    {
                        continue;
                    }
                    return candidate;
                }
            }
            catch (Exception ex)
            {
                // 解析本机地址失败不阻止启动，退回回环地址继续；消息里不含任何密钥
                Log("获取本机地址失败，中继地址退回回环地址：" + ex.GetType().Name + "：" + ex.Message);
            }

            return fallback;
        }

        /// <summary>
        /// 按配置创建 TURN 服务端，把全部可配项映射过去
        /// </summary>
        /// <param name="config">服务端配置</param>
        /// <param name="relayAddress">中继地址</param>
        /// <returns>TURN 服务端</returns>
        private static TurnServer CreateTurnServer(ServerConfig config, IPAddress relayAddress)
        {
            var options = new TurnServerOptions
            {
                SharedSecret = config.SharedSecret,
                Realm = config.Realm,
                CredentialTtlSeconds = config.CredentialTtlSeconds,
                NonceLifetimeSeconds = config.NonceLifetimeSeconds,
                RelayAddress = relayAddress,
                RelayPortMin = config.RelayPortRange.Min,
                RelayPortMax = config.RelayPortRange.Max,
                MaxAllocations = config.MaxAllocations,
                MaxAllocationsPerUser = config.MaxAllocationsPerUser,
                MaxBytes = config.MaxBytes,
                MaxLifetimeSeconds = config.MaxLifetimeSeconds,
                // 配置只暴露上限，默认值随上限收敛，避免上限小于 600s 时被校验判为「上限不能小于默认值」的非法组合
                DefaultLifetimeSeconds = Math.Min(600u, config.MaxLifetimeSeconds)
            };

            return new TurnServer(options);
        }

        /// <summary>
        /// 判断是否为通配监听地址
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns>任一地址族通配地址返回 true</returns>
        private static bool IsAnyAddress(IPAddress address)
        {
            return address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any);
        }

        /// <summary>
        /// 解析 IP 地址文本
        /// </summary>
        /// <param name="text">地址文本</param>
        /// <param name="paramName">字段名，用于异常</param>
        /// <returns>解析结果</returns>
        /// <exception cref="ArgumentException">不是合法 IP 地址</exception>
        private static IPAddress ParseAddress(string text, string paramName)
        {
            IPAddress? address;
            if (!IPAddress.TryParse(text, out address) || address == null)
            {
                throw new ArgumentException("不是合法的 IP 地址：" + text, paramName);
            }

            return address;
        }

        /// <summary>
        /// 构造备用端点；未配置第二地址时为 null
        /// </summary>
        /// <param name="port">与主端点相同的端口</param>
        /// <returns>备用端点；未配置时为 null</returns>
        private IPEndPoint? BuildAlternateEndPoint(int port)
        {
            var alternate = _alternateAddress;
            return alternate == null ? null : new IPEndPoint(alternate, port);
        }

        /// <summary>
        /// 取 STUN 服务端第一个已绑定的本地端点
        /// </summary>
        /// <param name="server">STUN 服务端</param>
        /// <returns>本地端点；尚未绑定时为 null</returns>
        private static IPEndPoint? FirstLocalEndPoint(StunServer server)
        {
            var endpoints = server.Endpoints;
            for (var i = 0; i < endpoints.Count; i++)
            {
                var local = endpoints[i].LocalEndPoint;
                if (local != null)
                {
                    return local;
                }
            }

            return null;
        }

        /// <summary>
        /// 打印启动结果；不输出共享密钥与证书口令
        /// </summary>
        private void LogSummary()
        {
            Log("STUN 监听：" + DescribeStunEndPoint());
            Log("TURN UDP 监听：" + DescribeEndPoint(TurnUdpEndPoint));
            Log("TURN TCP 监听：" + DescribeEndPoint(TurnTcpEndPoint));
            Log("TURN TLS 监听：" + DescribeEndPoint(TurnTlsEndPoint));
            Log("中继地址：" + _relayAddress + "，中继端口区间："
                + _config.RelayPortRange.Min + "-" + _config.RelayPortRange.Max);
            Log("认证域：" + _config.Realm + "，TLS：" + (_config.TurnTlsPort > 0 ? "已启用" : "未启用"));
            Log("并发分配上限：" + _config.MaxAllocations + "，单用户上限：" + _config.MaxAllocationsPerUser
                + "，凭据有效期：" + _config.CredentialTtlSeconds + "s，nonce 有效期："
                + _config.NonceLifetimeSeconds + "s");
        }

        /// <summary>
        /// 描述 STUN 监听状态；与 TURN 共用端口时说明 Binding 由谁应答
        /// </summary>
        /// <returns>状态描述</returns>
        private string DescribeStunEndPoint()
        {
            var endPoint = StunEndPoint;
            if (endPoint != null)
            {
                return endPoint.ToString() ?? string.Empty;
            }

            if (_config.StunPort > 0 && _config.StunPort == _config.TurnPort)
            {
                return "与 TURN UDP 共用端口 " + _config.TurnPort + "，Binding 由 TURN 监听应答";
            }

            return "未启用";
        }

        /// <summary>
        /// 描述端点；未启用时为固定文案
        /// </summary>
        /// <param name="endPoint">端点</param>
        /// <returns>状态描述</returns>
        private static string DescribeEndPoint(IPEndPoint? endPoint)
        {
            return endPoint == null ? "未启用" : endPoint.ToString() ?? string.Empty;
        }

        /// <summary>
        /// 已释放时拒绝启动
        /// </summary>
        /// <exception cref="ObjectDisposedException">已释放</exception>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        /// <summary>
        /// 输出一行带时间戳的日志；这里不用 ConsoleHelper，避免其异步队列在退出前丢日志
        /// </summary>
        /// <param name="message">日志内容</param>
        private static void Log(string message)
        {
            Console.WriteLine("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + message);
        }
    }
}