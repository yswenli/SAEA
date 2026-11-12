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
*文件名： TcpRelayCandidateProvider
*版本号： v26.11.5.1
*唯一标识：0218b51c-c620-44c2-82c5-a1464c0bbc08
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 17:40:32
*描述：TURN TCP 中继（RFC 6062）候选与数据连接的提供器
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 17:40:32
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN TCP 中继候选提供器
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Common;
using SAEA.P2P.Transport;
using SAEA.P2P.Turn;

namespace SAEA.P2P.Ice
{
    /// <summary>
    /// TURN TCP 中继候选提供器：按需申请一次 TCP 中继分配，把中继端点包装成 ICE relay 候选，
    /// 并按 RFC 6062 的 Connect/ConnectionBind 与 ConnectionAttempt 提供主动与入向两条数据连接。
    /// 申请与释放都可重复调用；释放后不再保留分配引用。
    /// </summary>
    public sealed class TcpRelayCandidateProvider : IDisposable
    {
        /// <summary>
        /// 日志节点名
        /// </summary>
        private const string LogNode = "TcpRelayCandidate";

        /// <summary>
        /// 所属 TURN 客户端
        /// </summary>
        private readonly TurnClient _client;

        /// <summary>
        /// 候选分量编号
        /// </summary>
        private readonly int _component;

        /// <summary>
        /// 串行化申请过程，避免并发调用创建出两个分配
        /// </summary>
        private readonly SemaphoreSlim _allocateGate = new SemaphoreSlim(1, 1);

        /// <summary>
        /// 保护分配、候选、入向通知队列与释放状态的同步对象
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 尚未被取走的入向连接通知
        /// </summary>
        private readonly Queue<TurnConnectionAttempt> _attempts = new Queue<TurnConnectionAttempt>();

        /// <summary>
        /// 当前分配，释放后为 null
        /// </summary>
        private TurnAllocationClient _allocation;

        /// <summary>
        /// 当前中继候选，释放后为 null
        /// </summary>
        private Candidate _candidate;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 创建 TCP 中继候选提供器；构造阶段只订阅入向连接事件，不发任何报文
        /// </summary>
        /// <param name="client">所属 TURN 客户端</param>
        /// <param name="component">候选分量编号，取值 1 到 256</param>
        /// <exception cref="ArgumentNullException">client 为 null</exception>
        /// <exception cref="ArgumentOutOfRangeException">分量编号越界</exception>
        public TcpRelayCandidateProvider(TurnClient client, int component)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }
            if (component < 1 || component > 256)
            {
                throw new ArgumentOutOfRangeException(nameof(component), "分量编号必须在 1 到 256 之间");
            }

            _client = client;
            _component = component;

