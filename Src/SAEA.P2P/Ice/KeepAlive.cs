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
*文件名： KeepAlive
*版本号： v26.11.5.1
*唯一标识：3f6f0c2e-9a34-4d8a-9c71-1d7b52e4a6c1
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 18:00:00
*描述：选中候选对的 STUN Binding Indication 保活
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 18:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增保活指示定时器
*
*****************************************************************************/
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Stun;
using SAEA.P2P.Transport;

namespace SAEA.P2P.Ice
{
    /// <summary>
    /// 保活：在选中的候选对上周期发送不带认证的 STUN Binding Indication，用于维持 NAT 映射。
    /// 与 consent freshness 是两套独立机制，各自持有定时器，不共享节拍。
    /// </summary>
    public sealed class KeepAlive : IDisposable
    {
        /// <summary>
        /// Tr 下限；低于该值会与检查阶段抢节奏，且不足以维持多数 NAT 映射
        /// </summary>
        public const int MinTrMilliseconds = 15000;

        /// <summary>
        /// 保活报文的目标传输，本类不负责释放
        /// </summary>
        private readonly IUdpTransport _transport;

        /// <summary>
        /// 保活报文的远端端点，即选中候选对的对端
        /// </summary>
        private readonly IPEndPoint _remoteEndPoint;

        /// <summary>
        /// 保护运行状态与取消源，Start/Stop/Dispose 可能来自不同线程
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 后台循环的取消源；未启动或已停止时为 null
        /// </summary>
        private CancellationTokenSource _cts;

        /// <summary>
        /// 后台循环任务，用于观察异常，不做等待
        /// </summary>
        private Task _runTask;

        /// <summary>
        /// 后台循环是否在运行
        /// </summary>
        private bool _running;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 创建保活定时器；Tr 不得小于 15 秒
        /// </summary>
        /// <param name="tr">两次保活指示之间的间隔，不得小于 15 秒</param>
        /// <param name="transport">已绑定的 UDP 传输，本类不负责释放</param>
        /// <param name="remoteEndPoint">选中候选对的对端端点</param>
        /// <exception cref="ArgumentNullException">transport 或 remoteEndPoint 为空</exception>
        /// <exception cref="ArgumentOutOfRangeException">Tr 小于 15 秒</exception>
        public KeepAlive(TimeSpan tr, IUdpTransport transport, IPEndPoint remoteEndPoint)
        {
            if (transport == null)
            {
                throw new ArgumentNullException(nameof(transport));
            }
            if (remoteEndPoint == null)
            {
                throw new ArgumentNullException(nameof(remoteEndPoint));
            }
            if (tr.TotalMilliseconds < MinTrMilliseconds)
            {
                throw new ArgumentOutOfRangeException(nameof(tr), "Tr 不得小于 15 秒");
            }

            Tr = tr;
            _transport = transport;
            _remoteEndPoint = remoteEndPoint;
        }

        /// <summary>
        /// 保活间隔
        /// </summary>
        public TimeSpan Tr { get; }

        /// <summary>
        /// 后台保活循环是否在运行
        /// </summary>
        public bool IsRunning
        {
            get
            {
                lock (_sync)
                {
                    return _running;
                }
            }
        }

        /// <summary>
        /// 构造一个保活指示：Binding Indication 加 FINGERPRINT，不带任何认证属性
        /// </summary>
        /// <returns>可直接发送的报文字节</returns>
        public byte[] BuildIndication()
        {
            // 不传事务 ID，让构造器生成新的：保活指示无事务无应答，但每次仍需独立 ID 便于对端区分
            var message = new StunMessage(StunMessageType.Binding, StunClass.Indication);

            // 保活指示不认证：不加 MESSAGE-INTEGRITY 等认证属性，只可带 FINGERPRINT
            StunFingerprint.Append(message);

            return message.ToArray();
        }

        /// <summary>
        /// 立即向远端发送一次保活指示
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>发送完成的任务</returns>
        public Task SendOnceAsync(CancellationToken cancellationToken)
        {
            return _transport.SendToAsync(_remoteEndPoint, BuildIndication(), cancellationToken);
        }

        /// <summary>
        /// 按 Tr 周期发送保活指示，直到取消；取消时正常返回不抛异常。
        /// 首次发送在首个 Tr 之后，避免与检查阶段抢节奏。
        /// </summary>
        /// <param name="delay">等待注入点；为 null 时用 Task.Delay</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>循环结束的任务</returns>
        public async Task RunAsync(Func<TimeSpan, CancellationToken, Task> delay, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await DelayAsync(delay, Tr, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    await SendOnceAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // 取消与发送可能同时发生，此时按正常停止处理
                    return;
                }
            }
        }

        /// <summary>
        /// 启动后台保活循环；重复调用幂等
        /// </summary>
        /// <param name="delay">等待注入点；为 null 时用 Task.Delay</param>
        public void Start(Func<TimeSpan, CancellationToken, Task> delay = null)
        {
            lock (_sync)
            {
                if (_disposed || _running)
                {
                    return;
                }

                // 上一次 Stop 留下的取消源已取消，这里换新的，避免沿用已失效的令牌
                if (_cts != null)
                {
                    _cts.Dispose();
                }

                _cts = new CancellationTokenSource();
                _running = true;
                var token = _cts.Token;

                // delay 由调用方注入，测试不真等 15 秒
                _runTask = Task.Run(() => RunLoopAsync(delay, token));
            }
        }

        /// <summary>
        /// 停止后台保活循环；不等待后台任务，也不抛异常
        /// </summary>
        public void Stop()
        {
            CancellationTokenSource cts;
            lock (_sync)
            {
                cts = _cts;
                _running = false;
                _runTask = null;
            }

            if (cts == null)
            {
                return;
            }

            // 不 Join：Stop 可能来自会话清理线程，等待最长一个 Tr 的延时不可接受
            cts.Cancel();
        }

        /// <summary>
        /// 停止循环并释放取消源；重复调用不抛异常
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

            Stop();

            lock (_sync)
            {
                if (_cts != null)
                {
                    _cts.Dispose();
                    _cts = null;
                }
            }
        }

        /// <summary>
        /// 后台循环包装：后台没有调用方能接住异常，这里吞掉并结束循环
        /// </summary>
        /// <param name="delay">等待注入点</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>循环结束的任务</returns>
        private async Task RunLoopAsync(Func<TimeSpan, CancellationToken, Task> delay, CancellationToken cancellationToken)
        {
            try
            {
                await RunAsync(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                // 后台保活失败无人可报，吞掉避免未观察的任务异常；下一轮 Start 会重新起循环
            }
            finally
            {
                lock (_sync)
                {
                    _running = false;
                }
            }
        }

        /// <summary>
        /// 按注入点等待一个间隔
        /// </summary>
        /// <param name="delay">等待注入点，可为 null</param>
        /// <param name="interval">等待时长</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>等待完成的任务</returns>
        private static Task DelayAsync(Func<TimeSpan, CancellationToken, Task> delay, TimeSpan interval, CancellationToken cancellationToken)
        {
            if (delay != null)
            {
                return delay(interval, cancellationToken);
            }

            return Task.Delay(interval, cancellationToken);
        }
    }
}