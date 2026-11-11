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
*文件名： ConsentFreshness
*版本号： v26.11.5.1
*唯一标识：b04a6e71-2d5f-4c8e-8f37-5a9c1e6d2b84
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 18:00:00
*描述：选中候选对的 consent freshness 探测与失效判定
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 18:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 consent freshness 定时器
*
*****************************************************************************/
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Transport;

namespace SAEA.P2P.Ice
{
    /// <summary>
    /// consent freshness（RFC 7675）：在选中的候选对上周期发送带认证的 Binding 请求，
    /// 超过 30 秒没有有效响应即判 consent 失效，调用方必须停止在该候选对发送并丢弃它。
    /// 与保活指示是两套独立机制，各自持有定时器，不共享节拍。
    /// </summary>
    public sealed class ConsentFreshness : IDisposable
    {
        /// <summary>
        /// 探测基准间隔，5 秒
        /// </summary>
        public const int IntervalMilliseconds = 5000;

        /// <summary>
        /// 失效判定窗口，30 秒
        /// </summary>
        public const int TimeoutMilliseconds = 30000;

        /// <summary>
        /// 抖动下限，避免同一对端同时发起探测
        /// </summary>
        private const double MinJitter = 0.8;

        /// <summary>
        /// 抖动上限
        /// </summary>
        private const double MaxJitter = 1.2;

        /// <summary>
        /// 实际发探测包的连通性检查，本类不负责释放
        /// </summary>
        private readonly ConnectivityCheck _check;

        /// <summary>
        /// 探测包的发送传输，本类不负责释放
        /// </summary>
        private readonly IUdpTransport _transport;

        /// <summary>
        /// 探测包目标端点，即选中候选对的对端
        /// </summary>
        private readonly IPEndPoint _remoteEndPoint;

        /// <summary>
        /// 对端 ufrag，写入探测请求的 USERNAME
        /// </summary>
        private readonly string _remoteUfrag;

        /// <summary>
        /// 对端口令，用于签发探测请求的 MESSAGE-INTEGRITY；不进日志与异常消息
        /// </summary>
        private string _remotePassword;

        /// <summary>
        /// 本地角色，决定探测请求携带哪个角色属性
        /// </summary>
        private readonly IceRole _role;

        /// <summary>
        /// 角色冲突判定用的 64 位随机数
        /// </summary>
        private readonly ulong _tiebreaker;

        /// <summary>
        /// 探测请求的 PRIORITY
        /// </summary>
        private readonly uint _priority;

        /// <summary>
        /// 抖动工厂，返回 0 到 1 的倍率
        /// </summary>
        private readonly Func<double> _jitterFactory;

        /// <summary>
        /// 时钟注入点，测试用可变时钟替代真实时间
        /// </summary>
        private readonly Func<DateTime> _utcNow;

        /// <summary>
        /// 保护运行状态与取消源，Start/Stop/Dispose 可能来自不同线程
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 后台循环的取消源；未启动时为 null，Stop 后保留供 Dispose 释放
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
        /// 创建 consent freshness 定时器
        /// </summary>
        /// <param name="check">连通性检查，负责实际收发与完整性处理</param>
        /// <param name="transport">已绑定的 UDP 传输，本类不负责释放</param>
        /// <param name="remoteEndPoint">选中候选对的对端端点</param>
        /// <param name="remoteUfrag">对端 ufrag，非空</param>
        /// <param name="remotePassword">对端口令，非空；不进日志与异常消息</param>
        /// <param name="role">本地角色</param>
        /// <param name="tiebreaker">角色冲突判定用的 64 位随机数</param>
        /// <param name="priority">探测请求的 PRIORITY，由调用方按 peer-reflexive 类型算好</param>
        /// <param name="jitterFactory">抖动工厂，为 null 时内部用 Random 生成 0 到 1 的值</param>
        /// <param name="utcNow">时钟，为 null 时用 DateTime.UtcNow</param>
        /// <exception cref="ArgumentNullException">check、transport 或 remoteEndPoint 为空</exception>
        /// <exception cref="ArgumentException">对端 ufrag 或口令为空</exception>
        public ConsentFreshness(
            ConnectivityCheck check,
            IUdpTransport transport,
            IPEndPoint remoteEndPoint,
            string remoteUfrag,
            string remotePassword,
            IceRole role,
            ulong tiebreaker,
            uint priority,
            Func<double> jitterFactory = null,
            Func<DateTime> utcNow = null)
        {
            if (check == null)
            {
                throw new ArgumentNullException(nameof(check));
            }
            if (transport == null)
            {
                throw new ArgumentNullException(nameof(transport));
            }
            if (remoteEndPoint == null)
            {
                throw new ArgumentNullException(nameof(remoteEndPoint));
            }
            // 凭据为空的异常只带参数名，绝不把口令值拼进消息
            if (string.IsNullOrEmpty(remoteUfrag))
            {
                throw new ArgumentException("对端 ufrag 不能为空", nameof(remoteUfrag));
            }
            if (string.IsNullOrEmpty(remotePassword))
            {
                throw new ArgumentException("对端口令不能为空", nameof(remotePassword));
            }

            _check = check;
            _transport = transport;
            _remoteEndPoint = remoteEndPoint;
            _remoteUfrag = remoteUfrag;
            _remotePassword = remotePassword;
            _role = role;
            _tiebreaker = tiebreaker;
            _priority = priority;
            _jitterFactory = jitterFactory == null ? CreateJitterFactory() : jitterFactory;
            _utcNow = utcNow == null ? new Func<DateTime>(GetUtcNow) : utcNow;

            LastResponseUtc = _utcNow();
        }

