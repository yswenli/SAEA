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
*命名空间：SAEA.P2P.Tests.Ice
*文件名： MatrixHost
*版本号： net10.0
*唯一标识：7a6faa7f-a0b9-4189-b5a9-552e368bfdce
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 11:11:14
*描述：ICE 矩阵测试中单个虚拟主机的状态与操作封装
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 11:46:53
*修改人： yswenli
*版本号： net10.0
*描述：ArgumentException 改为中文提示语，参数名仍作为 paramName
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Ice;
using SAEA.P2P.Stun;
using SAEA.P2P.Tests.Nat;
using SAEA.P2P.Transport;
using SAEA.P2P.Turn;

namespace SAEA.P2P.Tests.Ice
{
    /// <summary>
    /// 矩阵测试里的一个虚拟主机：持有 NAT 后的基线传输、TURN 控制传输、候选、检查表与被动应答器。
    /// </summary>
    public sealed class MatrixHost : IDisposable
    {
        /// <summary>
        /// ICE 基线传输，同时用于 STUN 收集与直连检查
        /// </summary>
        private readonly MappedUdpTransport _baseTransport;

        /// <summary>
        /// TURN 客户端独占的控制传输
        /// </summary>
        private readonly MappedUdpTransport _turnTransport;

        /// <summary>
        /// TURN 客户端传输适配器，复用 _turnTransport
        /// </summary>
        private readonly UdpTurnClientTransport _turnTransportForTurn;

        /// <summary>
        /// STUN 服务端点
        /// </summary>
        private readonly IPEndPoint _stunEndPoint;

        /// <summary>
        /// TURN 服务端点
        /// </summary>
        private readonly IPEndPoint _turnEndPoint;

        /// <summary>
        /// 期望的外部映射地址
        /// </summary>
        private readonly IPAddress _expectedExternalAddress;

        /// <summary>
        /// TURN 长凭据
        /// </summary>
        private readonly TurnCredential _turnCredential;

        /// <summary>
        /// 对端候选与本地候选组成的检查表
        /// </summary>
        private readonly CheckList _checkList;

        /// <summary>
        /// 已登记的本地候选，随 prflx 候选增长；读取方必须持 _sync
        /// </summary>
        private readonly List<Candidate> _localCandidates = new List<Candidate>();

        /// <summary>
        /// 已登记的远端候选，随 prflx 候选增长；读取方必须持 _sync
        /// </summary>
        private readonly List<Candidate> _remoteCandidates = new List<Candidate>();

        /// <summary>
        /// 保护候选列表与检查表的锁：接收线程上的 prflx 登记与测试线程的枚举/检查会并发
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 连通性检查器
        /// </summary>
        private readonly ConnectivityCheck _check;

        /// <summary>
        /// TURN 客户端，GatherAsync 中创建
        /// </summary>
        private TurnClient _turnClient;

        /// <summary>
        /// 中继候选提供器，GatherAsync 中创建
        /// </summary>
        private RelayCandidateProvider _relayProvider;

        /// <summary>
        /// 提名器，首次提名时创建
        /// </summary>
        private Nominator _nominator;