            // 必须先订阅再申请分配：对端可能在中继端点一出现就连入，晚订阅会丢掉这段窗口内的通知
            _client.ConnectionAttemptReceived += HandleConnectionAttempt;
        }

        /// <summary>
        /// 中继分配，未分配或已释放时为 null
        /// </summary>
        public TurnAllocationClient Allocation
        {
            get
            {
                lock (_sync)
                {
                    return _allocation;
                }
            }
        }

        /// <summary>
        /// 中继候选，未分配或已释放时为 null
        /// </summary>
        public Candidate Candidate
        {
            get
            {
                lock (_sync)
                {
                    return _candidate;
                }
            }
        }

        /// <summary>
        /// 队列中尚未取走的入向连接通知数
        /// </summary>
        public int PendingConnectionAttemptCount
        {
            get
            {
                lock (_sync)
                {
                    return _attempts.Count;
                }
            }
        }

        /// <summary>
        /// 申请 TCP 中继候选；已分配时直接返回现有候选
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>TCP 中继候选</returns>
        /// <exception cref="ObjectDisposedException">本实例已释放</exception>
        /// <exception cref="InvalidOperationException">分配没有返回中继端点</exception>
        /// <exception cref="TurnClientException">服务端拒绝分配或请求超时</exception>
        public async Task<Candidate> AllocateAsync(CancellationToken cancellationToken)
        {
            await _allocateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                lock (_sync)
                {
                    if (_disposed)
                    {
                        throw new ObjectDisposedException(GetType().Name);
                    }
                    if (_candidate != null)
                    {
                        return _candidate;
                    }
                }

                var allocation = await _client.AllocateAsync(null, cancellationToken).ConfigureAwait(false);
                try
                {
                    var relayed = allocation.RelayedEndPoint;
                    if (relayed == null)
                    {
                        throw new InvalidOperationException("TCP 中继分配没有返回中继端点");
                    }

                    // TCP 中继候选由对端主动连入，方向偏好取同时打开；显式优先级构造函数不再推断方向类型
                    var priority = IcePriority.ComputeForTcp(CandidateType.Relay, _component, TcpCandidateType.SimultaneousOpen);
                    var candidate = new Candidate(
                        CandidateType.Relay,
                        IceTransportType.Tcp,
                        _component,
                        relayed.Address,
                        relayed.Port,
                        priority);

                    lock (_sync)
                    {
                        if (_disposed)
                        {
                            throw new ObjectDisposedException(GetType().Name);
                        }
                        _allocation = allocation;
                        _candidate = candidate;
                        P2PLogHelper.Debug(LogNode, "TCP 中继候选：" + candidate.EndPoint);
                        return candidate;
                    }
                }
                catch
                {
                    // 分配已建立但包装失败时必须显式释放，否则服务端会一直挂着这条分配
                    await allocation.ReleaseAsync(CancellationToken.None).ConfigureAwait(false);
                    throw;
                }
            }
            finally
            {
                _allocateGate.Release();
            }
        }

        /// <summary>
        /// 取走最早一条入向连接通知
        /// </summary>
        /// <param name="attempt">取到的通知，队列为空时为 null</param>
        /// <returns>取到通知返回 true</returns>
        public bool TryTakeConnectionAttempt(out TurnConnectionAttempt attempt)
        {
            lock (_sync)
            {
                if (_attempts.Count == 0)
                {
                    attempt = null;
                    return false;
                }

                attempt = _attempts.Dequeue();
                return true;
            }
        }

        /// <summary>
        /// 主动向对端中继端点发起 Connect，并立即用返回的连接号完成 ConnectionBind
        /// </summary>
        /// <param name="peerRelayedEndPoint">对端的中继端点</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>已完成绑定的数据连接，所有权交给调用方</returns>
        /// <exception cref="ArgumentNullException">peerRelayedEndPoint 为 null</exception>
        /// <exception cref="ObjectDisposedException">本实例已释放</exception>
        /// <exception cref="InvalidOperationException">当前分配不是 TCP 中继或控制连接未就绪</exception>
        /// <exception cref="TurnClientException">服务端拒绝、应答应缺少 CONNECTION-ID 或请求超时</exception>
        public async Task<ITcpTransport> ConnectAndBindAsync(IPEndPoint peerRelayedEndPoint, CancellationToken cancellationToken)
        {
            if (peerRelayedEndPoint == null)
            {
                throw new ArgumentNullException(nameof(peerRelayedEndPoint));
            }
            ThrowIfDisposed();

            var connectionId = await _client.ConnectAsync(peerRelayedEndPoint, cancellationToken).ConfigureAwait(false);
            return await _client.ConnectionBindAsync(connectionId, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 对一条入向连接通知完成 ConnectionBind
        /// </summary>
        /// <param name="attempt">入向连接通知</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>已完成绑定的数据连接，所有权交给调用方</returns>
        /// <exception cref="ArgumentNullException">attempt 为 null</exception>
        /// <exception cref="ObjectDisposedException">本实例已释放</exception>
        /// <exception cref="InvalidOperationException">当前分配不是 TCP 中继或控制连接未就绪</exception>
        /// <exception cref="TurnClientException">服务端拒绝、应答无法解析或鉴权失败</exception>
        public async Task<ITcpTransport> BindInboundAsync(TurnConnectionAttempt attempt, CancellationToken cancellationToken)
        {
            if (attempt == null)
            {
                throw new ArgumentNullException(nameof(attempt));
            }
            ThrowIfDisposed();

            return await _client.ConnectionBindAsync(attempt.ConnectionId, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 释放中继分配并清空入向通知；未分配或已释放时为空操作，可重复调用
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>释放完成的任务</returns>
        /// <exception cref="TurnClientException">服务端拒绝或请求超时</exception>
        public async Task ReleaseAsync(CancellationToken cancellationToken)
        {
            TurnAllocationClient allocation;
            lock (_sync)
            {
                allocation = _allocation;
                _allocation = null;
                _candidate = null;
                _attempts.Clear();
            }

            if (allocation == null)
            {
                return;
            }

            // 先摘引用再发 LIFETIME=0：即使等待期间另有调用进入，也不会重复释放
            await allocation.ReleaseAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 只做本地清理，不同步等待网络；分配的释放需调用方显式调用 <see cref="ReleaseAsync"/>
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
                _allocation = null;
                _candidate = null;
                _attempts.Clear();
            }

            try
            {
                _client.ConnectionAttemptReceived -= HandleConnectionAttempt;
            }
            catch (Exception ex)
            {
                P2PLogHelper.Debug(LogNode, "退订入向连接事件失败：" + ex.GetType().Name);
            }
        }

        /// <summary>
        /// 入向连接通知回调：入队等调用方取走；已释放时直接丢弃
        /// </summary>
        /// <param name="attempt">连接尝试</param>
        private void HandleConnectionAttempt(TurnConnectionAttempt attempt)
        {
            if (attempt == null)
            {
                return;
            }

            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }
                _attempts.Enqueue(attempt);
            }

            P2PLogHelper.Debug(LogNode, "收到入向连接通知：" + attempt.PeerEndPoint);
        }

        /// <summary>
        /// 已释放时抛异常
        /// </summary>
        /// <exception cref="ObjectDisposedException">本实例已释放</exception>
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