        /// <summary>
        /// consent 是否已失效；失效后调用方必须停止在该候选对发送
        /// </summary>
        public bool IsExpired { get; private set; }

        /// <summary>
        /// 最近一次有效探测响应的时刻，构造时取创建时刻
        /// </summary>
        public DateTime LastResponseUtc { get; private set; }

        /// <summary>
        /// 后台探测循环是否在运行
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
        /// 本次探测间隔：基准 5 秒乘以夹到 0.8 到 1.2 的抖动倍率
        /// </summary>
        /// <returns>探测间隔</returns>
        public TimeSpan ComputeInterval()
        {
            var jitter = _jitterFactory();

            // 工厂可能给出非数值，抖动无意义时按 1.0 处理，避免落到非法 TimeSpan
            if (double.IsNaN(jitter))
            {
                jitter = 1.0;
            }
            if (jitter < MinJitter)
            {
                jitter = MinJitter;
            }
            if (jitter > MaxJitter)
            {
                jitter = MaxJitter;
            }

            return TimeSpan.FromMilliseconds(IntervalMilliseconds * jitter);
        }

        /// <summary>
        /// 发送一次带认证的 Binding 请求；失败只返回 false，不改动时间
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>收到有效响应返回 true</returns>
        public async Task<bool> ProbeAsync(CancellationToken cancellationToken)
        {
            // 探测包需要 MESSAGE-INTEGRITY：不带认证的请求拿不到能证明 consent 的响应
            var result = await _check.CheckAsync(
                _transport,
                _remoteEndPoint,
                _remoteUfrag,
                _remotePassword,
                _role,
                _tiebreaker,
                _priority,
                false,
                null,
                null,
                cancellationToken).ConfigureAwait(false);

            if (!result.Succeeded)
            {
                return false;
            }

            LastResponseUtc = _utcNow();
            IsExpired = false;
            return true;
        }

        /// <summary>
        /// 判定 consent 是否超时；失效事件只触发一次
        /// </summary>
        /// <returns>已失效返回 true</returns>
        public bool CheckExpiry()
        {
            if (IsExpired)
            {
                return true;
            }

            if (_utcNow() - LastResponseUtc < TimeSpan.FromMilliseconds(TimeoutMilliseconds))
            {
                return false;
            }

            IsExpired = true;
            RaiseOnConsentExpired();
            return true;
        }

        /// <summary>
        /// 按抖动间隔探测并判定失效，失效后立即退出循环；取消时正常返回不抛异常
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
                    await DelayAsync(delay, ComputeInterval(), cancellationToken).ConfigureAwait(false);
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
                    // 探测失败不抛：一次丢包不能中断循环，超时判定由 CheckExpiry 累积
                    await ProbeAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (CheckExpiry())
                {
                    return;
                }
            }
        }

        /// <summary>
        /// 启动后台探测循环；重复调用幂等
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

                // delay 由调用方注入，测试不真等 5 秒
                _runTask = Task.Run(() => RunLoopAsync(delay, token));
            }
        }

        /// <summary>
        /// 停止后台探测循环；不等待后台任务，也不抛异常
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

            // 不 Join：Stop 可能来自会话清理线程，等待最长一个探测间隔的延时不可接受
            cts.Cancel();
        }

        /// <summary>
        /// 停止循环并断开口令引用、释放取消源；重复调用不抛异常
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

            // string 不可变，置空只能让本实例不再持有引用，已复制的字符不会被覆盖
            _remotePassword = null;
        }

        /// <summary>
        /// consent 失效时触发一次
        /// </summary>
        public event Action OnConsentExpired;

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
                // 后台探测失败无人可报，吞掉避免未观察的任务异常；下一轮 Start 会重新起循环
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
        /// 触发失效事件
        /// </summary>
        private void RaiseOnConsentExpired()
        {
            var handler = OnConsentExpired;
            if (handler != null)
            {
                handler();
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

        /// <summary>
        /// 默认抖动工厂：每个实例独占一个 Random，避免共享静态实例的并发争用
        /// </summary>
        /// <returns>返回 0 到 1 的工厂</returns>
        private static Func<double> CreateJitterFactory()
        {
            var random = new Random();
            return () => random.NextDouble();
        }

        /// <summary>
        /// 默认时钟
        /// </summary>
        /// <returns>当前 UTC 时间</returns>
        private static DateTime GetUtcNow()
        {
            return DateTime.UtcNow;
        }
    }
}