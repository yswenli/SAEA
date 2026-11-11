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
*文件名： CandidateGatherer
*版本号： v26.11.5.1
*唯一标识：c1a6f0d5-7b3e-4a82-9d64-2f8e5b7c9a10
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 13:00:00
*描述：ICE 候选收集（host 与 srflx）
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 13:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 ICE 候选收集（host 与 srflx）
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Common;
using SAEA.P2P.Stun;
using SAEA.P2P.Transport;

namespace SAEA.P2P.Ice
{
    /// <summary>
    /// ICE 候选收集器：为本机每个可用地址绑定一个 UDP 传输并产出 host 候选，
    /// 再按地址族向 STUN 服务器申请 srflx 候选；候选逐条抛出，收集结束时触发一次结束事件。
    /// 收集到的传输归上层会话复用，本类在释放时一并关闭。
    /// </summary>
    public sealed class CandidateGatherer : IDisposable
    {
        /// <summary>
        /// 日志节点标识
        /// </summary>
        private const string LogNode = "CandidateGatherer";

        /// <summary>
        /// 收集配置
        /// </summary>
        private readonly IceOptions _options;

        /// <summary>
        /// 本机地址提供者
        /// </summary>
        private readonly HostCandidateProvider _provider;

        /// <summary>
        /// 保护候选与传输列表以及释放状态
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 已发现的候选，按发现顺序
        /// </summary>
        private readonly List<Candidate> _candidates = new List<Candidate>();

        /// <summary>
        /// 已绑定的传输，每个本地端口一个
        /// </summary>
        private readonly List<IUdpTransport> _transports = new List<IUdpTransport>();

        /// <summary>
        /// 是否已开始收集，用于拒绝重复调用
        /// </summary>
        private int _gatherStarted;

        /// <summary>
        /// 结束事件是否已触发，保证只触发一次
        /// </summary>
        private int _endOfCandidatesRaised;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 用给定配置创建候选收集器
        /// </summary>
        /// <param name="options">收集配置，非空；收集期间读取其当前值</param>
        /// <exception cref="ArgumentNullException">配置为空</exception>
        public CandidateGatherer(IceOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _options = options;
            _provider = new HostCandidateProvider(options);
        }

        /// <summary>
        /// 发现一个候选时触发，用于 trickle 逐条把候选交给对端
        /// </summary>
        public event Action<Candidate> OnCandidateDiscovered;

        /// <summary>
        /// 收集结束时触发一次，含预算到期与外部取消；之后不会再有候选事件
        /// </summary>
        public event Action EndOfCandidates;

        /// <summary>
        /// 已发现的候选，按发现顺序返回快照
        /// </summary>
        public IReadOnlyList<Candidate> Candidates
        {
            get
            {
                lock (_sync)
                {
                    return _candidates.ToArray();
                }
            }
        }

        /// <summary>
        /// 已绑定的传输，每个本地端口一个，供上层会话复用；释放后为空
        /// </summary>
        public IReadOnlyList<IUdpTransport> Transports
        {
            get
            {
                lock (_sync)
                {
                    return _transports.ToArray();
                }
            }
        }

        /// <summary>
        /// 执行一轮候选收集：先产出 host 候选，再补充 srflx 候选
        /// </summary>
        /// <param name="cancellationToken">调用方取消令牌；取消时先触发结束事件再抛出取消异常</param>
        /// <returns>收集结束的任务；预算到期按正常完成，不外抛取消异常</returns>
        /// <exception cref="ObjectDisposedException">收集器已释放</exception>
        /// <exception cref="InvalidOperationException">已调用过，收集只能执行一次</exception>
        public async Task GatherAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (Interlocked.CompareExchange(ref _gatherStarted, 1, 0) != 0)
            {
                throw new InvalidOperationException("候选收集只能执行一次。");
            }

            var timeout = _options.GatheringTimeout;