        /// <summary>
        /// 是否已完成候选收集
        /// </summary>
        private bool _gathered;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 建立 NAT 后的两套传输与检查表；构造阶段不做 STUN/TURN 交互
        /// </summary>
        /// <param name="internalAddress">NAT 内部地址</param>
        /// <param name="natGateway">NAT 网关端点</param>
        /// <param name="expectedExternalAddress">期望的外部映射地址</param>
        /// <param name="stunEndPoint">STUN 服务端点</param>
        /// <param name="turnEndPoint">TURN 服务端点</param>
        /// <param name="turnCredential">TURN 长凭据</param>
        /// <param name="isControlling">是否控制方</param>
        /// <param name="localUfrag">本地 ufrag</param>
        /// <param name="localPassword">本地短凭据口令</param>
        /// <exception cref="ArgumentNullException">任一引用参数为 null</exception>
        /// <exception cref="ArgumentException">localUfrag 或 localPassword 为 null 或空字符串</exception>
        public MatrixHost(IPAddress internalAddress, IPEndPoint natGateway, IPAddress expectedExternalAddress,
            IPEndPoint stunEndPoint, IPEndPoint turnEndPoint, TurnCredential turnCredential,
            bool isControlling, string localUfrag, string localPassword)
        {
            if (internalAddress == null) throw new ArgumentNullException(nameof(internalAddress));
            if (natGateway == null) throw new ArgumentNullException(nameof(natGateway));
            if (expectedExternalAddress == null) throw new ArgumentNullException(nameof(expectedExternalAddress));
            if (stunEndPoint == null) throw new ArgumentNullException(nameof(stunEndPoint));
            if (turnEndPoint == null) throw new ArgumentNullException(nameof(turnEndPoint));
            if (turnCredential == null) throw new ArgumentNullException(nameof(turnCredential));

            // 口令只用于校验，异常消息里不出现具体值
            if (string.IsNullOrEmpty(localUfrag)) throw new ArgumentException("ufrag 不能为空", nameof(localUfrag));
            if (string.IsNullOrEmpty(localPassword)) throw new ArgumentException("密码不能为空", nameof(localPassword));

            _stunEndPoint = stunEndPoint;
            _turnEndPoint = turnEndPoint;
            _expectedExternalAddress = expectedExternalAddress;
            _turnCredential = turnCredential;

            IsControlling = isControlling;
            LocalUfrag = localUfrag;
            LocalPassword = localPassword;

            _baseTransport = new MappedUdpTransport(new IPEndPoint(internalAddress, 0), natGateway);

            // TURN 客户端的读泵会把落到同一 socket 上的 ICE 检查响应当成 TURN 帧吞掉，必须另开一个 socket
            _turnTransport = new MappedUdpTransport(new IPEndPoint(internalAddress, 0), natGateway);
            _turnTransportForTurn = new UdpTurnClientTransport(_turnTransport, _turnEndPoint);

            InternalEndPoint = _baseTransport.LocalEndPoint;
            _checkList = new CheckList(isControlling);
            _check = new ConnectivityCheck(localUfrag, localPassword);
        }

        /// <summary>
        /// NAT 内部端点
        /// </summary>
        public IPEndPoint InternalEndPoint { get; private set; }

        /// <summary>
        /// 本地 ufrag
        /// </summary>
        public string LocalUfrag { get; private set; }

        /// <summary>
        /// 本地短凭据口令
        /// </summary>
        public string LocalPassword { get; private set; }

        /// <summary>
        /// 是否控制方
        /// </summary>
        public bool IsControlling { get; private set; }

        /// <summary>
        /// 服务端反射候选，收集后可用
        /// </summary>
        public Candidate SrflxCandidate { get; private set; }

        /// <summary>
        /// 中继候选，收集后可用
        /// </summary>
        public Candidate RelayCandidate { get; private set; }

        /// <summary>
        /// 本机对外公布的候选，顺序固定为 srflx、relay
        /// </summary>
        public IReadOnlyList<Candidate> Candidates
        {
            get
            {
                var candidates = new List<Candidate>(2);
                if (SrflxCandidate != null) candidates.Add(SrflxCandidate);
                if (RelayCandidate != null) candidates.Add(RelayCandidate);
                return candidates;
            }
        }

        /// <summary>
        /// 本机的检查表
        /// </summary>
        public CheckList CheckList
        {
            get { return _checkList; }
        }

        /// <summary>
        /// 被动侧应答器，收集后可用；计数由测试直接读取
        /// </summary>
        public MatrixIceResponder Responder { get; private set; }

        /// <summary>
        /// 已选中的候选对
        /// </summary>
        public SelectedPair SelectedPair { get; private set; }

