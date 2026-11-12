/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.P2P.Ice
*文件名： IceSession
*版本号： v26.11.5.1
*唯一标识：85eeb9df-6fec-4854-8f2d-9ef510adf962
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 14:44:17
*描述：ICE 会话编排：两阶段候选收集、选路阶梯与直连数据面
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 17:49:52
*修改人： yswenli
*版本号： v26.11.5.1
*描述：TURN 级内接入 RFC 6062 TCP 中继回退
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Common;
using SAEA.P2P.Stun;
using SAEA.P2P.Transport;
using SAEA.P2P.Turn;

namespace SAEA.P2P.Ice
{
    /// <summary>
    /// ICE 会话：先收集 UDP 候选，再用连通性检查逐对配对，最后由选路阶梯决定 UDP 直连、
    /// TCP 直连或 TURN 中继；选中候选对后建立数据面通道转发业务帧。
    /// 候选收集、检查、提名都按阶段预算推进，预算到期只结束当前阶段而不中断会话。
    /// </summary>
    public sealed class IceSession : IDisposable
    {
        /// <summary>
        /// 日志节点名
        /// </summary>
        private const string LogNode = "IceSession";

        /// <summary>
        /// 阶段内等待新候选对或等待对端提名的轮询间隔
        /// </summary>
        private static readonly TimeSpan CandidatePollInterval = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// 会话与阶段预算配置
        /// </summary>
        private readonly IceSessionOptions _options;

        /// <summary>
        /// 本端与对端协商参数
        /// </summary>
        private readonly IceSessionParameters _parameters;

        /// <summary>
        /// 保护候选列表、检查表与通道字段
        /// </summary>
        private readonly object _syncRoot = new object();

        /// <summary>
        /// 已发现的本地候选，含 UDP 与 TCP，按发现顺序
        /// </summary>
        private readonly List<Candidate> _localCandidates = new List<Candidate>();

        /// <summary>
        /// 会话生命周期令牌，释放时取消
        /// </summary>
        private readonly CancellationTokenSource _lifeTimeCts;

        /// <summary>
        /// 受控方等待对端提名的信号源
        /// </summary>
        private readonly TaskCompletionSource<CandidatePair> _remoteNomination;

        /// <summary>
        /// 阶段一 UDP 候选收集器
        /// </summary>
        private CandidateGatherer _udpGatherer;

        /// <summary>
        /// 阶段二 TCP 候选收集器
        /// </summary>
        private TcpCandidateGatherer _tcpGatherer;

        /// <summary>
        /// 阶段一绑定的 UDP 传输，随收集器释放
        /// </summary>
        private IUdpTransport[] _udpTransports = new IUdpTransport[0];

        /// <summary>
        /// 阶段二绑定的 TCP 监听端点
        /// </summary>
        private ITcpTransport[] _tcpListeners = new ITcpTransport[0];

        /// <summary>
        /// UDP 候选检查表
        /// </summary>
        private CheckList _checkList;

        /// <summary>
        /// UDP 入向检查应答器
        /// </summary>
        private IceInboundResponder _responder;

        /// <summary>
        /// 出向连通性检查
        /// </summary>
        private ConnectivityCheck _connectivityCheck;

        /// <summary>
        /// 控制方提名器
        /// </summary>
        private Nominator _nominator;

        /// <summary>
        /// 选中的 UDP 数据面通道
        /// </summary>
        private UdpDataChannel _udpDataChannel;

        /// <summary>
        /// 选中的 TCP 数据面通道
        /// </summary>
        private TcpDirectChannel _tcpDirectChannel;

        /// <summary>
        /// TURN 阶段的客户端；选中中继通道后继续保留，负责分配的刷新与重连
        /// </summary>
        private TurnClient _turnClient;

        /// <summary>
        /// TURN 阶段的中继候选提供器，持有服务端分配与中继数据通道
        /// </summary>
        private RelayCandidateProvider _turnProvider;

        /// <summary>
        /// TURN TCP 中继段的客户端；选中 TCP 中继通道后继续保留，负责分配与中继连接的刷新
        /// </summary>
        private TurnClient _tcpRelayClient;

        /// <summary>
        /// TURN TCP 中继段的候选提供器，持有服务端分配与入向连接队列
        /// </summary>
        private TcpRelayCandidateProvider _tcpRelayProvider;

        /// <summary>
        /// 选中的候选对
        /// </summary>
        private SelectedPair _selectedPair;

        /// <summary>
        /// 选路结果
        /// </summary>
        private IceSessionResult _result;

        /// <summary>
        /// 是否已启动，1 表示已启动
        /// </summary>
        private int _started;

        /// <summary>
        /// 是否已选中可用通道
        /// </summary>
        private volatile bool _isConnected;

        /// <summary>
        /// 是否已释放，1 表示已释放
        /// </summary>
        private int _disposed;

        /// <summary>
        /// 用会话配置与协商参数创建 ICE 会话
        /// </summary>
        /// <param name="options">会话与阶段预算配置，非空</param>
        /// <param name="parameters">本端与对端协商参数，非空</param>
        /// <exception cref="ArgumentNullException">options 或 parameters 为 null</exception>
        /// <exception cref="ArgumentException">配置或参数校验失败</exception>
        public IceSession(IceSessionOptions options, IceSessionParameters parameters)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            options.Validate();
            parameters.Validate();

            _options = options;
            _parameters = parameters;
            _lifeTimeCts = new CancellationTokenSource();
            _remoteNomination = new TaskCompletionSource<CandidatePair>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        /// <summary>
        /// 发现一个本地候选时触发，用于 trickle 逐条把候选交给对端
        /// </summary>
        public event Action<Candidate> LocalCandidateDiscovered;

        /// <summary>
        /// 选出可用通道时触发，参数为最终选路结果
        /// </summary>
        public event Action<IceSessionResult> RouteSelected;

        /// <summary>
        /// 收到一条完整业务帧时触发，参数为已复制的载荷；回调返回后仍可用
        /// </summary>
        public event Action<ReadOnlyMemory<byte>> FrameReceived;

        /// <summary>
        /// 构造时传入的协商参数
        /// </summary>
        public IceSessionParameters Parameters
        {
            get { return _parameters; }
        }

        /// <summary>
        /// 已发现的本地候选快照，含 UDP 与 TCP
        /// </summary>
        public IReadOnlyList<Candidate> LocalCandidates
        {
            get
            {
                lock (_syncRoot)
                {
                    return _localCandidates.ToArray();
                }
            }
        }

        /// <summary>
        /// 是否已选中可用通道
        /// </summary>
        public bool IsConnected
        {
            get { return _isConnected; }
        }

        /// <summary>
        /// 选路结果，未启动或未选中时为 null
        /// </summary>
        public IceSessionResult Result
        {
            get { return _result; }
        }

        /// <summary>
        /// 启动会话：收集 UDP 候选、初始化检查表，再交给选路阶梯逐阶段尝试
        /// </summary>
        /// <param name="cancellationToken">调用方取消令牌</param>
        /// <returns>选路结果；成功时已建立数据面通道</returns>
        /// <exception cref="ObjectDisposedException">会话已释放</exception>
        /// <exception cref="InvalidOperationException">已启动过，会话只能启动一次</exception>
        /// <exception cref="P2PException">选路成功但没有选中候选对</exception>
        public async Task<IceSessionResult> StartAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
            {
                throw new InvalidOperationException("ICE 会话只能启动一次。");
            }

