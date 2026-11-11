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
*文件名： StunTransaction
*版本号： v26.11.5.1
*唯一标识：b19c491a-4591-4a48-b943-7b84c8acc2c8
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 16:25:00
*描述：单个 STUN 请求事务的重传与完成
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 00:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：事务完成时在锁外释放取消注册，避免与取消回调互锁
*
*****************************************************************************/
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Common;

namespace SAEA.P2P.Stun
{
    /// <summary>
    /// 单个 STUN 请求事务：按设计的时刻表重传，收到同事务 ID 的应答即完成；
    /// 可靠传输上不重传，只等超时。所有状态迁移都在同一把锁内完成。
    /// </summary>
    public sealed class StunTransaction
    {
        /// <summary>
        /// 事务状态与发送计数的保护锁
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 已编码完成的请求报文，重传直接复用，避免发送期间原消息被改写
        /// </summary>
        private readonly byte[] _requestBytes;

        /// <summary>
        /// 事务 ID 副本
        /// </summary>
        private readonly byte[] _transactionId;

        /// <summary>
        /// 事务 ID 的十六进制键，用于字典查找
        /// </summary>
        private readonly string _key;

        /// <summary>
        /// 发送动作，由调用方绑定到具体传输
        /// </summary>
        private readonly Action<byte[]> _send;

        /// <summary>
        /// 初始重传间隔，单位毫秒
        /// </summary>
        private readonly int _rtoMilliseconds;

        /// <summary>
        /// 事务开始时刻
        /// </summary>
        private readonly DateTime _startedAt;

        /// <summary>
        /// 判定失败的时刻
        /// </summary>
        private readonly DateTime _deadlineAt;

        /// <summary>
        /// 结果通道；完成后的等待方按结果决定后续动作
        /// </summary>
        private readonly TaskCompletionSource<StunTransactionResult> _completion;

        /// <summary>
        /// 取消令牌注册，完成时必须释放
        /// </summary>
        private CancellationTokenRegistration _registration;

        /// <summary>
        /// 已发送次数，含首发
        /// </summary>
        private int _attempts;

        /// <summary>
        /// 下一次重传在时刻表中的序号；首发占 0，故从 1 开始
        /// </summary>
        private int _nextSendIndex;

        /// <summary>
        /// 当前状态
        /// </summary>
        private StunTransactionState _state;