            // 预算到期只结束本轮收集，与调用方主动取消区分；联合令牌让内部等待同时响应两者
            using (var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                if (timeout > TimeSpan.Zero)
                {
                    budget.CancelAfter(timeout);
                }
                else if (timeout != Timeout.InfiniteTimeSpan)
                {
                    // 0 或负数预算视为已到期，直接结束本轮收集
                    budget.Cancel();
                }

                try
                {
                    await CollectAsync(budget.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // 预算到期：停止收集并保留已有候选，按正常结束处理
                }
                finally
                {
                    RaiseEndOfCandidates();
                }
            }
        }

        /// <summary>
        /// 释放全部传输并清空候选与传输列表；可重复调用
        /// </summary>
        public void Dispose()
        {
            IUdpTransport[] transports;
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;

                transports = _transports.ToArray();
                _transports.Clear();
                _candidates.Clear();
            }

            for (int i = 0; i < transports.Length; i++)
            {
                // 单个传输释放失败不能阻断其余传输
                DisposeTransport(transports[i]);
            }
        }

        /// <summary>
        /// 两阶段收集：先按地址绑定传输并产出 host 候选，再对每个传输申请 srflx 候选
        /// </summary>
        /// <param name="token">联合取消令牌</param>
        /// <returns>收集结束的任务</returns>
        private async Task CollectAsync(CancellationToken token)
        {
            var addresses = _provider.GetAddresses();
            if (addresses == null || addresses.Count == 0)
            {
                return;
            }

            var component = _options.Component;

            for (int i = 0; i < addresses.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                if (IsDisposed())
                {
                    return;
                }

                var address = addresses[i];
                if (address == null)
                {
                    continue;
                }

                IUdpTransport transport = null;
                try
                {
                    transport = CreateTransport();
                    if (transport == null)
                    {
                        P2PLogHelper.Warning(LogNode, "传输工厂返回空实现，跳过地址 " + address);
                        continue;
                    }

                    await transport.BindAsync(new IPEndPoint(address, 0), token).ConfigureAwait(false);
                    transport.StartReceive();
                }
                catch (OperationCanceledException)
                {
                    // 取消时尚未入表的传输由本方法负责释放，入表后的交给 Dispose
                    DisposeTransport(transport);
                    throw;
                }
                catch (Exception ex)
                {
                    // 单个地址绑定失败只跳过该地址，不影响其余地址的收集
                    P2PLogHelper.Warning(LogNode, "绑定地址 " + address + " 失败：" + ex.Message);
                    DisposeTransport(transport);
                    continue;
                }

                var local = transport.LocalEndPoint;
                if (local == null || local.Address == null)
                {
                    P2PLogHelper.Warning(LogNode, "地址 " + address + " 绑定后没有本地端点，跳过");
                    DisposeTransport(transport);
                    continue;
                }

                // host 候选必须用 socket 的实际绑定地址，枚举值可能与绑定地址不一致
                if (!TryAddTransport(transport))
                {
                    DisposeTransport(transport);
                    return;
                }

                AddCandidate(new Candidate(CandidateType.Host, IceTransportType.Udp, component, local.Address, local.Port, TcpCandidateType.None, null, 0));
            }

            var servers = _options.StunServers;
            if (servers == null || servers.Count == 0)
            {
                return;
            }

            IUdpTransport[] transports;
            lock (_sync)
            {
                transports = _transports.ToArray();
            }

            for (int i = 0; i < transports.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                await GatherServerReflexiveAsync(transports[i], component, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 对单个传输按地址族依次尝试 STUN 服务器，取第一个成功结果产出 srflx 候选
        /// </summary>
        /// <param name="transport">已绑定的传输</param>
        /// <param name="component">分量编号</param>
        /// <param name="token">联合取消令牌</param>
        /// <returns>尝试结束的任务；全部失败时不产出候选</returns>
        private async Task GatherServerReflexiveAsync(IUdpTransport transport, int component, CancellationToken token)
        {
            var local = transport.LocalEndPoint;
            if (local == null || local.Address == null)
            {
                return;
            }

            var servers = _options.StunServers;
            for (int i = 0; i < servers.Count; i++)
            {
                token.ThrowIfCancellationRequested();

                var server = servers[i];
                if (server == null || server.Address == null)
                {
                    continue;
                }
                // 不同地址族的端点走不通，跳过而不是浪费一次超时
                if (server.AddressFamily != local.AddressFamily)
                {
                    continue;
                }

                StunBindingResult result;
                try
                {
                    using (var client = new StunClient(transport))
                    {
                        result = await client.BindingAsync(server, token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    P2PLogHelper.Debug(LogNode, "STUN 服务器 " + server + " 请求异常：" + ex.Message);
                    continue;
                }

                if (result == null || !result.Succeeded || result.MappedAddress == null || result.MappedAddress.Address == null)
                {
                    P2PLogHelper.Debug(LogNode, "STUN 服务器 " + server + " 未返回映射地址");
                    continue;
                }

                var mapped = result.MappedAddress;

                // v4-mapped 与对应 IPv4 视为同一地址，进候选表前归一化
                var address = mapped.Address.IsIPv4MappedToIPv6 ? mapped.Address.MapToIPv4() : mapped.Address;
                AddCandidate(new Candidate(CandidateType.ServerReflexive, IceTransportType.Udp, component, address, mapped.Port, TcpCandidateType.None, local.Address, local.Port));

                // 该传输已拿到映射地址，不再尝试后续服务器
                return;
            }
        }

        /// <summary>
        /// 按配置创建传输
        /// </summary>
        /// <returns>未绑定的传输实例</returns>
        private IUdpTransport CreateTransport()
        {
            var factory = _options.TransportFactory;
            return factory != null ? factory() : new SaeaUdpTransport();
        }

        /// <summary>
        /// 把传输加入复用列表；已释放时返回 false 由调用方自行释放
        /// </summary>
        /// <param name="transport">已绑定的传输</param>
        /// <returns>入表成功返回 true</returns>
        private bool TryAddTransport(IUdpTransport transport)
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return false;
                }
                _transports.Add(transport);
                return true;
            }
        }

        /// <summary>
        /// 记录候选并触发发现事件；已释放时丢弃
        /// </summary>
        /// <param name="candidate">新候选</param>
        private void AddCandidate(Candidate candidate)
        {
            if (candidate == null)
            {
                return;
            }

            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }
                _candidates.Add(candidate);
            }

            RaiseCandidateDiscovered(candidate);
        }

