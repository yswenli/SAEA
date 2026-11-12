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
*修改时间：2026/11/12 15:00:08
*修改人： yswenli
*版本号： v26.11.5.1
*描述：候选对状态迁移补齐解冻与检查中步骤
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

                default:
                    return "TURN 中继尚未接入";
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
        /// 按本地候选端点找到对应的 UDP 传输，srflx 候选回退到其基地址与端口
        /// </summary>
        /// <param name="local">本地候选</param>
        /// <returns>匹配的传输；没有匹配时返回 null</returns>
        private IUdpTransport FindUdpTransport(Candidate local)
        {
            if (local == null)
            {
                return null;
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
                        && candidate.Type == CandidateType.Host
                        && candidate.EndPoint.Equals(transport.LocalEndPoint))
                    {
                        local = candidate;
                        break;
                    }
                }
            }

            if (local == null)
            {
                P2PLogHelper.Debug(LogNode, "入向检查请求找不到本地 host 候选：" + transport.LocalEndPoint);
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