            // 生命周期令牌让 Dispose 能中断正在进行的收集与检查，而不用等待阶段预算耗尽
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifeTimeCts.Token))
            {
                await GatherUdpCandidatesAsync(linked.Token).ConfigureAwait(false);
                InitializeUdpCheckList();

                var stage = await new RouteLadder(_options).RunAsync(AttemptAsync, StageStartingAsync, linked.Token).ConfigureAwait(false);

                var pair = _selectedPair;
                if (pair == null)
                {
                    throw new P2PException(ErrorCode.NoCandidatePair, "选路成功但没有选中候选对");
                }

                _result = new IceSessionResult(stage, pair);
                _isConnected = true;
            }

            RaiseRouteSelected(_result);
            return _result;
        }

        /// <summary>
        /// 追加一个对端候选：先登记到协商参数，UDP 候选同时进入检查表
        /// </summary>
        /// <param name="candidate">对端候选，非空</param>
        public void AddRemoteCandidate(Candidate candidate)
        {
            _parameters.AddRemoteCandidate(candidate);

            lock (_syncRoot)
            {
                if (_checkList != null && candidate.Transport == IceTransportType.Udp)
                {
                    _checkList.AddRemoteCandidate(candidate);
                }
            }
        }

        /// <summary>
        /// 发送一条业务帧，走已选出的 UDP 或 TCP 数据面通道
        /// </summary>
        /// <param name="payload">业务载荷</param>
        /// <exception cref="ObjectDisposedException">会话已释放</exception>
        /// <exception cref="P2PException">尚未选出可用通道，错误码为 NotConnected</exception>
        public void Send(ReadOnlySpan<byte> payload)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            var udp = _udpDataChannel;
            if (udp != null)
            {
                udp.Send(payload);
                return;
            }

            var tcp = _tcpDirectChannel;
            if (tcp != null)
            {
                tcp.Send(payload);
                return;
            }

            throw new P2PException(ErrorCode.NotConnected, "尚未选出可用通道，不能发送业务数据");
        }

        /// <summary>
        /// 取消会话并释放全部资源，可重复调用
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            try
            {
                _lifeTimeCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // 会话已释放时取消令牌可能已不可用，后续释放照常进行
            }

            var udpChannel = _udpDataChannel;
            _udpDataChannel = null;
            DisposeQuietly(udpChannel, "UDP 数据面通道");

            var tcpChannel = _tcpDirectChannel;
            _tcpDirectChannel = null;
            DisposeQuietly(tcpChannel, "TCP 数据面通道");

            var responder = _responder;
            _responder = null;
            if (responder != null)
            {
                // 先退订再释放，避免释放过程中仍有接收线程回调进会话
                try { responder.CheckRequestReceived -= OnInboundCheckRequest; } catch { }
            }
            DisposeQuietly(responder, "入向检查应答器");

            var nominator = _nominator;
            _nominator = null;
            DisposeQuietly(nominator, "提名器");

            var check = _connectivityCheck;
            _connectivityCheck = null;
            DisposeQuietly(check, "连通性检查");

            var tcpGatherer = _tcpGatherer;
            _tcpGatherer = null;
            DisposeQuietly(tcpGatherer, "TCP 候选收集器");

            var udpGatherer = _udpGatherer;
            _udpGatherer = null;
            DisposeQuietly(udpGatherer, "UDP 候选收集器");

            // 这里只做本地释放，不发送 LIFETIME=0；中继端口由服务端按分配租期回收，与 TurnClient / TurnAllocationClient 的释放语义一致
            var turnProvider = _turnProvider;
            _turnProvider = null;
            DisposeQuietly(turnProvider, "TURN 中继提供器");

            var turnClient = _turnClient;
            _turnClient = null;
            DisposeQuietly(turnClient, "TURN 客户端");

            var tcpRelayProvider = _tcpRelayProvider;
            _tcpRelayProvider = null;
            DisposeQuietly(tcpRelayProvider, "TURN TCP 中继提供器");

            var tcpRelayClient = _tcpRelayClient;
            _tcpRelayClient = null;
            DisposeQuietly(tcpRelayClient, "TURN TCP 中继客户端");

            lock (_syncRoot)
            {
                _checkList = null;
                _selectedPair = null;
            }

            try
            {
                _lifeTimeCts.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // 已释放的令牌源再次释放按幂等处理
            }
        }

        /// <summary>
        /// 阶段一：收集 UDP 候选，预算到期按正常结束处理
        /// </summary>
        /// <param name="token">联合取消令牌</param>
        /// <returns>收集结束的任务</returns>
        private async Task GatherUdpCandidatesAsync(CancellationToken token)
        {
            var gatherer = new CandidateGatherer(_options.Ice);
            _udpGatherer = gatherer;
            gatherer.OnCandidateDiscovered += OnUdpCandidateDiscovered;
            gatherer.EndOfCandidates += OnUdpEndOfCandidates;

            var gatheringBudget = _options.GatheringBudget;

            // 会话预算比收集器自身超时更紧时以会话预算为准，否则沿用收集器超时
            using (var budget = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                if (gatheringBudget > TimeSpan.Zero && gatheringBudget < _options.Ice.GatheringTimeout)
                {
                    budget.CancelAfter(gatheringBudget);
                }

                try
                {
                    await gatherer.GatherAsync(budget.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    if (token.IsCancellationRequested)
                    {
                        throw;
                    }

                    // 预算到期：保留已收集的候选继续选路，与调用方主动取消区分
                }
            }

            _udpTransports = gatherer.Transports.ToArray();
        }

        /// <summary>
        /// 阶段一每发现一个 UDP 候选时记录并对外发布
        /// </summary>
        /// <param name="candidate">新发现的本端候选</param>
        private void OnUdpCandidateDiscovered(Candidate candidate)
        {
            if (candidate == null)
            {
                return;
            }

            if (!TryAddLocalCandidate(candidate, true))
            {
                return;
            }

            RaiseLocalCandidateDiscovered(candidate);
        }

        /// <summary>
        /// 阶段一收集结束时记录日志
        /// </summary>
        private void OnUdpEndOfCandidates()
        {
            P2PLogHelper.Debug(LogNode, "阶段一 UDP 候选收集结束");
        }

        /// <summary>
        /// 用已发现的 UDP 候选与对端候选建立检查表，并启动入向应答
        /// </summary>
        private void InitializeUdpCheckList()
        {
            var checkList = new CheckList(_parameters.Role == IceRole.Controlling);

            Candidate[] localSnapshot;
            lock (_syncRoot)
            {
                localSnapshot = _localCandidates.ToArray();
            }

            for (int i = 0; i < localSnapshot.Length; i++)
            {
                if (localSnapshot[i].Transport == IceTransportType.Udp)
                {
                    checkList.AddLocalCandidate(localSnapshot[i]);
                }
            }

            var remotes = _parameters.RemoteCandidates;
            if (remotes != null)
            {
                for (int i = 0; i < remotes.Count; i++)
                {
                    if (remotes[i] != null && remotes[i].Transport == IceTransportType.Udp)
                    {
                        checkList.AddRemoteCandidate(remotes[i]);
                    }
                }
            }

            var check = new ConnectivityCheck(_parameters.LocalUfrag, _parameters.LocalPassword);

            lock (_syncRoot)
            {
                _checkList = checkList;
            }

            _connectivityCheck = check;

            if (_udpTransports.Length > 0)
            {
                var responder = new IceInboundResponder(_udpTransports, _parameters.LocalUfrag, _parameters.LocalPassword);
                _responder = responder;
                responder.CheckRequestReceived += OnInboundCheckRequest;
                responder.Start();
            }
        }

        /// <summary>
        /// 阶段切换前的准备：进入 TCP 直连前关闭 UDP 传输，进入 TURN 前关闭 TCP
        /// </summary>
        /// <param name="stage">即将开始的阶段</param>
        /// <param name="token">联合取消令牌</param>
        /// <returns>准备完成的任务</returns>
        private async Task StageStartingAsync(IceStage stage, CancellationToken token)
        {
            switch (stage)
            {
                case IceStage.TcpDirect:
                    CleanupUdpStage();
                    await GatherTcpCandidatesAsync(token).ConfigureAwait(false);
                    break;

                case IceStage.TurnUdp:
                    CleanupTcpStage();
                    CleanupTurnStage();
                    break;

                case IceStage.TurnTcp:
                    CleanupTurnStage();
                    break;

                case IceStage.TurnTls:
                    CleanupTurnStage();
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// 释放 UDP 阶段资源，可重复调用；已发现的本地与对端候选保留
        /// </summary>
        private void CleanupUdpStage()
        {
            var udpChannel = _udpDataChannel;
            _udpDataChannel = null;
            DisposeQuietly(udpChannel, "UDP 数据面通道");

            var responder = _responder;
            _responder = null;
            if (responder != null)
            {
                try { responder.CheckRequestReceived -= OnInboundCheckRequest; } catch { }
            }
            DisposeQuietly(responder, "入向检查应答器");

            var nominator = _nominator;
            _nominator = null;
            DisposeQuietly(nominator, "提名器");

            var check = _connectivityCheck;
            _connectivityCheck = null;
            DisposeQuietly(check, "连通性检查");

            var gatherer = _udpGatherer;
            _udpGatherer = null;
            DisposeQuietly(gatherer, "UDP 候选收集器");

            lock (_syncRoot)
            {
                _checkList = null;
            }

            _udpTransports = new IUdpTransport[0];
        }

        /// <summary>
        /// 阶段二：收集 TCP 候选，只记录与发布，不进入 UDP 检查表
        /// </summary>
        /// <param name="token">联合取消令牌</param>
        /// <returns>收集结束的任务</returns>
        private async Task GatherTcpCandidatesAsync(CancellationToken token)
        {
            if (_tcpGatherer != null)
            {
                return;
            }

            var gatherer = new TcpCandidateGatherer(_options.Ice);
            _tcpGatherer = gatherer;
            gatherer.OnCandidateDiscovered += OnTcpCandidateDiscovered;
            gatherer.EndOfCandidates += OnTcpEndOfCandidates;

            await gatherer.GatherAsync(token).ConfigureAwait(false);

            _tcpListeners = gatherer.Listeners.ToArray();
            P2PLogHelper.Debug(LogNode, "阶段二 TCP 监听端点数：" + _tcpListeners.Length);
        }

        /// <summary>
        /// 阶段二每发现一个 TCP 候选时记录并对外发布
        /// </summary>
        /// <param name="candidate">新发现的本端候选</param>
        private void OnTcpCandidateDiscovered(Candidate candidate)
        {
            if (candidate == null)
            {
                return;
            }

            if (!TryAddLocalCandidate(candidate, false))
            {
                return;
            }

            RaiseLocalCandidateDiscovered(candidate);
        }

        /// <summary>
        /// 阶段二收集结束时记录日志
        /// </summary>
        private void OnTcpEndOfCandidates()
        {
            P2PLogHelper.Debug(LogNode, "阶段二 TCP 候选收集结束");
        }

        /// <summary>
        /// 释放 TCP 阶段资源，可重复调用
        /// </summary>
        private void CleanupTcpStage()
        {
            var channel = _tcpDirectChannel;
            _tcpDirectChannel = null;
            DisposeQuietly(channel, "TCP 数据面通道");

            var gatherer = _tcpGatherer;
            _tcpGatherer = null;
            DisposeQuietly(gatherer, "TCP 候选收集器");

            _tcpListeners = new ITcpTransport[0];
        }

        /// <summary>
        /// 释放 TURN 阶段资源，可重复调用
        /// </summary>
        /// <remarks>
        /// 只做本地清理，中继分配的释放由失败路径显式调用释放辅助方法完成；
        /// 先置空字段再释放，避免同一通道被释放两次。
        /// </remarks>
        private void CleanupTurnStage()
        {
            var udpChannel = _udpDataChannel;
            _udpDataChannel = null;
            DisposeQuietly(udpChannel, "UDP 数据面通道");

            var responder = _responder;
            _responder = null;
            if (responder != null)
            {
                // 先退订再释放，避免释放过程中仍有接收线程回调进会话
                try { responder.CheckRequestReceived -= OnInboundCheckRequest; } catch { }
            }
            DisposeQuietly(responder, "入向检查应答器");

            var nominator = _nominator;
            _nominator = null;
            DisposeQuietly(nominator, "提名器");

            var check = _connectivityCheck;
            _connectivityCheck = null;
            DisposeQuietly(check, "连通性检查");

            var provider = _turnProvider;
            _turnProvider = null;
            DisposeQuietly(provider, "TURN 中继提供器");

            var client = _turnClient;
            _turnClient = null;
            DisposeQuietly(client, "TURN 客户端");

            // 阶段失败或切换时只做本地清理，分配的 LIFETIME=0 由失败路径显式发过
            var tcpRelayProvider = _tcpRelayProvider;
            _tcpRelayProvider = null;
            DisposeQuietly(tcpRelayProvider, "TURN TCP 中继提供器");

            var tcpRelayClient = _tcpRelayClient;
            _tcpRelayClient = null;
            DisposeQuietly(tcpRelayClient, "TURN TCP 中继客户端");

            lock (_syncRoot)
            {
                _checkList = null;
            }
        }

        /// <summary>
        /// 单个阶段的尝试入口，返回 null 表示该阶段已选中通道，否则返回失败原因
        /// </summary>
        /// <param name="stage">当前阶段</param>
        /// <param name="stageBudget">该阶段可用预算</param>
        /// <param name="token">联合取消令牌</param>
        /// <returns>失败原因；成功时为 null</returns>
        private async Task<string> AttemptAsync(IceStage stage, TimeSpan stageBudget, CancellationToken token)
        {
            switch (stage)
            {
                case IceStage.UdpDirect:
                    return await RunUdpDirectAsync(stageBudget, token).ConfigureAwait(false);

                case IceStage.TcpDirect:
                    return await RunTcpDirectAsync(stageBudget, token).ConfigureAwait(false);

                case IceStage.TurnUdp:
                    return await RunTurnUdpAsync(stageBudget, token).ConfigureAwait(false);

                case IceStage.TurnTcp:
                    return await RunTurnTcpAsync(stageBudget, token).ConfigureAwait(false);

                case IceStage.TurnTls:
                    return await RunTurnTlsAsync(stageBudget, token).ConfigureAwait(false);

                default:
                    return "未知的选路阶段：" + stage;
            }
        }

        /// <summary>
        /// UDP 直连阶段：按优先级逐对检查，控制方提名，受控方等待对端提名
        /// </summary>
        /// <remarks>
        /// 候选对不只在阶段开始时取一次快照：trickle 方式下对端候选会在阶段内陆续到达并登记进检查表，
        /// 因此每轮循环都重新取一次候选快照，新到达的候选对能立即补做连通性检查。
        /// 受控方不发起检查也不提名，只等对端的 USE-CANDIDATE，避免对端提名到达前就退出阶段。
        /// </remarks>
        /// <param name="stageBudget">本阶段可用预算</param>
        /// <param name="token">联合取消令牌</param>
        /// <returns>失败原因；选中通道时为 null</returns>
        private async Task<string> RunUdpDirectAsync(TimeSpan stageBudget, CancellationToken token)
        {
            CheckList checkList;
            ConnectivityCheck check;
            lock (_syncRoot)
            {
                checkList = _checkList;
                check = _connectivityCheck;
            }

            if (checkList == null || check == null)
            {
                return "UDP 候选未初始化";
            }

            var isControlling = _parameters.Role == IceRole.Controlling;
            var clock = Stopwatch.StartNew();

            // 已尝试的候选对按实例记录，避免同一对在每轮重取快照后被反复检查而耗尽预算
            var attempted = new HashSet<CandidatePair>();
            var triedAny = false;

            while (true)
            {
                token.ThrowIfCancellationRequested();

                var remaining = stageBudget - clock.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                // 受控方不发起检查也不提名，只等对端 USE-CANDIDATE
                if (!isControlling)
                {
                    if (_remoteNomination.Task.IsCompleted)
                    {
                        if (TrySelectNominatedPair())
                        {
                            return null;
                        }

                        break;
                    }

                    var nominationWait = remaining < CandidatePollInterval ? remaining : CandidatePollInterval;
                    await Task.WhenAny(_remoteNomination.Task, Task.Delay(nominationWait, token)).ConfigureAwait(false);
                    continue;
                }

                var pair = PickNextUdpPair(checkList, attempted);
                if (pair == null)
                {
                    // 还没有新的可检查候选对，等对端 trickle 后重取快照
                    var pollWait = remaining < CandidatePollInterval ? remaining : CandidatePollInterval;
                    await Task.Delay(pollWait, token).ConfigureAwait(false);
                    continue;
                }

                attempted.Add(pair);
                triedAny = true;

                var transport = FindUdpTransport(pair.Local);
                if (transport == null)
                {
                    pair.MarkFailed();
                    continue;
                }

                var pairTimeout = TimeSpan.FromMilliseconds(
                    Math.Max(1, Math.Min(StunTransactionTiming.GetTimeoutMilliseconds(500), remaining.TotalMilliseconds)));

                ConnectivityCheckResult result;
                using (var pairCts = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    pairCts.CancelAfter(pairTimeout);

                    try
                    {
                        result = await check.CheckAsync(
                            transport,
                            pair.Remote.EndPoint,
                            _parameters.RemoteUfrag,
                            _parameters.RemotePassword,
                            _parameters.Role,
                            _parameters.Tiebreaker,
                            check.ComputePeerReflexivePriority(pair.Local.Component),
                            false,
                            null,
                            null,
                            pairCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        if (token.IsCancellationRequested)
                        {
                            throw;
                        }

                        // 单对超时不终止本阶段，换下一对继续
                        pair.MarkFailed();
                        continue;
                    }
                }

                if (result == null || !result.Succeeded)
                {
                    pair.MarkFailed();

                    // 角色冲突说明双方同为控制方或同为受控方，只提示不改变本地角色
                    if (result != null && result.RoleConflict)
                    {
                        P2PLogHelper.Debug(LogNode, "连通性检查出现角色冲突：" + pair.Remote.EndPoint);
                    }

                    continue;
                }

                if (pair.State == PairState.Frozen) pair.MarkWaiting();
                if (pair.State != PairState.InProgress) pair.MarkInProgress();
                pair.MarkSucceeded();
                pair.MarkValid();

                remaining = stageBudget - clock.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                if (_nominator == null)
                {
                    _nominator = new Nominator(
                        _parameters.Role,
                        _parameters.Tiebreaker,
                        _parameters.LocalUfrag,
                        _parameters.LocalPassword,
                        _parameters.RemoteUfrag,
                        _parameters.RemotePassword,
                        checkList);
                }

                bool nominated;
                using (var nominateCts = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    nominateCts.CancelAfter(remaining);

                    try
                    {
                        nominated = await _nominator.NominateAsync(transport, pair, null, null, nominateCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        if (token.IsCancellationRequested)
                        {
                            throw;
                        }

                        nominated = false;
                    }
                }

                if (nominated)
                {
                    SelectUdpPair(pair, transport);
                    return null;
                }
            }

            if (!isControlling)
            {
                return "等待对端提名超时";
            }

            return triedAny ? "UDP 直连未打通" : "没有可配对的 UDP 候选";
        }

        /// <summary>
        /// 从检查表当前快照中挑出优先级最高的、尚未尝试过的 UDP 直连候选对
        /// </summary>
        /// <remarks>
        /// 受控方不调用本方法。每轮只挑一对未尝试过的候选，让阶段内 trickle 新到的候选也能补做检查，
        /// 而不是像一次性快照那样漏掉后到的候选。
        /// </remarks>
        /// <param name="checkList">UDP 检查表，非空</param>
        /// <param name="attempted">已尝试过的候选对集合，非空</param>
        /// <returns>优先级最高的未尝试候选对；没有可检查的候选对时返回 null</returns>
        private CandidatePair PickNextUdpPair(CheckList checkList, HashSet<CandidatePair> attempted)
        {
            var pairs = new List<CandidatePair>(checkList.Pairs);
            CandidatePair best = null;

            for (int i = 0; i < pairs.Count; i++)
            {
                var pair = pairs[i];
                if (pair == null || pair.Local == null || pair.Remote == null)
                {
                    continue;
                }

                if (pair.Local.Transport != IceTransportType.Udp || pair.Remote.Transport != IceTransportType.Udp)
                {
                    continue;
                }

                // 中继候选属于 TURN 阶段，混入 UDP 直连只会浪费预算
                if (pair.Local.Type == CandidateType.Relay || pair.Remote.Type == CandidateType.Relay)
                {
                    continue;
                }

                if (FindUdpTransport(pair.Local) == null)
                {
                    continue;
                }

                if (attempted.Contains(pair))
                {
                    continue;
                }

                if (best == null || pair.Priority > best.Priority)
                {
                    best = pair;
                }
            }

            return best;
        }

        /// <summary>
        /// TCP 直连阶段：按本端角色分派到主动连接或被动接受
        /// </summary>
        /// <param name="stageBudget">本阶段可用预算</param>
        /// <param name="token">联合取消令牌</param>
        /// <returns>失败原因；选中通道时为 null</returns>
        private async Task<string> RunTcpDirectAsync(TimeSpan stageBudget, CancellationToken token)
        {
            if (_parameters.Role == IceRole.Controlling)
            {
                return await RunTcpDirectActiveAsync(stageBudget, token).ConfigureAwait(false);
            }

            return await RunTcpDirectPassiveAsync(stageBudget, token).ConfigureAwait(false);
        }

        /// <summary>
        /// TCP 直连主动侧：控制方轮询重取对端被动候选，按优先级逐对主动连接
        /// </summary>
        /// <remarks>
        /// 对端被动候选可能在阶段内才通过 trickle 到达，因此每轮都重新组合候选对；
        /// 每轮只尝试优先级最高的一对，避免预算被已失效的旧候选吃光。
        /// </remarks>
        /// <param name="stageBudget">本阶段可用预算</param>
        /// <param name="token">联合取消令牌</param>
        /// <returns>失败原因；选中通道时为 null</returns>
        private async Task<string> RunTcpDirectActiveAsync(TimeSpan stageBudget, CancellationToken token)
        {
            if (_tcpGatherer == null)
            {
                return "没有可配对的 TCP 候选";
            }

            var locals = BuildActiveTcpCandidates();

            // 没有本地主动候选时不可能配对成功，不必空耗阶段预算等对端 trickle
            if (locals.Count == 0)
            {
                return "没有可配对的 TCP 候选";
            }

            // 主动 TCP 候选也要通过 trickle 告知对端，被动侧据此匹配入向连接
            for (int i = 0; i < locals.Count; i++)
            {
                var candidate = locals[i];
                if (TryAddLocalCandidate(candidate, false))
                {
                    RaiseLocalCandidateDiscovered(candidate);
                }
            }

            var clock = Stopwatch.StartNew();

            // 已尝试的候选对按实例记录；每轮重新组合时复用同一实例，身份比较才能命中
            var attempted = new HashSet<CandidatePair>();
            var pairCache = new Dictionary<string, CandidatePair>();
            var triedAny = false;

            while (true)
            {
                token.ThrowIfCancellationRequested();

                var remaining = stageBudget - clock.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                var remoteCandidates = _parameters.RemoteCandidates;
                var pairs = new List<CandidatePair>();
                if (remoteCandidates != null)
                {
                    for (int i = 0; i < locals.Count; i++)
                    {
                        var local = locals[i];
                        for (int j = 0; j < remoteCandidates.Count; j++)
                        {
                            var remote = remoteCandidates[j];
                            if (remote == null || remote.Transport != IceTransportType.Tcp || remote.TcpType != TcpCandidateType.Passive)
                            {
                                continue;
                            }

                            // 地址族或分量不一致的组合无法建立连接，先排除以免白耗预算
                            if (local.Address == null || remote.Address == null)
                            {
                                continue;
                            }

                            if (local.Address.AddressFamily != remote.Address.AddressFamily)
                            {
                                continue;
                            }

                            if (local.Component != remote.Component)
                            {
                                continue;
                            }

                            var pairKey = local.EndPoint + "|" + remote.EndPoint + "|" + local.Component;
                            CandidatePair cachedPair;
                            if (!pairCache.TryGetValue(pairKey, out cachedPair))
                            {
                                cachedPair = new CandidatePair(local, remote, true);
                                pairCache[pairKey] = cachedPair;
                            }

                            if (attempted.Contains(cachedPair))
                            {
                                continue;
                            }

                            pairs.Add(cachedPair);
                        }
                    }
                }

                if (pairs.Count == 0)
                {
                    // 对端被动候选可能尚未 trickle 到达，等一个轮询间隔后重新组合
                    var wait = remaining < CandidatePollInterval ? remaining : CandidatePollInterval;
                    await Task.Delay(wait, token).ConfigureAwait(false);
                    continue;
                }

                pairs.Sort((left, right) => right.Priority.CompareTo(left.Priority));

                var pair = pairs[0];
                attempted.Add(pair);
                triedAny = true;

                var channel = new TcpDirectChannel(_parameters.LocalUfrag, _parameters.LocalPassword);

                // 单次连接超时不能超过本阶段剩余预算，同时给通道留出最小可用的超时值
                channel.CheckTimeoutMilliseconds = (int)Math.Max(1, Math.Min(remaining.TotalMilliseconds, 5000));

                TcpDirectResult result;
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    cts.CancelAfter(remaining);

                    try
                    {
                        result = await channel.ConnectAsync(
                            new IPEndPoint(pair.Local.Address, 0),
                            pair.Remote.EndPoint,
                            _parameters.RemoteUfrag,
                            _parameters.RemotePassword,
                            IceRole.Controlling,
                            _parameters.Tiebreaker,
                            IcePriority.ComputeForTcp(CandidateType.PeerReflexive, _parameters.Component, TcpCandidateType.Active),
                            true,
                            cts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        if (token.IsCancellationRequested)
                        {
                            channel.Dispose();
                            throw;
                        }

                        // 单对超时不终止本阶段，换下一对继续
                        result = null;
                    }
                }

                if (result == null || !result.Succeeded)
                {
                    // 失败细节由通道内部记录日志，这里只需换下一对
                    channel.Dispose();
                    continue;
                }

                if (pair.State == PairState.Frozen) pair.MarkWaiting();
                if (pair.State != PairState.InProgress) pair.MarkInProgress();
                pair.MarkSucceeded();
                pair.MarkValid();
                pair.MarkNominated();
                channel.FrameReceived += OnFrameReceived;

                lock (_syncRoot)
                {
                    _tcpDirectChannel = channel;
                    _selectedPair = new SelectedPair(pair);
                }

                return null;
            }

            return triedAny ? "TCP 直连未打通" : "没有可配对的 TCP 候选";
        }

        /// <summary>
        /// TCP 直连被动侧：受控方在监听端点等待对端主动接入，接入成功后绑定通道
        /// </summary>
        /// <param name="stageBudget">本阶段可用预算</param>
        /// <param name="token">联合取消令牌</param>
        /// <returns>失败原因；选中通道时为 null</returns>
        private async Task<string> RunTcpDirectPassiveAsync(TimeSpan stageBudget, CancellationToken token)
        {
            var listeners = _tcpListeners;
            if (listeners == null || listeners.Length == 0)
            {
                return "没有可用的 TCP 监听";
            }

            var clock = Stopwatch.StartNew();
            ITcpTransport accepted = null;

            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                cts.CancelAfter(stageBudget);

                var tasks = new List<Task<ITcpTransport>>();
                for (int i = 0; i < listeners.Length; i++)
                {
                    var listener = listeners[i];
                    if (listener == null)
                    {
                        continue;
                    }

                    tasks.Add(listener.AcceptAsync(cts.Token));
                }

                if (tasks.Count == 0)
                {
                    return "没有可用的 TCP 监听";
                }

                Task<ITcpTransport> winner = null;
                try
                {
                    winner = await Task.WhenAny(tasks).ConfigureAwait(false);

                    // 落败的接受任务会随取消结束，失败分支必须观察异常，否则在部分运行时上会变成未处理异常
                    for (int i = 0; i < tasks.Count; i++)
                    {
                        var task = tasks[i];
                        if (ReferenceEquals(task, winner))
                        {
                            continue;
                        }

                        var ignored = task.ContinueWith(t => { var error = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                    }
                }
                catch (OperationCanceledException)
                {
                    if (token.IsCancellationRequested)
                    {
                        throw;
                    }

                    winner = null;
                }
                finally
                {
                    // 无论是否胜出都取消其余接受任务，避免监听端点被长期占用等待
                    cts.Cancel();
                }

                if (!ReferenceEquals(winner, null))
                {
                    accepted = await winner.ConfigureAwait(false);
                }
            }

            if (accepted == null)
            {
                return "未收到 TCP 直连连接";
            }

            var remaining = stageBudget - clock.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                accepted.Dispose();
                return "TCP 直连未打通";
            }

            var channel = new TcpDirectChannel(_parameters.LocalUfrag, _parameters.LocalPassword);
            channel.CheckTimeoutMilliseconds = (int)Math.Max(1, Math.Min(remaining.TotalMilliseconds, 5000));

            TcpDirectResult result;
            using (var attachCts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                attachCts.CancelAfter(remaining);

                try
                {
                    result = await channel.AttachAsync(accepted, attachCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    if (token.IsCancellationRequested)
                    {
                        channel.Dispose();
                        throw;
                    }

                    result = null;
                }
            }

            // 附加失败时通道已关闭入向连接，不能再重复释放 accepted
            if (result == null || !result.Succeeded)
            {
                channel.Dispose();
                return "TCP 直连未打通";
            }

            var localEndPoint = accepted.LocalEndPoint;
            if (localEndPoint == null)
            {
                channel.Dispose();
                return "入向连接缺少本地端点";
            }

            Candidate local = null;
            var gatherer = _tcpGatherer;
            if (gatherer != null)
            {
                var candidates = gatherer.Candidates;
                if (candidates != null)
                {
                    for (int i = 0; i < candidates.Count; i++)
                    {
                        var candidate = candidates[i];
                        if (candidate != null
                            && candidate.Transport == IceTransportType.Tcp
                            && candidate.TcpType == TcpCandidateType.Passive
                            && candidate.EndPoint.Equals(localEndPoint))
                        {
                            local = candidate;
                            break;
                        }
                    }
                }
            }

            if (local == null)
            {
                local = new Candidate(
                    CandidateType.Host,
                    IceTransportType.Tcp,
                    _parameters.Component,
                    localEndPoint.Address,
                    localEndPoint.Port,
                    TcpCandidateType.Passive,
                    null,
                    0);
            }

            var remote = FindRemoteActiveTcpCandidate(localEndPoint.Address.AddressFamily);
            if (remote == null)
            {
                // 对端主动候选可能尚未通过 trickle 到达，在剩余预算内最多再等 1 秒
                var waitBudget = stageBudget - clock.Elapsed;
                if (waitBudget > TimeSpan.Zero)
                {
                    var maxWait = TimeSpan.FromMilliseconds(1000);
                    var waitLimit = waitBudget < maxWait ? waitBudget : maxWait;
                    var waitClock = Stopwatch.StartNew();

                    while (remote == null)
                    {
                        token.ThrowIfCancellationRequested();

                        var waitRemaining = waitLimit - waitClock.Elapsed;
                        if (waitRemaining <= TimeSpan.Zero)
                        {
                            break;
                        }

                        var delay = waitRemaining < CandidatePollInterval ? waitRemaining : CandidatePollInterval;

                        // Task.Delay 不接受 0，向上取整到 1 毫秒
                        await Task.Delay((int)Math.Max(1, Math.Ceiling(delay.TotalMilliseconds)), token).ConfigureAwait(false);
                        remote = FindRemoteActiveTcpCandidate(localEndPoint.Address.AddressFamily);
                    }
                }
            }

            if (remote == null)
            {
                channel.Dispose();
                return "找不到对端的主动 TCP 候选";
            }

            var pair = new CandidatePair(local, remote, false);
            if (pair.State == PairState.Frozen) pair.MarkWaiting();
            if (pair.State != PairState.InProgress) pair.MarkInProgress();
            pair.MarkSucceeded();
            pair.MarkValid();
            pair.MarkNominated();
            channel.FrameReceived += OnFrameReceived;

            lock (_syncRoot)
            {
                _tcpDirectChannel = channel;
                _selectedPair = new SelectedPair(pair);
            }

            return null;
        }

        /// <summary>
        /// 在对端已登记的候选里找出与指定地址族匹配的主动 TCP 候选
        /// </summary>
        /// <remarks>
        /// 传输契约不暴露入向连接的对端来源端点，被动侧只能按对端已登记的主动候选匹配。
        /// </remarks>
        /// <param name="family">入向连接的地址族</param>
        /// <returns>匹配的主动候选；尚未到达时返回 null</returns>
        private Candidate FindRemoteActiveTcpCandidate(AddressFamily family)
        {
            var remoteCandidates = _parameters.RemoteCandidates;
            if (remoteCandidates == null)
            {
                return null;
            }

            for (int i = 0; i < remoteCandidates.Count; i++)
            {
                var candidate = remoteCandidates[i];
                if (candidate != null
                    && candidate.Transport == IceTransportType.Tcp
                    && candidate.TcpType == TcpCandidateType.Active
                    && candidate.Address != null
                    && candidate.Address.AddressFamily == family)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// 由本端 TCP 监听候选生成主动连接候选，同一监听地址只生成一条
        /// </summary>
        /// <returns>主动候选列表；无监听候选时为空列表</returns>
        private List<Candidate> BuildActiveTcpCandidates()
        {
            var result = new List<Candidate>();

            var gatherer = _tcpGatherer;
            if (gatherer == null)
            {
                return result;
            }

            var candidates = gatherer.Candidates;
            if (candidates == null)
            {
                return result;
            }

            var addresses = new List<IPAddress>();
            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate == null || candidate.Transport != IceTransportType.Tcp || candidate.TcpType != TcpCandidateType.Passive)
                {
                    continue;
                }

                var address = candidate.Address;
                if (address == null || addresses.Contains(address))
                {
                    continue;
                }

                addresses.Add(address);

                // 主动候选的本地端口由操作系统在连接时分配，候选本身只描述地址
                result.Add(new Candidate(
                    CandidateType.Host,
                    IceTransportType.Tcp,
                    _parameters.Component,
                    address,
                    0,
                    TcpCandidateType.Active,
                    null,
                    0));
            }

            return result;
        }

        /// <summary>
        /// TURN 中继阶段共享主干：先申请中继候选与中继数据通道，再在中继通道上做连通性检查与提名
        /// </summary>
        /// <remarks>
        /// 控制传输按阶段选择：TurnUdp 用调用方给的传输工厂，TurnTcp / TurnTls 用内置 TCP / TLS 控制传输；
        /// 中继对端仍是 UDP，因此中继路径上的检查请求必须从分配的中继端口发出，检查与提名都用中继数据通道本身作为传输；
        /// 每次检查前还要为该对端地址建立中继权限，否则服务端会直接丢弃发往该地址的报文。
        /// 分配在中继阶段成功时保留给数据面使用，失败时先向服务端发 LIFETIME=0 再清理本地资源。
        /// </remarks>
        /// <param name="stage">当前中继阶段，决定客户端到 TURN 的控制传输</param>
        /// <param name="stageBudget">本阶段可用预算</param>
        /// <param name="token">联合取消令牌</param>
        /// <returns>失败原因；选中通道时为 null</returns>
        private async Task<string> RunTurnRelayAsync(IceStage stage, TimeSpan stageBudget, CancellationToken token)
        {
            if (_options.Turn == null)
            {
                return "未配置 TURN 服务";
            }

            if (_options.Turn.ServerEndPoint == null)
            {
                return "TURN 服务端端点未配置";
            }

            var clock = Stopwatch.StartNew();

            // 客户端与提供器不放进 using：中继阶段成功时它们要继续为数据面服务
            TurnClient client = null;
            RelayCandidateProvider provider = null;

            try
            {
                client = new TurnClient(CreateStageTurnOptions(stage));
                provider = new RelayCandidateProvider(client, _parameters.Component);

                var allocateRemaining = stageBudget - clock.Elapsed;
                if (allocateRemaining <= TimeSpan.Zero)
                {
                    await FailTurnStageAsync(provider, client).ConfigureAwait(false);
                    return "TURN 分配超时";
                }

                Candidate relayCandidate;
                using (var allocateCts = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    allocateCts.CancelAfter(allocateRemaining);

                    try
                    {
                        relayCandidate = await provider.AllocateAsync(allocateCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        if (token.IsCancellationRequested)
                        {
                            throw;
                        }

                        await FailTurnStageAsync(provider, client).ConfigureAwait(false);
                        return "TURN 分配超时";
                    }
                    catch (TurnClientException ex)
                    {
                        await FailTurnStageAsync(provider, client).ConfigureAwait(false);
                        P2PLogHelper.Debug(LogNode, "TURN 分配失败：" + ex.Message);
                        return "TURN 分配失败：" + ex.Message;
                    }
                    catch (Exception ex) when (ex is SocketException || ex is System.IO.IOException)
                    {
                        // TCP 控制传输建链被拒绝等传输层失败只说明本级不通，交给阶梯继续下一级
                        await FailTurnStageAsync(provider, client).ConfigureAwait(false);
                        P2PLogHelper.Debug(LogNode, "TURN 控制传输失败：" + ex.Message);
                        return "TURN 控制传输失败：" + ex.Message;
                    }
                }

                var channel = provider.Channel;
                if (relayCandidate == null || channel == null)
                {
                    await FailTurnStageAsync(provider, client).ConfigureAwait(false);
                    return "TURN 分配失败：未返回中继端点";
                }

                P2PLogHelper.Debug(LogNode, "TURN 中继候选：" + relayCandidate.EndPoint);

                // 字段先于候选发布赋值，后续失败路径的清理才能释放同一批实例
                lock (_syncRoot)
                {
                    _turnClient = client;
                    _turnProvider = provider;
                }

                if (TryAddLocalCandidate(relayCandidate, false))
                {
                    RaiseLocalCandidateDiscovered(relayCandidate);
                }

                // 中继阶段单独建检查表：只放本地中继候选与对端 UDP 候选，避免混入直连阶段已失败的组合
                var checkList = new CheckList(_parameters.Role == IceRole.Controlling);
                checkList.AddLocalCandidate(relayCandidate);

                var remotes = _parameters.RemoteCandidates;
                if (remotes != null)
                {
                    for (int i = 0; i < remotes.Count; i++)
                    {
                        if (remotes[i] != null && remotes[i].Transport == IceTransportType.Udp)
                        {
                            checkList.AddRemoteCandidate(remotes[i]);
                        }
                    }
                }

                var check = new ConnectivityCheck(_parameters.LocalUfrag, _parameters.LocalPassword);

                lock (_syncRoot)
                {
                    _checkList = checkList;
                }

                _connectivityCheck = check;

                // 中继通道也要应答入向检查：对端从中继端口发来的请求只有本通道能收到
                var responder = new IceInboundResponder(new IUdpTransport[] { channel }, _parameters.LocalUfrag, _parameters.LocalPassword);
                _responder = responder;
                responder.CheckRequestReceived += OnInboundCheckRequest;
                responder.Start();

                var permissionRemaining = stageBudget - clock.Elapsed;
                if (permissionRemaining <= TimeSpan.Zero)
                {
                    await FailTurnStageAsync(provider, client).ConfigureAwait(false);
                    return "TURN 分配超时";
                }

                var remoteAddresses = CollectRemoteUdpAddresses(remotes);
                using (var permissionCts = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    permissionCts.CancelAfter(permissionRemaining);

                    try
                    {
                        await channel.CreatePermissionsAsync(remoteAddresses, permissionCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        if (token.IsCancellationRequested)
                        {
                            throw;
                        }

                        await FailTurnStageAsync(provider, client).ConfigureAwait(false);
                        return "TURN 权限建立超时";
                    }
                    catch (TurnClientException ex)
                    {
                        await FailTurnStageAsync(provider, client).ConfigureAwait(false);
                        P2PLogHelper.Debug(LogNode, "TURN 权限建立失败：" + ex.Message);
                        return "TURN 权限建立失败：" + ex.Message;
                    }
                }

                var isControlling = _parameters.Role == IceRole.Controlling;

                // 已建权限的地址；对端中继候选常在本阶段内才 trickle 到达，循环里补建缺失的地址
                var permitted = new HashSet<IPAddress>();
                for (int i = 0; i < remoteAddresses.Count; i++)
                {
                    permitted.Add(remoteAddresses[i]);
                }

                // 已尝试的候选对按实例记录，避免同一对在每轮重取快照后被反复检查而耗尽预算
                var attempted = new HashSet<CandidatePair>();
                var triedAny = false;

                while (true)
                {
                    token.ThrowIfCancellationRequested();

                    var remaining = stageBudget - clock.Elapsed;
                    if (remaining <= TimeSpan.Zero)
                    {
                        break;
                    }

                    await EnsureRelayPermissionsAsync(channel, permitted, remaining, token).ConfigureAwait(false);

                    // 受控方不发起检查也不提名，只等对端 USE-CANDIDATE
                    if (!isControlling)
                    {
                        if (_remoteNomination.Task.IsCompleted)
                        {
                            if (TrySelectNominatedPair())
                            {
                                return null;
                            }

                            await FailTurnStageAsync(provider, client).ConfigureAwait(false);
                            return "对端提名缺少可用传输";
                        }

                        var nominationWait = remaining < CandidatePollInterval ? remaining : CandidatePollInterval;
                        await Task.WhenAny(_remoteNomination.Task, Task.Delay(nominationWait, token)).ConfigureAwait(false);
                        continue;
                    }

                    var pair = PickNextTurnPair(checkList, attempted);
                    if (pair == null)
                    {
                        // 还没有新的可检查候选对，等对端 trickle 后重取快照
                        var pollWait = remaining < CandidatePollInterval ? remaining : CandidatePollInterval;
                        await Task.Delay(pollWait, token).ConfigureAwait(false);
                        continue;
                    }

                    attempted.Add(pair);
                    triedAny = true;

                    var pairTimeout = TimeSpan.FromMilliseconds(
                        Math.Max(1, Math.Min(StunTransactionTiming.GetTimeoutMilliseconds(500), (stageBudget - clock.Elapsed).TotalMilliseconds)));

                    ConnectivityCheckResult result;
                    using (var pairCts = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        pairCts.CancelAfter(pairTimeout);

                        try
                        {
                            result = await check.CheckAsync(
                                channel,
                                pair.Remote.EndPoint,
                                _parameters.RemoteUfrag,
                                _parameters.RemotePassword,
                                _parameters.Role,
                                _parameters.Tiebreaker,
                                check.ComputePeerReflexivePriority(pair.Local.Component),
                                false,
                                channel,
                                new IPAddress[] { pair.Remote.Address },
                                pairCts.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            if (token.IsCancellationRequested)
                            {
                                throw;
                            }

                            // 单对超时不终止本阶段，换下一对继续
                            pair.MarkFailed();
                            continue;
                        }
                        catch (TurnClientException ex)
                        {
                            // 中继权限或通道发送被服务端拒绝时只作废这一对，不能让整级阶段中断
                            P2PLogHelper.Debug(LogNode, "TURN 中继检查失败：" + ex.Message);
                            pair.MarkFailed();
                            continue;
                        }
                    }

                    if (result == null || !result.Succeeded)
                    {
                        pair.MarkFailed();

                        // 角色冲突说明双方同为控制方或同为受控方，只提示不改变本地角色
                        if (result != null && result.RoleConflict)
                        {
                            P2PLogHelper.Debug(LogNode, "TURN 中继检查出现角色冲突：" + pair.Remote.EndPoint);
                        }

                        continue;
                    }

                    if (pair.State == PairState.Frozen) pair.MarkWaiting();
                    if (pair.State != PairState.InProgress) pair.MarkInProgress();
                    pair.MarkSucceeded();
                    pair.MarkValid();

                    var nominateRemaining = stageBudget - clock.Elapsed;
                    if (nominateRemaining <= TimeSpan.Zero)
                    {
                        break;
                    }

                    if (_nominator == null)
                    {
                        _nominator = new Nominator(
                            _parameters.Role,
                            _parameters.Tiebreaker,
                            _parameters.LocalUfrag,
                            _parameters.LocalPassword,
                            _parameters.RemoteUfrag,
                            _parameters.RemotePassword,
                            checkList);
                    }

                    bool nominated;
                    using (var nominateCts = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        nominateCts.CancelAfter(nominateRemaining);

                        try
                        {
                            nominated = await _nominator.NominateAsync(
                                channel,
                                pair,
                                channel,
                                new IPAddress[] { pair.Remote.Address },
                                nominateCts.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            if (token.IsCancellationRequested)
                            {
                                throw;
                            }

                            nominated = false;
                        }
                        catch (TurnClientException ex)
                        {
                            P2PLogHelper.Debug(LogNode, "TURN 中继提名失败：" + ex.Message);
                            nominated = false;
                        }
                    }

                    if (nominated)
                    {
                        SelectUdpPair(pair, channel);
                        return null;
                    }
                }

                await FailTurnStageAsync(provider, client).ConfigureAwait(false);

                if (!isControlling)
                {
                    return "等待对端提名超时";
                }

                return triedAny ? "TURN 中继未打通" : "没有可配对的 TURN 候选";
            }
            catch
            {
                // 异常与取消路径同样要释放服务端分配，否则中继端口会一直挂在服务端
                await FailTurnStageAsync(provider, client).ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>
        /// TURN over UDP 阶段：客户端到 TURN 的控制连接走 UDP，中继对端也是 UDP
        /// </summary>
        /// <remarks>
        /// 本阶段与 TURN over TCP 的差别只在控制传输，中继对端始终是 UDP。
        /// UDP 控制传输承载不了 RFC 6062 的 Connect/ConnectionBind，因此本级不尝试 TCP 中继，
        /// 避免多打一次注定失败的分配。
        /// </remarks>
        /// <param name="stageBudget">本阶段可用预算</param>
        /// <param name="token">联合取消令牌</param>
        /// <returns>失败原因；选中通道时为 null</returns>
        private async Task<string> RunTurnUdpAsync(TimeSpan stageBudget, CancellationToken token)
        {
            return await RunTurnRelayAsync(IceStage.TurnUdp, stageBudget, token).ConfigureAwait(false);
        }

        /// <summary>
        /// TURN over TCP 阶段：先用半个阶段预算试 UDP 中继，剩余预算再试 RFC 6062 TCP 中继
        /// </summary>
        /// <remarks>
        /// 控制连接走 TCP 时两种中继都可用：UDP 中继的数据面仍是 UDP，TCP 中继的数据面是 Connect/ConnectionBind 得到的 TCP 连接。
        /// </remarks>
        /// <param name="stageBudget">本阶段可用预算</param>
        /// <param name="token">联合取消令牌</param>
        /// <returns>失败原因；选中通道时为 null</returns>
        private async Task<string> RunTurnTcpAsync(TimeSpan stageBudget, CancellationToken token)
        {
            return await RunTurnStageWithTcpRelayAsync(IceStage.TurnTcp, stageBudget, token).ConfigureAwait(false);
        }

        /// <summary>
        /// TURN over TLS 阶段：先用半个阶段预算试 UDP 中继，剩余预算再试 RFC 6062 TCP 中继
        /// </summary>
        /// <remarks>
        /// 控制连接走 TLS 时两种中继都可用：UDP 中继的数据面仍是 UDP，TCP 中继的数据面是 TLS 上的 Connect/ConnectionBind 连接。
        /// </remarks>
        /// <param name="stageBudget">本阶段可用预算</param>
        /// <param name="token">联合取消令牌</param>
        /// <returns>失败原因；选中通道时为 null</returns>
        private async Task<string> RunTurnTlsAsync(TimeSpan stageBudget, CancellationToken token)
        {
            return await RunTurnStageWithTcpRelayAsync(IceStage.TurnTls, stageBudget, token).ConfigureAwait(false);
        }

        /// <summary>
        /// TCP / TLS 控制下的中继阶段：先用半个阶段预算试 UDP 中继，剩余预算再试 RFC 6062 TCP 中继
        /// </summary>
        /// <remarks>
        /// 五五分是固定切分：UDP 中继是同一控制连接上更省事的路径，成功即返回；
        /// 两段都失败时把两段失败原因一起返回，便于定位是哪一层不通。
        /// </remarks>
        /// <param name="stage">当前阶段</param>
        /// <param name="stageBudget">本阶段可用预算</param>
        /// <param name="token">联合取消令牌</param>
        /// <returns>失败原因；选中通道时为 null</returns>
        private async Task<string> RunTurnStageWithTcpRelayAsync(IceStage stage, TimeSpan stageBudget, CancellationToken token)
        {
            var clock = Stopwatch.StartNew();
            var udpBudget = TimeSpan.FromTicks(stageBudget.Ticks / 2);

            var udpFailure = await RunTurnRelayAsync(stage, udpBudget, token).ConfigureAwait(false);
            if (udpFailure == null)
            {
                return null;
            }

            var remaining = stageBudget - clock.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                return udpFailure;
            }

            var tcpFailure = await RunTurnTcpRelayAsync(stage, remaining, token).ConfigureAwait(false);
            if (tcpFailure == null)
            {
                return null;
            }

            return udpFailure + "；" + tcpFailure;
        }

        /// <summary>
        /// RFC 6062 TCP 中继：申请 TCP 分配、等对端的 TCP 中继候选，再按角色主动 Connect 或接受入向连接并做一次检查
        /// </summary>
        /// <remarks>
        /// TCP 中继的数据面是服务端转发后得到的独立 TCP 连接，控制连接只负责分配与转发请求；
        /// 控制方坚持用 useCandidate 提名，受控方只应答对向检查，两端在同一连接上的检查可以并存。
        /// 分配成功后字段先于候选发布赋值，失败路径才能释放同一批实例并向服务端发 LIFETIME=0。
        /// </remarks>
        /// <param name="stage">当前阶段，决定控制连接走 TCP 还是 TLS</param>
        /// <param name="stageBudget">本阶段可用预算</param>
        /// <param name="token">联合取消令牌</param>
        /// <returns>失败原因；选中通道时为 null</returns>
        private async Task<string> RunTurnTcpRelayAsync(IceStage stage, TimeSpan stageBudget, CancellationToken token)
        {
            if (_options.Turn == null)
            {
                return "未配置 TURN 服务";
            }

            if (_options.Turn.ServerEndPoint == null)
            {
                return "TURN 服务端端点未配置";
            }

            var clock = Stopwatch.StartNew();

            // 客户端与提供器不放进 using：中继阶段成功时它们要继续为数据面服务
            TurnClient client = null;
            TcpRelayCandidateProvider provider = null;
            TcpDirectChannel channel = null;
            var isControlling = _parameters.Role == IceRole.Controlling;

            try
            {
                client = new TurnClient(CreateTurnTcpRelayOptions(stage));
                provider = new TcpRelayCandidateProvider(client, _parameters.Component);

                var allocateRemaining = stageBudget - clock.Elapsed;
                if (allocateRemaining <= TimeSpan.Zero)
                {
                    await FailTurnTcpRelayStageAsync(provider, client, null).ConfigureAwait(false);
                    return "TURN TCP 中继分配超时";
                }

                Candidate relayCandidate;
                using (var allocateCts = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    allocateCts.CancelAfter(allocateRemaining);

                    try
                    {
                        relayCandidate = await provider.AllocateAsync(allocateCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        if (token.IsCancellationRequested)
                        {
                            throw;
                        }

                        await FailTurnTcpRelayStageAsync(provider, client, null).ConfigureAwait(false);
                        return "TURN TCP 中继分配超时";
                    }
                    catch (TurnClientException ex)
                    {
                        await FailTurnTcpRelayStageAsync(provider, client, null).ConfigureAwait(false);
                        P2PLogHelper.Debug(LogNode, "TURN TCP 中继分配失败：" + ex.Message);
                        return "TURN TCP 中继分配失败：" + ex.Message;
                    }
                    catch (Exception ex) when (ex is SocketException || ex is System.IO.IOException)
                    {
                        // 控制传输建链被拒绝等传输层失败只说明本级不通，交给阶梯继续下一级
                        await FailTurnTcpRelayStageAsync(provider, client, null).ConfigureAwait(false);
                        P2PLogHelper.Debug(LogNode, "TURN TCP 中继控制传输失败：" + ex.Message);
                        return "TURN TCP 中继控制传输失败：" + ex.Message;
                    }
                }

                if (relayCandidate == null)
                {
                    await FailTurnTcpRelayStageAsync(provider, client, null).ConfigureAwait(false);
                    return "TURN TCP 中继分配失败：未返回中继端点";
                }

                // 字段先于候选发布赋值，后续失败路径的清理才能释放同一批实例
                lock (_syncRoot)
                {
                    _tcpRelayClient = client;
                    _tcpRelayProvider = provider;
                }

                if (TryAddLocalCandidate(relayCandidate, false))
                {
                    RaiseLocalCandidateDiscovered(relayCandidate);
                }

                P2PLogHelper.Debug(LogNode, "TURN TCP 中继候选：" + relayCandidate.EndPoint);

                // 两端都要拿到对端的 TCP 中继候选：受控方拿它作为候选对的远端，控制方还要连它的中继地址
                Candidate remote = FindRemoteTcpRelayCandidate();
                while (remote == null)
                {
                    token.ThrowIfCancellationRequested();

                    var waitRemaining = stageBudget - clock.Elapsed;
                    if (waitRemaining <= TimeSpan.Zero)
                    {
                        break;
                    }

                    var pollWait = waitRemaining < CandidatePollInterval ? waitRemaining : CandidatePollInterval;

                    // Task.Delay 不接受 0，向上取整到 1 毫秒
                    await Task.Delay((int)Math.Max(1, Math.Ceiling(pollWait.TotalMilliseconds)), token).ConfigureAwait(false);
                    remote = FindRemoteTcpRelayCandidate();
                }

                if (remote == null)
                {
                    await FailTurnTcpRelayStageAsync(provider, client, null).ConfigureAwait(false);
                    return "找不到对端的 TCP 中继候选";
                }

                var remaining = stageBudget - clock.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    await FailTurnTcpRelayStageAsync(provider, client, null).ConfigureAwait(false);
                    return "TURN TCP 中继未打通";
                }

                channel = new TcpDirectChannel(_parameters.LocalUfrag, _parameters.LocalPassword);

                // 单次检查超时不能超过本阶段剩余预算，同时给通道留出最小可用的超时值
                channel.CheckTimeoutMilliseconds = (int)Math.Max(1, Math.Min(remaining.TotalMilliseconds, 5000));

                TcpDirectResult result = null;
                string transportFailure = null;

                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    cts.CancelAfter(remaining);

                    ITcpTransport transport = null;
                    if (isControlling)
                    {
                        try
                        {
                            transport = await provider.ConnectAndBindAsync(remote.EndPoint, cts.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            if (token.IsCancellationRequested)
                            {
                                throw;
                            }

                            transportFailure = "TURN TCP 中继连接超时";
                        }
                        catch (TurnClientException ex)
                        {
                            P2PLogHelper.Debug(LogNode, "TURN TCP 中继连接失败：" + ex.Message);
                            transportFailure = "TURN TCP 中继连接失败：" + ex.Message;
                        }
                        catch (Exception ex) when (ex is SocketException || ex is System.IO.IOException)
                        {
                            P2PLogHelper.Debug(LogNode, "TURN TCP 中继连接失败：" + ex.Message);
                            transportFailure = "TURN TCP 中继连接失败：" + ex.Message;
                        }
                    }
                    else
                    {
                        while (transport == null && transportFailure == null)
                        {
                            token.ThrowIfCancellationRequested();

                            TurnConnectionAttempt attempt;
                            if (provider.TryTakeConnectionAttempt(out attempt))
                            {
                                try
                                {
                                    transport = await provider.BindInboundAsync(attempt, cts.Token).ConfigureAwait(false);
                                }
                                catch (OperationCanceledException)
                                {
                                    if (token.IsCancellationRequested)
                                    {
                                        throw;
                                    }

                                    transportFailure = "TURN TCP 中继绑定超时";
                                }
                                catch (TurnClientException ex)
                                {
                                    P2PLogHelper.Debug(LogNode, "TURN TCP 中继绑定失败：" + ex.Message);
                                    transportFailure = "TURN TCP 中继绑定失败：" + ex.Message;
                                }
                                catch (Exception ex) when (ex is SocketException || ex is System.IO.IOException)
                                {
                                    P2PLogHelper.Debug(LogNode, "TURN TCP 中继绑定失败：" + ex.Message);
                                    transportFailure = "TURN TCP 中继绑定失败：" + ex.Message;
                                }

                                break;
                            }

                            var waitRemaining = stageBudget - clock.Elapsed;
                            if (waitRemaining <= TimeSpan.Zero)
                            {
                                // 预算耗尽只是本级没等到连接，不当作异常
                                transportFailure = "未收到 TURN TCP 中继入向连接";
                                break;
                            }

                            var pollWait = waitRemaining < CandidatePollInterval ? waitRemaining : CandidatePollInterval;
                            await Task.Delay((int)Math.Max(1, Math.Ceiling(pollWait.TotalMilliseconds)), token).ConfigureAwait(false);
                        }
                    }

                    if (transportFailure == null && transport != null)
                    {
                        var priority = IcePriority.ComputeForTcp(CandidateType.Relay, _parameters.Component, TcpCandidateType.SimultaneousOpen);

                        try
                        {
                            result = await channel.CheckAsync(
                                transport,
                                remote.EndPoint,
                                _parameters.RemoteUfrag,
                                _parameters.RemotePassword,
                                _parameters.Role,
                                _parameters.Tiebreaker,
                                priority,
                                isControlling,
                                cts.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            if (token.IsCancellationRequested)
                            {
                                throw;
                            }

                            result = null;
                        }
                    }
                }

                if (transportFailure != null)
                {
                    channel.Dispose();
                    await FailTurnTcpRelayStageAsync(provider, client, channel).ConfigureAwait(false);
                    return transportFailure;
                }

                if (result == null || !result.Succeeded)
                {
                    // 检查失败时通道已关闭中继数据连接，这里只需释放本地对象与服务端分配
                    channel.Dispose();
                    await FailTurnTcpRelayStageAsync(provider, client, channel).ConfigureAwait(false);
                    P2PLogHelper.Debug(LogNode, "TURN TCP 中继检查失败："
                        + (result == null ? "超时" : result.Failure + " / " + result.ErrorReason));
                    return "TURN TCP 中继检查失败：" + (result == null ? "超时" : result.Failure + " / " + result.ErrorReason);
                }

                channel.FrameReceived += OnFrameReceived;

                var pair = new CandidatePair(relayCandidate, remote, isControlling);
                if (pair.State == PairState.Frozen) pair.MarkWaiting();
                if (pair.State != PairState.InProgress) pair.MarkInProgress();
                pair.MarkSucceeded();
                pair.MarkValid();
                pair.MarkNominated();

                lock (_syncRoot)
                {
                    _tcpDirectChannel = channel;
                    _selectedPair = new SelectedPair(pair);
                }

                return null;
            }
            catch
            {
                // 异常与取消路径同样要释放服务端分配，否则中继端口会一直挂在服务端
                await FailTurnTcpRelayStageAsync(provider, client, channel).ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>
        /// 在对端已登记的候选里找出 TCP 中继候选
        /// </summary>
        /// <returns>第一个 Type 为 Relay 且 Transport 为 Tcp 的候选；尚未到达时返回 null</returns>
        private Candidate FindRemoteTcpRelayCandidate()
        {
            var remoteCandidates = _parameters.RemoteCandidates;
            if (remoteCandidates == null)
            {
                return null;
            }

            for (int i = 0; i < remoteCandidates.Count; i++)
            {
                var candidate = remoteCandidates[i];
                if (candidate != null
                    && candidate.Type == CandidateType.Relay
                    && candidate.Transport == IceTransportType.Tcp)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// 按阶段生成 TURN 客户端配置：始终返回副本，并把 REQUESTED-TRANSPORT 强制为 UDP
        /// </summary>
        /// <remarks>
        /// TurnUdp 也返回副本；副本保留调用方给的传输工厂与本地绑定，因此行为与原「直接复用调用方实例」一致，
        /// 而 RequestedTransportProtocol 一律被会话改成 17，调用方填的值在会话级不生效。
        /// 其它阶段不能就地改调用方实例，否则同一次会话里上一级的传输选择会被后一级覆盖。
        /// TurnTcp / TurnTls 换成内置的 TCP / TLS 控制传输，TurnTls 的目标主机名与是否忽略证书错误取会话配置。
        /// </remarks>
        /// <param name="stage">当前中继阶段</param>
        /// <returns>供本阶段 TurnClient 使用的配置</returns>
        private TurnClientOptions CreateStageTurnOptions(IceStage stage)
        {
            var clone = CloneTurnOptions(_options.Turn);

            // 会话在 UDP 中继段只用 UDP 分配；调用方填的 RequestedTransportProtocol 在会话级不生效
            clone.RequestedTransportProtocol = 17;

            if (stage == IceStage.TurnTcp)
            {
                clone.TransportFactory = () => new TcpTurnClientTransport(_options.Turn.ServerEndPoint);
            }

            if (stage == IceStage.TurnTls)
            {
                var targetHost = _options.TurnTlsTargetHost;
                if (string.IsNullOrEmpty(targetHost))
                {
                    // 配置没给域名时退回服务端地址，生产建议显式配置证书里的域名
                    targetHost = _options.Turn.ServerEndPoint.Address.ToString();
                }

                var allowInvalid = _options.TurnTlsAllowInvalidCertificates;
                clone.TransportFactory = () => new TurnTlsClientTransport(_options.Turn.ServerEndPoint, targetHost, allowInvalid);
            }

            return clone;
        }

        /// <summary>
        /// 按阶段生成 RFC 6062 TCP 中继的客户端配置
        /// </summary>
        /// <param name="stage">当前中继阶段</param>
        /// <returns>REQUESTED-TRANSPORT 为 6 的配置副本</returns>
        private TurnClientOptions CreateTurnTcpRelayOptions(IceStage stage)
        {
            var clone = CreateStageTurnOptions(stage);

            // RFC 6062 要求 TCP 分配：REQUESTED-TRANSPORT 必须是 6，数据连接也走 TCP / TLS
            clone.RequestedTransportProtocol = 6;
            return clone;
        }

        /// <summary>
        /// 按值复制 TURN 客户端配置
        /// </summary>
        /// <remarks>
        /// 按值复制配置对象，避免为某一级改掉调用方实例；复制后由调用方覆盖需要变的项。
        /// </remarks>
        /// <param name="source">源配置，非空</param>
        /// <returns>各项属性与源配置一致的副本</returns>
        private static TurnClientOptions CloneTurnOptions(TurnClientOptions source)
        {
            var clone = new TurnClientOptions();
            clone.ServerEndPoint = source.ServerEndPoint;
            clone.TransportFactory = source.TransportFactory;
            clone.Username = source.Username;
            clone.Password = source.Password;
            clone.Credential = source.Credential;
            clone.RtoMilliseconds = source.RtoMilliseconds;
            clone.AllocationRefreshInterval = source.AllocationRefreshInterval;
            clone.PermissionRefreshInterval = source.PermissionRefreshInterval;
            clone.ChannelRefreshInterval = source.ChannelRefreshInterval;
            clone.RefreshTimeout = source.RefreshTimeout;
            clone.ReconnectBackoffInitial = source.ReconnectBackoffInitial;
            clone.ReconnectBackoffMax = source.ReconnectBackoffMax;
            clone.TimerFactory = source.TimerFactory;
            clone.RequestedTransportProtocol = source.RequestedTransportProtocol;
            clone.DataTransportFactory = source.DataTransportFactory;
            clone.Software = source.Software;
            return clone;
        }

        /// <summary>
        /// 从检查表当前快照中挑出优先级最高的、尚未尝试过的 TURN 中继候选对
        /// </summary>
        /// <remarks>
        /// 只让本地中继候选发起检查：对端中继候选要由对端自己从中继端口发出才收得到应答，
        /// 本端直接向对端中继地址发报文会因缺少权限被服务端丢弃。
        /// </remarks>
        /// <param name="checkList">TURN 检查表，非空</param>
        /// <param name="attempted">已尝试过的候选对集合，非空</param>
        /// <returns>优先级最高的未尝试候选对；没有可检查的候选对时返回 null</returns>
        private CandidatePair PickNextTurnPair(CheckList checkList, HashSet<CandidatePair> attempted)
        {
            var pairs = new List<CandidatePair>(checkList.Pairs);
            CandidatePair best = null;

            for (int i = 0; i < pairs.Count; i++)
            {
                var pair = pairs[i];
                if (pair == null || pair.Local == null || pair.Remote == null)
                {
                    continue;
                }

                if (pair.Local.Transport != IceTransportType.Udp || pair.Remote.Transport != IceTransportType.Udp)
                {
                    continue;
                }

                if (pair.Local.Type != CandidateType.Relay)
                {
                    continue;
                }

                if (attempted.Contains(pair))
                {
                    continue;
                }

                if (best == null || pair.Priority > best.Priority)
                {
                    best = pair;
                }
            }

            return best;
        }

        /// <summary>
        /// 收集对端 UDP 候选的地址并去重，用于一次性建立中继权限
        /// </summary>
        /// <param name="remotes">对端候选，可为 null</param>
        /// <returns>去重后的地址列表；没有候选时为空列表</returns>
        private static List<IPAddress> CollectRemoteUdpAddresses(IReadOnlyList<Candidate> remotes)
        {
            var result = new List<IPAddress>();
            if (remotes == null)
            {
                return result;
            }

            for (int i = 0; i < remotes.Count; i++)
            {
                var remote = remotes[i];
                if (remote == null || remote.Transport != IceTransportType.Udp || remote.Address == null)
                {
                    continue;
                }

                // 权限按 IP 放行，同一地址的多个候选只需申请一次
                if (!result.Contains(remote.Address))
                {
                    result.Add(remote.Address);
                }
            }

            return result;
        }

        /// <summary>
        /// 为中继阶段新到达的对端地址补建权限，已有权限的地址跳过
        /// </summary>
        /// <remarks>
        /// 阶段开始时只按当时的对端候选快照建了一次权限，trickle 方式下后到的中继候选不在快照里，
        /// 服务端会直接丢弃这些地址的报文，导致本阶段白耗预算；补建失败只记日志，下一轮按剩余预算重试。
        /// </remarks>
        /// <param name="channel">中继数据通道，非空</param>
        /// <param name="permitted">已建立权限的地址集合，非空</param>
        /// <param name="budget">本次补建可用的预算</param>
        /// <param name="token">联合取消令牌</param>
        /// <returns>补建结束的任务</returns>
        private async Task EnsureRelayPermissionsAsync(TurnDataChannel channel, HashSet<IPAddress> permitted, TimeSpan budget, CancellationToken token)
        {
            var remotes = _parameters.RemoteCandidates;
            if (remotes == null || budget <= TimeSpan.Zero)
            {
                return;
            }

            List<IPAddress> missing = null;
            for (int i = 0; i < remotes.Count; i++)
            {
                var remote = remotes[i];
                if (remote == null || remote.Transport != IceTransportType.Udp || remote.Address == null)
                {
                    continue;
                }

                if (permitted.Contains(remote.Address))
                {
                    continue;
                }

                if (missing == null)
                {
                    missing = new List<IPAddress>();
                }

                // 权限按 IP 放行，同一地址的多个候选只需申请一次
                if (!missing.Contains(remote.Address))
                {
                    missing.Add(remote.Address);
                }
            }

            if (missing == null)
            {
                return;
            }

            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                cts.CancelAfter(budget);

                try
                {
                    await channel.CreatePermissionsAsync(missing, cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    if (token.IsCancellationRequested)
                    {
                        throw;
                    }

                    return;
                }
                catch (TurnClientException ex)
                {
                    P2PLogHelper.Debug(LogNode, "TURN 权限补建失败：" + ex.Message);
                    return;
                }
            }

            for (int i = 0; i < missing.Count; i++)
            {
                permitted.Add(missing[i]);
            }
        }

        /// <summary>
        /// TURN 阶段失败收尾：先摘掉会话字段，再释放服务端分配与本地资源
        /// </summary>
        /// <param name="provider">中继候选提供器，可为 null</param>
        /// <param name="client">TURN 客户端，可为 null</param>
        /// <returns>收尾完成的任务</returns>
        private async Task FailTurnStageAsync(RelayCandidateProvider provider, TurnClient client)
        {
            // 先摘字段，避免随后的 CleanupTurnStage 释放同一批实例两次
            lock (_syncRoot)
            {
                if (ReferenceEquals(_turnProvider, provider))
                {
                    _turnProvider = null;
                }
                if (ReferenceEquals(_turnClient, client))
                {
                    _turnClient = null;
                }
            }

            await ReleaseTurnResourcesAsync(provider, client).ConfigureAwait(false);
            CleanupTurnStage();
        }

        /// <summary>
        /// 释放 TURN 分配与客户端：先向服务端发 LIFETIME=0，再释放本地对象，异常只记日志
        /// </summary>
        /// <param name="provider">中继候选提供器，可为 null</param>
        /// <param name="client">TURN 客户端，可为 null</param>
        /// <returns>释放完成的任务</returns>
        private async Task ReleaseTurnResourcesAsync(RelayCandidateProvider provider, TurnClient client)
        {
            if (provider != null)
            {
                try
                {
                    // 中继端口只有服务端能回收，失败也要继续本地释放，但必须先尝试过
                    await provider.ReleaseAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    P2PLogHelper.Debug(LogNode, "释放 TURN 分配失败：" + ex.GetType().Name);
                }

                DisposeQuietly(provider, "TURN 中继提供器");
            }

            DisposeQuietly(client, "TURN 客户端");
        }

        /// <summary>
        /// TURN TCP 中继段失败收尾：摘字段、释放通道、向服务端发 LIFETIME=0 再释放本地对象
        /// </summary>
        /// <param name="provider">候选提供器，可为 null</param>
        /// <param name="client">TURN 客户端，可为 null</param>
        /// <param name="channel">已建立的 TCP 中继数据面通道，可为 null</param>
        /// <returns>收尾完成的任务</returns>
        private async Task FailTurnTcpRelayStageAsync(TcpRelayCandidateProvider provider, TurnClient client, TcpDirectChannel channel)
        {
            // 先摘字段，避免随后的 Dispose 或 CleanupTurnStage 释放同一批实例两次
            lock (_syncRoot)
            {
                if (ReferenceEquals(_tcpRelayProvider, provider))
                {
                    _tcpRelayProvider = null;
                }
                if (ReferenceEquals(_tcpRelayClient, client))
                {
                    _tcpRelayClient = null;
                }
                if (ReferenceEquals(_tcpDirectChannel, channel))
                {
                    _tcpDirectChannel = null;
                }
            }

            DisposeQuietly(channel, "TCP 中继数据面通道");

            if (provider != null)
            {
                try
                {
                    // 中继端口只有服务端能回收，失败也要继续本地释放，但必须先尝试过
                    await provider.ReleaseAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    P2PLogHelper.Debug(LogNode, "释放 TURN TCP 中继分配失败：" + ex.GetType().Name);
                }

                DisposeQuietly(provider, "TURN TCP 中继提供器");
            }

            DisposeQuietly(client, "TURN TCP 中继客户端");
        }

        /// <summary>
        /// 按本地候选端点找到对应的 UDP 传输，srflx 候选回退到其基地址与端口
        /// </summary>
        /// <remarks>
        /// 中继候选对应中继数据通道，不在本地 UDP 传输数组里
        /// </remarks>
        /// <param name="local">本地候选</param>
        /// <returns>匹配的传输；没有匹配时返回 null</returns>
        private IUdpTransport FindUdpTransport(Candidate local)
        {
            if (local == null)
            {
                return null;
            }

            if (local.Type == CandidateType.Relay)
            {
                var provider = _turnProvider;
                if (provider != null)
                {
                    var relayChannel = provider.Channel;
                    if (relayChannel != null
                        && relayChannel.LocalEndPoint != null
                        && relayChannel.LocalEndPoint.Equals(local.EndPoint))
                    {
                        return relayChannel;
                    }
                }
            }

            var transports = _udpTransports;
            for (int i = 0; i < transports.Length; i++)
            {
                var transport = transports[i];
                if (transport == null || transport.LocalEndPoint == null)
                {
                    continue;
                }

                if (transport.LocalEndPoint.Equals(local.EndPoint))
                {
                    return transport;
                }
            }

            if (local.RelatedAddress == null)
            {
                return null;
            }

            for (int i = 0; i < transports.Length; i++)
            {
                var transport = transports[i];
                if (transport == null || transport.LocalEndPoint == null)
                {
                    continue;
                }

                var endPoint = transport.LocalEndPoint;
                if (endPoint.Address.Equals(local.RelatedAddress) && endPoint.Port == local.RelatedPort)
                {
                    return transport;
                }
            }

            return null;
        }

        /// <summary>
        /// 建立 UDP 数据面通道并记录选中的候选对
        /// </summary>
        /// <param name="pair">选中的候选对</param>
        /// <param name="transport">候选对对应的本地传输</param>
        private void SelectUdpPair(CandidatePair pair, IUdpTransport transport)
        {
            var channel = new UdpDataChannel(transport, pair.Remote.EndPoint);
            channel.FrameReceived += OnFrameReceived;
            channel.Start();

            var nominator = _nominator;

            lock (_syncRoot)
            {
                _udpDataChannel = channel;
                // 控制方以提名器记录的候选对为准，受控方用对端提名的候选对
                _selectedPair = (nominator != null && nominator.SelectedPair != null)
                    ? nominator.SelectedPair
                    : new SelectedPair(pair);
            }
        }

        /// <summary>
        /// 选取对端提名的候选对并建立通道
        /// </summary>
        /// <returns>选中成功返回 true</returns>
        private bool TrySelectNominatedPair()
        {
            if (!_remoteNomination.Task.IsCompleted)
            {
                return false;
            }

            var nominated = _remoteNomination.Task.Result;
            if (nominated == null)
            {
                return false;
            }

            var transport = FindUdpTransport(nominated.Local);
            if (transport == null)
            {
                P2PLogHelper.Debug(LogNode, "提名候选对找不到对应传输：" + nominated.Local.EndPoint);
                return false;
            }

            SelectUdpPair(nominated, transport);
            return true;
        }

        /// <summary>
        /// 处理入向检查请求：受控方收到 USE-CANDIDATE 即按对端提名确定候选对
        /// </summary>
        /// <param name="transport">收到请求的传输</param>
        /// <param name="source">请求来源端点</param>
        /// <param name="priority">请求 PRIORITY 属性的值，缺失时为 0</param>
        /// <param name="useCandidate">请求是否携带 USE-CANDIDATE</param>
        private void OnInboundCheckRequest(IUdpTransport transport, IPEndPoint source, uint priority, bool useCandidate)
        {
            if (_parameters.Role != IceRole.Controlled)
            {
                return;
            }

            if (!useCandidate)
            {
                return;
            }

            Candidate local = null;
            lock (_syncRoot)
            {
                for (int i = 0; i < _localCandidates.Count; i++)
                {
                    var candidate = _localCandidates[i];
                    if (candidate.Transport == IceTransportType.Udp
                        && candidate.EndPoint.Equals(transport.LocalEndPoint))
                    {
                        local = candidate;
                        break;
                    }
                }
            }

            if (local == null)
            {
                P2PLogHelper.Debug(LogNode, "入向检查请求找不到本地候选：" + transport.LocalEndPoint);
                return;
            }

            var remote = new Candidate(
                CandidateType.PeerReflexive,
                IceTransportType.Udp,
                _parameters.Component,
                source.Address,
                source.Port,
                priority);

            var pair = new CandidatePair(local, remote, false);
            if (pair.State == PairState.Frozen) pair.MarkWaiting();
            if (pair.State != PairState.InProgress) pair.MarkInProgress();
            pair.MarkSucceeded();
            pair.MarkValid();
            pair.MarkNominated();
            _remoteNomination.TrySetResult(pair);
        }

        /// <summary>
        /// 把候选加入本地列表，已存在的同端点候选不重复加入
        /// </summary>
        /// <param name="candidate">新候选</param>
        /// <param name="addToCheckList">是否同时登记到 UDP 检查表</param>
        /// <returns>加入成功返回 true，重复候选返回 false</returns>
        private bool TryAddLocalCandidate(Candidate candidate, bool addToCheckList)
        {
            lock (_syncRoot)
            {
                for (int i = 0; i < _localCandidates.Count; i++)
                {
                    var existing = _localCandidates[i];
                    if (existing.Transport == candidate.Transport
                        && existing.TcpType == candidate.TcpType
                        && existing.EndPoint.Equals(candidate.EndPoint))
                    {
                        return false;
                    }
                }

                _localCandidates.Add(candidate);

                if (addToCheckList && _checkList != null)
                {
                    _checkList.AddLocalCandidate(candidate);
                }

                return true;
            }
        }

        /// <summary>
        /// 逐订阅者触发本地候选发现事件；单个订阅者异常不影响其余订阅者
        /// </summary>
        /// <param name="candidate">新候选</param>
        private void RaiseLocalCandidateDiscovered(Candidate candidate)
        {
            var handler = LocalCandidateDiscovered;
            if (handler == null)
            {
                return;
            }

            var subscribers = handler.GetInvocationList();
            for (int i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    ((Action<Candidate>)subscribers[i])(candidate);
                }
                catch (Exception ex)
                {
                    P2PLogHelper.Debug(LogNode, "候选事件订阅者异常：" + ex.GetType().Name);
                }
            }
        }

        /// <summary>
        /// 逐订阅者触发选路完成事件；单个订阅者异常不影响其余订阅者
        /// </summary>
        /// <param name="result">选路结果</param>
        private void RaiseRouteSelected(IceSessionResult result)
        {
            var handler = RouteSelected;
            if (handler == null)
            {
                return;
            }

            var subscribers = handler.GetInvocationList();
            for (int i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    ((Action<IceSessionResult>)subscribers[i])(result);
                }
                catch (Exception ex)
                {
                    P2PLogHelper.Debug(LogNode, "选路事件订阅者异常：" + ex.GetType().Name);
                }
            }
        }

        /// <summary>
        /// 逐订阅者转发业务帧；单个订阅者异常不影响其余订阅者
        /// </summary>
        /// <param name="frame">已复制的业务载荷</param>
        private void OnFrameReceived(ReadOnlyMemory<byte> frame)
        {
            var handler = FrameReceived;
            if (handler == null)
            {
                return;
            }

            var subscribers = handler.GetInvocationList();
            for (int i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    ((Action<ReadOnlyMemory<byte>>)subscribers[i])(frame);
                }
                catch (Exception ex)
                {
                    P2PLogHelper.Debug(LogNode, "业务帧订阅者异常：" + ex.GetType().Name);
                }
            }
        }

        /// <summary>
        /// 释放单个资源，异常只记日志以免阻断会话释放
        /// </summary>
        /// <param name="disposable">待释放对象，可为 null</param>
        /// <param name="name">对象名称，用于日志</param>
        private static void DisposeQuietly(IDisposable disposable, string name)
        {
            if (disposable == null)
            {
                return;
            }

            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                P2PLogHelper.Debug(LogNode, "释放 " + name + " 异常：" + ex.GetType().Name);
            }
        }

        /// <summary>
        /// 已释放时拒绝启动
        /// </summary>
        /// <exception cref="ObjectDisposedException">会话已释放</exception>
        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}