        /// <summary>
        /// 逐订阅者触发候选发现事件；单个订阅者异常不影响其余订阅者
        /// </summary>
        /// <param name="candidate">新候选</param>
        private void RaiseCandidateDiscovered(Candidate candidate)
        {
            var handler = OnCandidateDiscovered;
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
        /// 触发结束事件，仅第一次调用生效
        /// </summary>
        private void RaiseEndOfCandidates()
        {
            if (Interlocked.Exchange(ref _endOfCandidatesRaised, 1) != 0)
            {
                return;
            }

            var handler = EndOfCandidates;
            if (handler == null)
            {
                return;
            }

            var subscribers = handler.GetInvocationList();
            for (int i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    ((Action)subscribers[i])();
                }
                catch (Exception ex)
                {
                    P2PLogHelper.Debug(LogNode, "结束事件订阅者异常：" + ex.GetType().Name);
                }
            }
        }

        /// <summary>
        /// 释放单个传输，异常只记日志
        /// </summary>
        /// <param name="transport">待释放的传输，可为空</param>
        private static void DisposeTransport(IUdpTransport transport)
        {
            if (transport == null)
            {
                return;
            }

            try
            {
                transport.Dispose();
            }
            catch (Exception ex)
            {
                P2PLogHelper.Debug(LogNode, "释放传输异常：" + ex.GetType().Name);
            }
        }

        /// <summary>
        /// 读取释放状态
        /// </summary>
        /// <returns>已释放返回 true</returns>
        private bool IsDisposed()
        {
            lock (_sync)
            {
                return _disposed;
            }
        }

        /// <summary>
        /// 已释放时拒绝新的收集
        /// </summary>
        private void ThrowIfDisposed()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }
            }
        }
    }
}