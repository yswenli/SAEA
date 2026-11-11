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
*命名空间：SAEA.P2P.Stun
*文件名： StunTransactionLayer
*版本号： v26.11.5.1
*唯一标识：cb50d543-94c0-434c-895f-40560dc8308e
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 16:30:00
*描述：STUN 事务层——注册、匹配应答与重传驱动
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 16:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 STUN 事务层
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Common;

namespace SAEA.P2P.Stun
{
    /// <summary>
    /// STUN 事务层：按事务 ID 管理未决请求，匹配应答、驱动重传并清理已结束事务。
    /// 时间全部取自注入的时钟；构造时不给 tickInterval 就由调用方显式调用 <see cref="Tick"/>，测试可手工推进时间。
    /// </summary>
    public sealed class StunTransactionLayer : IDisposable
    {
        /// <summary>
        /// 未决事务表，键为事务 ID 的十六进制串
        /// </summary>
        private readonly Dictionary<string, StunTransaction> _pending = new Dictionary<string, StunTransaction>();

        /// <summary>
        /// 未决事务表的保护锁
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 注入的时钟
        /// </summary>
        private readonly IRetryClock _clock;

        /// <summary>
        /// 可选的后台计时器；为 null 时由调用方显式 Tick
        /// </summary>
        private readonly Timer _timer;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 创建事务层，由调用方显式驱动 <see cref="Tick"/>
        /// </summary>
        /// <param name="clock">计时时钟</param>
        public StunTransactionLayer(IRetryClock clock)
            : this(clock, null)
        {
        }

        /// <summary>
        /// 创建事务层
        /// </summary>
        /// <param name="clock">计时时钟</param>
        /// <param name="tickInterval">后台计时器周期；为 null 时不启动计时器</param>
        public StunTransactionLayer(IRetryClock clock, TimeSpan? tickInterval)
        {
            if (clock == null)
            {
                throw new ArgumentNullException(nameof(clock));
            }

            _clock = clock;
            if (tickInterval.HasValue)
            {
                if (tickInterval.Value <= TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException(nameof(tickInterval));
                }
                _timer = new Timer(OnTickTimer, null, tickInterval.Value, tickInterval.Value);
            }
        }

        /// <summary>
        /// 当前未决事务数
        /// </summary>
        public int PendingCount
        {
            get
            {
                lock (_sync)
                {
                    return _pending.Count;
                }
            }
        }

        /// <summary>
        /// 注册并发送一个请求事务；同一事务 ID 已有未决请求时返回 false
        /// </summary>
        /// <param name="request">请求消息，必须是 <see cref="StunClass.Request"/></param>
        /// <param name="reliable">是否可靠传输；为 true 时不重传</param>
        /// <param name="send">发送动作，收到已编码的完整报文</param>
        /// <param name="transaction">注册成功的事务，失败时为 null</param>
        /// <param name="rtoMilliseconds">初始重传间隔，单位毫秒；默认 500ms</param>
        /// <param name="cancellationToken">取消令牌，取消后事务以 <see cref="StunTransactionFailure.Cancelled"/> 结束</param>
        /// <returns>注册成功返回 true</returns>
        public bool TryBegin(StunMessage request, bool reliable, Action<byte[]> send, out StunTransaction transaction, int rtoMilliseconds = StunTransactionTiming.RtoMilliseconds, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            if (send == null)
            {
                throw new ArgumentNullException(nameof(send));
            }
            if (request.Class != StunClass.Request)
            {
                throw new ArgumentException("只有请求类消息才建立事务", nameof(request));
            }
            if (rtoMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rtoMilliseconds));
            }

            var created = new StunTransaction(
                request.ToArray(),
                request.TransactionId.ToArray(),
                reliable,
                send,
                rtoMilliseconds,
                _clock.UtcNow);

            lock (_sync)
            {
                if (_disposed)
                {
                    transaction = null;
                    return false;
                }
                if (_pending.ContainsKey(created.Key))
                {
                    transaction = null;
                    return false;
                }

                _pending.Add(created.Key, created);
            }

            transaction = created;

            // 完成后立即从表里摘掉，避免未决表随会话累积
            created.Completion.ContinueWith(
                t => RemovePending(created),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            created.RegisterCancellation(cancellationToken);
            created.SendFirst();
            return true;
        }

        /// <summary>
        /// 用收到的应答完成对应事务；迟到或未知事务的应答直接丢弃
        /// </summary>
        /// <param name="response">成功或错误应答</param>
        /// <returns>本次是否完成了一个未决事务</returns>
        public bool HandleResponse(StunMessage response)
        {
            if (response == null)
            {
                return false;
            }
            if (response.Class != StunClass.SuccessResponse && response.Class != StunClass.ErrorResponse)
            {
                return false;
            }

            StunTransaction transaction = null;
            var key = StunTransaction.ToKey(response.TransactionId.Span);
            lock (_sync)
            {
                if (!_pending.TryGetValue(key, out transaction))
                {
                    transaction = null;
                }
            }

            if (transaction == null)
            {
                P2PLogHelper.Debug("StunTransactionLayer", "丢弃迟到或未知事务的应答");
                return false;
            }

            if (!transaction.CompleteWithResponse(response))
            {
                P2PLogHelper.Debug("StunTransactionLayer", "应答到达时事务已结束");
                return false;
            }

            RemovePending(transaction);
            return true;
        }

        /// <summary>
        /// 按时钟推进全部未决事务：补发到期重传，判定超时
        /// </summary>
        public void Tick()
        {
            List<StunTransaction> snapshot;
            lock (_sync)
            {
                if (_disposed || _pending.Count == 0)
                {
                    return;
                }

                snapshot = new List<StunTransaction>(_pending.Values);
            }

            var now = _clock.UtcNow;
            for (var i = 0; i < snapshot.Count; i++)
            {
                snapshot[i].Tick(now);
                if (snapshot[i].IsCompleted)
                {
                    RemovePending(snapshot[i]);
                }
            }
        }

        /// <summary>
        /// 取消全部未决事务并清空表，用于关闭连接或会话
        /// </summary>
        public void CancelAll()
        {
            List<StunTransaction> snapshot;
            lock (_sync)
            {
                snapshot = new List<StunTransaction>(_pending.Values);
                _pending.Clear();
            }

            for (var i = 0; i < snapshot.Count; i++)
            {
                snapshot[i].Fail(StunTransactionFailure.Cancelled);
            }
        }

        /// <summary>
        /// 释放事务层：停止后台计时器并取消全部未决事务
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

            if (_timer != null)
            {
                _timer.Dispose();
            }

            CancelAll();
        }

        /// <summary>
        /// 后台计时器回调；异常在此吞掉，避免计时器线程异常终止进程
        /// </summary>
        /// <param name="state">未使用</param>
        private void OnTickTimer(object state)
        {
            try
            {
                Tick();
            }
            catch (Exception ex)
            {
                P2PLogHelper.Error("StunTransactionLayer", "STUN_TICK_FAILED", "STUN 事务计时推进失败", ex);
            }
        }

        /// <summary>
        /// 从表里删除指定事务；只删同一个实例，避免误删同 ID 的新事务
        /// </summary>
        /// <param name="transaction">已结束的事务</param>
        private void RemovePending(StunTransaction transaction)
        {
            lock (_sync)
            {
                StunTransaction existing;
                if (_pending.TryGetValue(transaction.Key, out existing) && ReferenceEquals(existing, transaction))
                {
                    _pending.Remove(transaction.Key);
                }
            }
        }
    }
}