        /// <summary>
        /// 创建事务；只由事务层调用
        /// </summary>
        /// <param name="requestBytes">已编码的请求报文</param>
        /// <param name="transactionId">事务 ID</param>
        /// <param name="reliable">是否可靠传输；为 true 时不重传</param>
        /// <param name="send">发送动作</param>
        /// <param name="rtoMilliseconds">初始重传间隔，单位毫秒</param>
        /// <param name="startedAt">事务开始时刻</param>
        internal StunTransaction(byte[] requestBytes, byte[] transactionId, bool reliable, Action<byte[]> send, int rtoMilliseconds, DateTime startedAt)
        {
            _requestBytes = requestBytes;
            _transactionId = transactionId;
            _key = ToKey(transactionId);
            _send = send;
            _rtoMilliseconds = rtoMilliseconds;
            _startedAt = startedAt;
            _deadlineAt = startedAt.AddMilliseconds(StunTransactionTiming.GetTimeoutMilliseconds(rtoMilliseconds));
            _nextSendIndex = 1;
            Reliable = reliable;
            _completion = new TaskCompletionSource<StunTransactionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        /// <summary>
        /// 事务 ID；返回只读视图
        /// </summary>
        public ReadOnlyMemory<byte> TransactionId
        {
            get { return _transactionId; }
        }

        /// <summary>
        /// 事务 ID 的十六进制键，同一事务 ID 对应同一个键
        /// </summary>
        public string Key
        {
            get { return _key; }
        }

        /// <summary>
        /// 是否可靠传输；可靠传输上不做 STUN 层重传
        /// </summary>
        public bool Reliable { get; }

        /// <summary>
        /// 已发送次数，含首发
        /// </summary>
        public int Attempts
        {
            get
            {
                lock (_sync)
                {
                    return _attempts;
                }
            }
        }

        /// <summary>
        /// 当前状态
        /// </summary>
        public StunTransactionState State
        {
            get
            {
                lock (_sync)
                {
                    return _state;
                }
            }
        }

        /// <summary>
        /// 是否已经结束；结束后不再重传，也不接受应答
        /// </summary>
        public bool IsCompleted
        {
            get { return State != StunTransactionState.Waiting; }
        }

        /// <summary>
        /// 事务完成的任务；成功、超时、取消都会完成该任务
        /// </summary>
        public Task<StunTransactionResult> Completion
        {
            get { return _completion.Task; }
        }

        /// <summary>
        /// 发送首发报文；发送异常直接判传输失败
        /// </summary>
        internal void SendFirst()
        {
            StunTransactionResult failure = null;
            lock (_sync)
            {
                if (_state != StunTransactionState.Waiting)
                {
                    return;
                }

                _attempts = 1;
                failure = SendLocked();
            }

            if (failure != null)
            {
                Finish(failure);
            }
        }

        /// <summary>
        /// 按当前时间推进事务：先判超时，再补发到期的重传
        /// </summary>
        /// <param name="now">当前时间</param>
        internal void Tick(DateTime now)
        {
            StunTransactionResult failure = null;
            lock (_sync)
            {
                if (_state != StunTransactionState.Waiting)
                {
                    return;
                }

                if (now >= _deadlineAt)
                {
                    failure = StunTransactionResult.Failed(StunTransactionFailure.Timeout, _attempts);
                }
                else if (!Reliable)
                {
                    while (_nextSendIndex < StunTransactionTiming.TotalSendCount)
                    {
                        var offset = StunTransactionTiming.GetSendOffsetMilliseconds(_nextSendIndex, _rtoMilliseconds);
                        if (now < _startedAt.AddMilliseconds(offset))
                        {
                            break;
                        }

                        _nextSendIndex++;
                        _attempts++;
                        failure = SendLocked();
                        if (failure != null)
                        {
                            break;
                        }
                    }
                }
            }

            // 结束事务要释放取消注册，而释放会等待正在执行的取消回调退出，
            // 因此必须在锁外完成，否则与正在等待 _sync 的取消回调互等
            if (failure != null)
            {
                Finish(failure);
            }
        }

        /// <summary>
        /// 用对端应答完成事务；事务已结束时返回 false
        /// </summary>
        /// <param name="response">对端应答</param>
        /// <returns>本次是否由该应答完成事务</returns>
        internal bool CompleteWithResponse(StunMessage response)
        {
            int attempts;
            lock (_sync)
            {
                if (_state != StunTransactionState.Waiting)
                {
                    return false;
                }

                attempts = _attempts;
            }

            return Finish(StunTransactionResult.Success(response, attempts));
        }

        /// <summary>
        /// 以给定原因结束事务；事务已结束时为空操作
        /// </summary>
        /// <param name="failure">失败原因</param>
        internal void Fail(StunTransactionFailure failure)
        {
            Finish(StunTransactionResult.Failed(failure, Attempts));
        }

        /// <summary>
        /// 注册取消回调；事务已结束时注册无效
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        internal void RegisterCancellation(CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled)
            {
                return;
            }

            CancellationTokenRegistration registration;
            lock (_sync)
            {
                if (_state != StunTransactionState.Waiting)
                {
                    return;
                }

                // 令牌可能已取消，回调会在 Register 内同步执行并把事务结束
                registration = cancellationToken.Register(() => Fail(StunTransactionFailure.Cancelled));
                if (_state == StunTransactionState.Waiting)
                {
                    _registration = registration;
                    return;
                }
            }

            registration.Dispose();
        }

        /// <summary>
        /// 发送一次报文；异常视为传输不可用
        /// </summary>
        /// <returns>发送成功返回 null，发送异常返回传输失败结果</returns>
        private StunTransactionResult SendLocked()
        {
            try
            {
                _send(_requestBytes);
                return null;
            }
            catch (Exception ex)
            {
                P2PLogHelper.Error("StunTransaction", "STUN_SEND_FAILED", "STUN 事务发送失败", ex);
                return StunTransactionResult.Failed(StunTransactionFailure.TransportError, _attempts);
            }
        }

        /// <summary>
        /// 结束事务：锁内切状态并摘除取消注册，锁外完成任务并释放注册
        /// </summary>
        /// <param name="result">最终结果</param>
        /// <returns>本次是否由该结果结束事务</returns>
        private bool Finish(StunTransactionResult result)
        {
            CancellationTokenRegistration registration;
            lock (_sync)
            {
                if (_state != StunTransactionState.Waiting)
                {
                    return false;
                }

                _state = result.Succeeded ? StunTransactionState.Completed : StunTransactionState.Failed;
                registration = _registration;
                _registration = default(CancellationTokenRegistration);
            }

            _completion.TrySetResult(result);

            // Dispose 会等待正在执行的取消回调退出，而回调里还要拿 _sync，
            // 持锁释放会与回调互等，故必须在锁外释放
            try
            {
                registration.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // 令牌源已释放时释放注册为空操作
            }

            return true;
        }

        /// <summary>
        /// 把事务 ID 转成十六进制键
        /// </summary>
        /// <param name="transactionId">事务 ID</param>
        /// <returns>大写十六进制字符串</returns>
        internal static string ToKey(ReadOnlySpan<byte> transactionId)
        {
            var builder = new StringBuilder(transactionId.Length * 2);
            for (var i = 0; i < transactionId.Length; i++)
            {
                builder.Append(transactionId[i].ToString("X2"));
            }
            return builder.ToString();
        }
    }
}