        /// <summary>
        /// 收集 srflx 与 relay 候选并启动应答器，可重复调用
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>收集完成的任务</returns>
        /// <exception cref="InvalidOperationException">STUN 收集失败或映射地址与期望不符</exception>
        public async Task GatherAsync(CancellationToken cancellationToken)
        {
            if (_gathered) return;

            IPEndPoint mapped;
            // StunClient 的读订阅必须在此释放，否则会抢走后续 ICE 检查的响应报文
            using (var stun = new StunClient(_baseTransport))
            {
                var result = await stun.BindingAsync(_stunEndPoint, cancellationToken).ConfigureAwait(false);
                if (!result.Succeeded || result.MappedAddress == null)
                {
                    throw new InvalidOperationException("STUN 收集失败：" + result.ErrorReason);
                }

                mapped = result.MappedAddress;
                if (!mapped.Address.Equals(_expectedExternalAddress))
                {
                    throw new InvalidOperationException(
                        "STUN 映射地址与期望不符，实际 " + mapped.Address + "，期望 " + _expectedExternalAddress);
                }
            }

            SrflxCandidate = new Candidate(CandidateType.ServerReflexive, IceTransportType.Udp, 1,
                mapped.Address, mapped.Port, IcePriority.ComputeForUdp(CandidateType.ServerReflexive, 1));

            var options = new TurnClientOptions
            {
                ServerEndPoint = _turnEndPoint,
                Credential = _turnCredential,
                TransportFactory = () => _turnTransportForTurn
            };
            _turnClient = new TurnClient(options);
            _relayProvider = new RelayCandidateProvider(_turnClient, 1);
            RelayCandidate = await _relayProvider.AllocateAsync(cancellationToken).ConfigureAwait(false);
            if (RelayCandidate == null)
            {
                throw new InvalidOperationException("TURN 分配未返回中继候选");
            }

            Responder = new MatrixIceResponder(new IUdpTransport[] { _baseTransport, _relayProvider.Channel }, LocalPassword);
            Responder.CheckRequestReceived += OnCheckRequestReceived;
            Responder.Start();

            _gathered = true;
        }

        /// <summary>
        /// 登记本机候选：本地列表与检查表必须同步更新，prflx 候选也走同一入口
        /// </summary>
        /// <param name="localCandidates">本端候选集合</param>
        /// <exception cref="ArgumentNullException">localCandidates 为 null</exception>
        public void AddLocalCandidates(IEnumerable<Candidate> localCandidates)
        {
            if (localCandidates == null) throw new ArgumentNullException(nameof(localCandidates));

            lock (_sync)
            {
                foreach (var candidate in localCandidates)
                {
                    if (candidate == null) continue;
                    _localCandidates.Add(candidate);
                    _checkList.AddLocalCandidate(candidate);
                }
            }
        }

        /// <summary>
        /// 把对端候选加入检查表
        /// </summary>
        /// <param name="remoteCandidates">对端候选集合</param>
        /// <exception cref="ArgumentNullException">remoteCandidates 为 null</exception>
        public void AddRemoteCandidates(IEnumerable<Candidate> remoteCandidates)
        {
            if (remoteCandidates == null) throw new ArgumentNullException(nameof(remoteCandidates));

            lock (_sync)
            {
                foreach (var candidate in remoteCandidates)
                {
                    if (candidate == null) continue;
                    _remoteCandidates.Add(candidate);
                    _checkList.AddRemoteCandidate(candidate);
                }
            }
        }

        /// <summary>
        /// 取候选对快照；检查在多线程并发进行，期间可能有 prflx 配对插入，快照避免枚举时集合被修改
        /// </summary>
        /// <returns>候选对数组，随时可能过期的副本</returns>
        public CandidatePair[] SnapshotPairs()
        {
            lock (_sync)
            {
                return _checkList.Pairs.ToArray();
            }
        }

        /// <summary>
        /// 取候选对应的传输：中继候选走 TURN 数据通道，其余（含 prflx）走基线传输
        /// </summary>
        /// <param name="local">本地候选</param>
        /// <returns>承载该候选的传输</returns>
        /// <exception cref="ArgumentNullException">local 为 null</exception>
        public IUdpTransport TransportFor(Candidate local)
        {
            if (local == null) throw new ArgumentNullException(nameof(local));

            // prflx 描述的是基线 socket 被对端看到的地址，报文仍从基线 socket 发出
            return local.Type == CandidateType.Relay ? (IUdpTransport)_relayProvider.Channel : _baseTransport;
        }

