/****************************************************************************
*项目名称：SAEA.MQTT.Common
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Common
*类 名 称：AsyncAutoResetEvent
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 15:51:12
*描述：
*=====================================================================
*修改时间：2019/1/15 15:51:12
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.Common
{
    public class AsyncAutoResetEvent
    {
        private readonly LinkedList<TaskCompletionSource<bool>> _waiters = new LinkedList<TaskCompletionSource<bool>>();
        private bool _isSignaled;

        public AsyncAutoResetEvent()
            : this(false)
        {
        }

        public AsyncAutoResetEvent(bool signaled)
        {
            _isSignaled = signaled;
        }

        public Task<bool> WaitOneAsync()
        {
            return WaitOneAsync(CancellationToken.None);
        }

        public Task<bool> WaitOneAsync(TimeSpan timeout)
        {
            return WaitOneAsync(timeout, CancellationToken.None);
        }

        public Task<bool> WaitOneAsync(CancellationToken cancellationToken)
        {
            return WaitOneAsync(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        public async Task<bool> WaitOneAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TaskCompletionSource<bool> tcs;

            lock (_waiters)
            {
                if (_isSignaled)
                {
                    _isSignaled = false;
                    return true;
                }

                if (timeout == TimeSpan.Zero)
                {
                    return _isSignaled;
                }

                tcs = new TaskCompletionSource<bool>();
                _waiters.AddLast(tcs);
            }

            var winner = await Task.WhenAny(tcs.Task, Task.Delay(timeout, cancellationToken)).ConfigureAwait(false);
            var taskWasSignaled = winner == tcs.Task;
            if (taskWasSignaled)
            {
                return true;
            }

            // We timed-out; remove our reference to the task.
            // This is an O(n) operation since waiters is a LinkedList<T>.
            lock (_waiters)
            {
                _waiters.Remove(tcs);

                if (winner.Status == TaskStatus.Canceled)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                throw new TimeoutException();
            }
        }

        public void Set()
        {
            TaskCompletionSource<bool> toRelease = null;

            lock (_waiters)
            {
                if (_waiters.Count > 0)
                {
                    // Signal the first task in the waiters list.
                    toRelease = _waiters.First.Value;
                    _waiters.RemoveFirst();
                }
                else if (!_isSignaled)
                {
                    // No tasks are pending
                    _isSignaled = true;
                }
            }

            toRelease?.SetResult(true);
        }
    }
}
