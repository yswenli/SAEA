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
 *命名空间：SAEA.Sockets.Core
 *文件名： SendOperation
 *版本号： v26.11.5.1
 *唯一标识：a7c1e4b2-5d6f-4a8b-9c0d-1e2f3a4b5c6d
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/9 10:30:00
 *描述：单次发送操作的完成源与所有权凭据
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/9 10:30:00
 *修改人： yswenli
 *版本号： v26.11.5.1
 *描述：单次发送操作的完成源与所有权凭据
 *
 *****************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets.Core
{
    /// <summary>
    /// 单次发送操作的完成源与所有权凭据。发送完成回调、同步异常、断开清理与超时
    /// 共同竞争同一个完成点，保证 <see cref="IDisposable"/> 所有权对象恰好释放一次。
    /// </summary>
    internal sealed class SendOperation
    {
        private readonly TaskCompletionSource<bool> _completion =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private IDisposable _owner;

        private int _completed;

        private int _timedOut;

        /// <summary>
        /// 创建发送操作并接管所有权对象。
        /// </summary>
        /// <param name="owner">待发送数据的所有权对象，可为 null</param>
        internal SendOperation(IDisposable owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// 本次发送的完成任务；成功完成、故障或超时放弃后均可等待。
        /// </summary>
        internal Task Task
        {
            get { return _completion.Task; }
        }

        /// <summary>
        /// 是否已被超时标记；完成回调据此把成功改为超时失败。
        /// </summary>
        internal bool IsTimedOut
        {
            get { return Volatile.Read(ref _timedOut) != 0; }
        }

        /// <summary>
        /// 标记本次发送已超时。已经完成的操作返回 false，避免撤销已确定的成功结果。
        /// </summary>
        /// <returns>标记成功且操作仍未完成时为 true</returns>
        internal bool TryMarkTimedOut()
        {
            if (Volatile.Read(ref _completed) != 0) return false;
            Interlocked.Exchange(ref _timedOut, 1);
            return Volatile.Read(ref _completed) == 0;
        }

        /// <summary>
        /// 以成功完成本次发送：释放所有权对象一次并解除等待方。
        /// </summary>
        /// <returns>本次调用是否赢得完成竞争</returns>
        internal bool TryCompleteSuccess()
        {
            if (Interlocked.Exchange(ref _completed, 1) != 0) return false;
            ReleaseOwner();
            _completion.TrySetResult(true);
            return true;
        }

        /// <summary>
        /// 以失败完成本次发送：释放所有权对象一次并让等待方观察到异常。
        /// </summary>
        /// <param name="exception">失败原因，为 null 时使用通用发送失败异常</param>
        /// <returns>本次调用是否赢得完成竞争</returns>
        internal bool TryCompleteException(Exception exception)
        {
            if (exception == null) exception = new Exception("发送失败。");
            if (Interlocked.Exchange(ref _completed, 1) != 0) return false;
            ReleaseOwner();
            _completion.TrySetException(exception);
            return true;
        }

        /// <summary>
        /// 超时且无法证明底层完成回调已经到达时使用：让等待方失败，但刻意不释放所有权对象。
        /// 内核可能仍在读取该缓冲，归还池化数组会被再次租用并覆写造成数据损坏；
        /// 这是唯一允许「不恰好一次」释放的路径，必须在调用处保留该说明。
        /// </summary>
        /// <param name="exception">超时原因</param>
        /// <returns>本次调用是否赢得完成竞争</returns>
        internal bool TryAbandon(Exception exception)
        {
            if (exception == null) exception = new TimeoutException("发送数据超时。");
            if (Interlocked.Exchange(ref _completed, 1) != 0) return false;
            _completion.TrySetException(exception);
            return true;
        }

        /// <summary>
        /// 原子取出所有权对象，保证只被取出一次。
        /// </summary>
        /// <returns>所有权对象；已被取出时为 null</returns>
        internal IDisposable TakeOwner()
        {
            return Interlocked.Exchange(ref _owner, null);
        }

        private void ReleaseOwner()
        {
            var owner = TakeOwner();
            if (owner == null) return;
            try { owner.Dispose(); } catch { }
        }
    }
}