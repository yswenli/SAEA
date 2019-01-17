/****************************************************************************
*项目名称：SAEA.MQTT.Common
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Common
*类 名 称：AsyncLock
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/16 9:41:10
*描述：
*=====================================================================
*修改时间：2019/1/16 9:41:10
*修 改 人： yswenli
*版 本 号： V3.6.2.2
*描    述：
*****************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.Common
{
    public class AsyncLock : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly Task<IDisposable> _releaser;

        public AsyncLock()
        {
            _releaser = Task.FromResult((IDisposable)new Releaser(this));
        }

        public Task<IDisposable> LockAsync(CancellationToken cancellationToken)
        {
            var wait = _semaphore.WaitAsync(cancellationToken);
            return wait.IsCompleted ?
                        _releaser :
                        wait.ContinueWith((_, state) => (IDisposable)state,
                            _releaser.Result, cancellationToken,
                            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        public void Dispose()
        {
            _semaphore?.Dispose();
        }

        private class Releaser : IDisposable
        {
            private readonly AsyncLock _toRelease;

            internal Releaser(AsyncLock toRelease)
            {
                _toRelease = toRelease;
            }

            public void Dispose()
            {
                _toRelease._semaphore.Release();
            }
        }
    }
}
