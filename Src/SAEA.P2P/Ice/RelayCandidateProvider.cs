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
*文件名： RelayCandidateProvider
*版本号： v26.11.5.1
*唯一标识：5bf1839c-3b5c-4f75-977b-63ca52a939a3
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 20:00:00
*描述：TURN 中继候选与中继数据通道的申请与释放
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 20:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN 中继候选提供器
*
*****************************************************************************/
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Transport;
using SAEA.P2P.Turn;

namespace SAEA.P2P.Ice
{
    /// <summary>
    /// TURN 中继候选提供器：按需申请一次 TURN 分配，把中继端点包装成 ICE relay 候选并配上中继数据通道。
    /// 申请与释放都可重复调用；释放后不再保留分配引用。
    /// </summary>
    public sealed class RelayCandidateProvider : IDisposable
    {
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
        /// 保护分配、通道、候选与释放状态的同步对象
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 当前分配，释放后为 null
        /// </summary>
        private TurnAllocationClient _allocation;

        /// <summary>
        /// 当前中继数据通道，随分配一起创建与释放
        /// </summary>
        private TurnDataChannel _channel;

        /// <summary>
        /// 当前中继候选，释放后为 null
        /// </summary>
        private Candidate _candidate;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 创建中继候选提供器；构造阶段不发任何报文
        /// </summary>
        /// <param name="client">所属 TURN 客户端</param>
        /// <param name="component">候选分量编号，取值 1 到 256</param>
        /// <exception cref="ArgumentNullException">client 为 null</exception>
        /// <exception cref="ArgumentOutOfRangeException">分量编号越界</exception>
        public RelayCandidateProvider(TurnClient client, int component)
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
        }

        /// <summary>
        /// 中继数据通道，未分配或已释放时为 null
        /// </summary>
        public TurnDataChannel Channel
        {
            get
            {
                lock (_sync)
                {
                    return _channel;
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
        /// 申请中继候选并启动通道接收；已分配时直接返回现有候选
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>中继候选</returns>
        /// <exception cref="ObjectDisposedException">本实例已释放</exception>
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
                TurnDataChannel channel = null;
                try
                {
                    var relayed = allocation.RelayedEndPoint;
                    channel = new TurnDataChannel(_client, allocation);
                    channel.StartReceive();

                    // 自动优先级构造函数会按 relay 的类型偏好算出优先级；
                    // 关联地址取分配前的本地映射地址，服务端没给映射时留空
                    var mapped = allocation.MappedEndPoint;
                    var candidate = new Candidate(
                        CandidateType.Relay,
                        IceTransportType.Udp,
                        _component,
                        relayed.Address,
                        relayed.Port,
                        TcpCandidateType.None,
                        mapped != null ? mapped.Address : null,
                        mapped != null ? mapped.Port : 0);

                    lock (_sync)
                    {
                        if (_disposed)
                        {
                            throw new ObjectDisposedException(GetType().Name);
                        }
                        _allocation = allocation;
                        _channel = channel;
                        _candidate = candidate;
                        return candidate;
                    }
                }
                catch
                {
                    // 分配已建立但包装失败时必须显式释放，否则服务端会一直挂着这条分配
                    if (channel != null)
                    {
                        channel.Dispose();
                    }
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
        /// 释放中继分配与通道；未分配或已释放时为空操作
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>释放完成的任务</returns>
        public async Task ReleaseAsync(CancellationToken cancellationToken)
        {
            TurnAllocationClient allocation;
            TurnDataChannel channel;
            lock (_sync)
            {
                allocation = _allocation;
                channel = _channel;
                _allocation = null;
                _channel = null;
                _candidate = null;
            }

            if (allocation == null)
            {
                return;
            }

            // 先摘引用再发 LIFETIME=0：即使等待期间另有调用进入，也不会重复释放
            if (channel != null)
            {
                channel.Dispose();
            }
            await allocation.ReleaseAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 只做本地清理，不同步等待网络；分配的释放需调用方显式调用 <see cref="ReleaseAsync"/>
        /// </summary>
        public void Dispose()
        {
            TurnDataChannel channel;
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
                channel = _channel;
                _channel = null;
                _candidate = null;
                _allocation = null;
            }

            if (channel != null)
            {
                channel.Dispose();
            }
        }
    }
}