        /// <summary>
        /// 取检查表中对端候选的地址去重结果，用作 TURN 权限预置的目标
        /// </summary>
        /// <returns>对端地址列表，无候选对时为空数组</returns>
        public IReadOnlyList<IPAddress> GetRemoteAddresses()
        {
            var pairs = SnapshotPairs();
            if (pairs.Length == 0)
            {
                return Array.Empty<IPAddress>();
            }

            var addresses = new List<IPAddress>(pairs.Length);
            for (var i = 0; i < pairs.Length; i++)
            {
                var remote = pairs[i].Remote;
                if (remote == null || remote.Address == null) continue;
                addresses.Add(remote.Address);
            }

            return addresses.Distinct().ToList();
        }

        /// <summary>
        /// 对指定候选对发起一次连通性检查并推进其状态
        /// </summary>
        /// <param name="pair">候选对</param>
        /// <param name="remoteUfrag">对端 ufrag</param>
        /// <param name="remotePassword">对端口令</param>
        /// <param name="tiebreaker">角色冲突裁决值</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>检查成功返回 true</returns>
        /// <exception cref="ArgumentNullException">pair 为 null</exception>
        /// <exception cref="InvalidOperationException">检查出现角色冲突</exception>
        public async Task<bool> CheckPairAsync(CandidatePair pair, string remoteUfrag, string remotePassword,
            ulong tiebreaker, CancellationToken cancellationToken)
        {
            if (pair == null) throw new ArgumentNullException(nameof(pair));
            if (pair.State == PairState.Succeeded) return true;

            if (pair.State == PairState.Frozen) pair.MarkWaiting();
            else if (pair.State == PairState.Failed) pair.ResetToWaiting();
            pair.MarkInProgress();

            var permission = pair.Local.Type == CandidateType.Relay
                ? (ITurnRelayPermission)_relayProvider.Channel
                : null;
            var remoteAddresses = permission == null ? null : GetRemoteAddresses();

            ConnectivityCheckResult result;
            try
            {
                result = await _check.CheckAsync(TransportFor(pair.Local), pair.Remote.EndPoint, remoteUfrag,
                    remotePassword, IsControlling ? IceRole.Controlling : IceRole.Controlled, tiebreaker,
                    _check.ComputePeerReflexivePriority(pair.Local.Component), false, permission, remoteAddresses,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 检查默认事务上限 40s，矩阵用 2s 上限压时，取消视为该轮失败，不能让 pair 停在 InProgress
                if (pair.State != PairState.Succeeded) pair.MarkFailed();
                return false;
            }

            if (result.Succeeded)
            {
                pair.MarkSucceeded();
                pair.MarkValid();

                if (result.MappedAddress != null)
                {
                    lock (_sync)
                    {
                        // 对称 NAT 侧自己的外部映射只有响应的 XOR-MAPPED-ADDRESS 才暴露，必须建本地 prflx 才能与对端视角的 pair 对上
                        var prflx = Nominator.CreatePeerReflexiveFromResponse(pair, result.MappedAddress,
                            pair.Local.Priority, _localCandidates);
                        if (prflx != null)
                        {
                            _localCandidates.Add(prflx);
                            _checkList.AddLocalCandidate(prflx);
                        }
                    }
                }

                return true;
            }

            // 矩阵里角色固定，收到 487 说明前提不成立，直接暴露而不是静默换角色
            if (result.RoleConflict)
            {
                throw new InvalidOperationException("检查出现角色冲突，矩阵不接受 487 收敛路径");
            }

            pair.MarkFailed();
            return false;
        }

        /// <summary>
        /// 作为控制方对候选对发起提名
        /// </summary>
        /// <param name="pair">候选对</param>
        /// <param name="remoteUfrag">对端 ufrag</param>
        /// <param name="remotePassword">对端口令</param>
        /// <param name="tiebreaker">角色冲突裁决值</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>提名成功返回 true；被动侧固定返回 false</returns>
        /// <exception cref="ArgumentNullException">pair 为 null</exception>
        public async Task<bool> NominateAsync(CandidatePair pair, string remoteUfrag, string remotePassword,
            ulong tiebreaker, CancellationToken cancellationToken)
        {
            if (pair == null) throw new ArgumentNullException(nameof(pair));

            // 被动侧不提名，只应答控制方的 USE-CANDIDATE
            if (!IsControlling) return false;

            if (_nominator == null)
            {
                _nominator = new Nominator(IceRole.Controlling, tiebreaker, LocalUfrag, LocalPassword,
                    remoteUfrag, remotePassword, _checkList);
            }

            var permission = pair.Local.Type == CandidateType.Relay
                ? (ITurnRelayPermission)_relayProvider.Channel
                : null;
            var remoteAddresses = permission == null ? null : GetRemoteAddresses();

            var ok = await _nominator.NominateAsync(TransportFor(pair.Local), pair, permission, remoteAddresses,
                cancellationToken).ConfigureAwait(false);
            if (ok)
            {
                SelectedPair = _nominator.SelectedPair;
            }

            return ok;
        }

        /// <summary>
        /// 被动侧在已成功的候选对上直接发布选中对，用于驱动数据面
        /// </summary>
        /// <param name="pair">已成功的候选对</param>
        /// <exception cref="ArgumentNullException">pair 为 null</exception>
        public void MarkSelected(CandidatePair pair)
        {
            if (pair == null) throw new ArgumentNullException(nameof(pair));

            // 测试用被动侧不跑提名流程，直接在已成功对上打提名标记以产出选中对
            pair.MarkNominated();
            SelectedPair = new SelectedPair(pair);
        }

        /// <summary>
        /// 收到对端检查请求时按来源地址建远端 prflx 候选；在接收线程上同步执行
        /// </summary>
        /// <param name="transport">收到请求的传输</param>
        /// <param name="source">请求来源端点</param>
        /// <param name="priority">请求 PRIORITY 属性的值</param>
        private void OnCheckRequestReceived(IUdpTransport transport, IPEndPoint source, uint priority)
        {
            if (source == null) return;

            lock (_sync)
            {
                // 分量编号跟随已登记的本端候选；首个请求可能早于候选登记，此时按 UDP 默认分量 1 处理
                var component = 1;
                var pairs = _checkList.Pairs;
                if (pairs != null && pairs.Count > 0 && pairs[0].Local != null)
                {
                    component = pairs[0].Local.Component;
                }

                // 只按来源地址建远端 prflx：对称 NAT 侧的真实可用端点只有对端检查包的来源才暴露出来
                var prflx = Nominator.CreatePeerReflexiveFromRequest(source, priority, component,
                    IceTransportType.Udp, _remoteCandidates);
                if (prflx == null) return;
                _remoteCandidates.Add(prflx);
                _checkList.AddRemoteCandidate(prflx);
            }
        }

        /// <summary>
        /// 释放 TURN 分配并释放全部资源，可重复调用
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>释放完成的任务</returns>
        public async Task ReleaseAsync(CancellationToken cancellationToken)
        {
            if (_disposed) return;

            if (_relayProvider != null)
            {
                // 释放异常交给调用方判定是否影响用例结论，这里不吞
                await _relayProvider.ReleaseAsync(cancellationToken).ConfigureAwait(false);
            }

            Dispose();
        }

        /// <summary>
        /// 释放全部资源，可重复调用
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (Responder != null)
            {
                // 先退订再释放，避免释放过程中的报文继续走 prflx 登记
                Responder.CheckRequestReceived -= OnCheckRequestReceived;
                Responder.Dispose();
            }
            if (_nominator != null) _nominator.Dispose();
            if (_check != null) _check.Dispose();
            if (_relayProvider != null) _relayProvider.Dispose();
            if (_turnClient != null) _turnClient.Dispose();

            // TURN 客户端只复用外部传入的传输、不持有其所有权（_ownsTransport 为 false），由本类释放；
            // 两个 MappedUdpTransport 的 Dispose 均幂等，重复调用不会二次释放底层 socket
            _turnTransportForTurn.Dispose();
            _turnTransport.Dispose();
            _baseTransport.Dispose();
        }
    }
}