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
*文件名： TcpCandidateGatherer
*版本号： v26.11.5.1
*唯一标识：084c6db1-73de-4e5d-87bf-03bbe4cb6d2b
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 12:00:19
*描述：TCP 候选收集（host/passive 与 host/so）
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 12:00:19
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TCP 候选收集
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Common;
using SAEA.P2P.Transport;

namespace SAEA.P2P.Ice
{
    /// <summary>
    /// TCP 候选收集器：为本机每个可用地址绑定被动监听并产出 host/passive 候选，
    /// 平台支持时再为同一地址绑定一个端口不同的监听产出 host/so 候选。
    /// 监听句柄经 <see cref="Listeners"/> 交给上层直连层复用，本类释放时一并关闭。
    /// </summary>
    public sealed class TcpCandidateGatherer : IDisposable
    {
        /// <summary>
        /// 日志节点标识
        /// </summary>
        private const string LogNode = "TcpCandidateGatherer";

        /// <summary>
        /// 收集配置
        /// </summary>
        private readonly IceOptions _options;

        /// <summary>
        /// 本机地址提供者
        /// </summary>
        private readonly HostCandidateProvider _provider;

        /// <summary>
        /// 保护候选与监听列表以及释放状态
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 已发现的候选，按发现顺序
        /// </summary>
        private readonly List<Candidate> _candidates = new List<Candidate>();

        /// <summary>
        /// 已绑定的监听，每个本地端口一个
        /// </summary>
        private readonly List<ITcpTransport> _listeners = new List<ITcpTransport>();

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
        /// 用给定配置创建 TCP 候选收集器
        /// </summary>
        /// <param name="options">收集配置，非空；收集期间读取其当前值</param>
        /// <exception cref="ArgumentNullException">配置为空</exception>
        public TcpCandidateGatherer(IceOptions options)
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
        /// 收集结束时触发一次；之后不会再有候选事件
        /// </summary>
        public event Action EndOfCandidates;

        /// <summary>
        /// 当前平台是否支持 TCP 同时打开；Windows 的 TCP 栈不支持该模式
        /// </summary>
        public static bool IsSimultaneousOpenSupported
        {
            get { return !RuntimeInformation.IsOSPlatform(OSPlatform.Windows); }
        }

        /// <summary>
        /// 已发现的候选，按发现顺序返回快照；释放后为空
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
        /// 已绑定的监听，每个本地端口一个，供上层直连层复用；释放后为空
        /// </summary>
        public IReadOnlyList<ITcpTransport> Listeners
        {
            get
            {
                lock (_sync)
                {
                    return _listeners.ToArray();
                }
            }
        }

        /// <summary>
        /// 执行一轮 TCP 候选收集：逐个地址绑定被动监听，平台支持时再补一个同时打开监听
        /// </summary>
        /// <param name="cancellationToken">调用方取消令牌；取消时先触发结束事件再抛出取消异常</param>
        /// <returns>收集结束的任务</returns>
        /// <exception cref="ObjectDisposedException">收集器已释放</exception>
        /// <exception cref="InvalidOperationException">已调用过，收集只能执行一次</exception>
        public async Task GatherAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (Interlocked.CompareExchange(ref _gatherStarted, 1, 0) != 0)
            {
                throw new InvalidOperationException("TCP 候选收集只能执行一次。");
            }

            try
            {
                Collect(cancellationToken);
            }
            finally
            {
                RaiseEndOfCandidates();
            }

            // 整轮收集是纯绑定动作，没有真实异步等待；用已完成的等待让异常进入返回的任务
            await Task.CompletedTask.ConfigureAwait(false);
        }

        /// <summary>
        /// 释放全部监听并清空候选与监听列表；可重复调用
        /// </summary>
        public void Dispose()
        {
            ITcpTransport[] listeners;
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;

                listeners = _listeners.ToArray();
                _listeners.Clear();
                _candidates.Clear();
            }

            for (int i = 0; i < listeners.Length; i++)
            {
                // 单个监听释放失败不能阻断其余监听
                DisposeListener(listeners[i]);
            }
        }

        /// <summary>
        /// 遍历本机地址逐个绑定监听并产出候选
        /// </summary>
        /// <param name="token">取消令牌</param>
        private void Collect(CancellationToken token)
        {
            var addresses = _provider.GetAddresses();
            if (addresses == null || addresses.Count == 0)
            {
                return;
            }

            var component = _options.Component;

            // Windows 上同时打开无对应实现，开启也只会收到被动连接，强制按关闭处理并只提示一次
            var wantSimultaneousOpen = _options.EnableTcpSimultaneousOpen && IsSimultaneousOpenSupported;
            if (_options.EnableTcpSimultaneousOpen && !IsSimultaneousOpenSupported)
            {
                P2PLogHelper.Info(LogNode, "当前平台不支持 TCP 同时打开，按被动方向收集候选");
            }

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

                BindListener(address, component, TcpCandidateType.Passive, token);

                if (wantSimultaneousOpen)
                {
                    token.ThrowIfCancellationRequested();
                    BindListener(address, component, TcpCandidateType.SimultaneousOpen, token);
                }
            }
        }

        /// <summary>
        /// 为单个地址绑定一个监听并产出对应方向的 host 候选
        /// </summary>
        /// <param name="address">绑定地址</param>
        /// <param name="component">分量编号</param>
        /// <param name="tcpType">TCP 方向类型</param>
        /// <param name="token">取消令牌</param>
        private void BindListener(IPAddress address, int component, TcpCandidateType tcpType, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            ITcpTransport listener;
            try
            {
                // 端口 0 由系统分配，每个方向各占一个端口
                listener = new SaeaTcpTransport(new IPEndPoint(address, 0));
            }
            catch (SocketException ex)
            {
                // 单个地址不可绑定只跳过该地址，不影响其余地址的收集
                P2PLogHelper.Debug(LogNode, "绑定地址 " + address + " 失败：" + ex.Message);
                return;
            }

            var local = listener.LocalEndPoint;
            if (local == null || local.Address == null)
            {
                P2PLogHelper.Debug(LogNode, "地址 " + address + " 绑定后没有本地端点，跳过");
                DisposeListener(listener);
                return;
            }

            // 候选必须用 socket 的实际绑定地址，枚举值可能与绑定地址不一致
            if (!TryAddListener(listener))
            {
                DisposeListener(listener);
                return;
            }

            AddCandidate(new Candidate(CandidateType.Host, IceTransportType.Tcp, component, local.Address, local.Port, tcpType, null, 0));
        }

        /// <summary>
        /// 把监听加入复用列表；已释放时返回 false 由调用方自行释放
        /// </summary>
        /// <param name="listener">已绑定的监听</param>
        /// <returns>入表成功返回 true</returns>
        private bool TryAddListener(ITcpTransport listener)
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return false;
                }
                _listeners.Add(listener);
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
        /// 释放单个监听，异常只记日志
        /// </summary>
        /// <param name="listener">待释放的监听，可为空</param>
        private static void DisposeListener(ITcpTransport listener)
        {
            if (listener == null)
            {
                return;
            }

            try
            {
                listener.Dispose();
            }
            catch (Exception ex)
            {
                P2PLogHelper.Debug(LogNode, "释放监听异常：" + ex.GetType().Name);
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