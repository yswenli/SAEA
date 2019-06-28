/****************************************************************************
*项目名称：SAEA.Http2.Events
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Events
*类 名 称：AsyncManualResetEvent
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 16:28:16
*描述：
*=====================================================================
*修改时间：2019/6/27 16:28:16
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Http2.Events
{
    public class AsyncManualResetEvent : ICriticalNotifyCompletion
    {
        private static readonly Action isSet = () => { };
        private static readonly Action isReset = () => { };

        private volatile Action _state = isReset;

        public bool IsCompleted => _state == isSet;
        public bool IsReset => _state == isReset;

        public AsyncManualResetEvent(bool signaled)
        {
            if (signaled) _state = isSet;
        }

        public void UnsafeOnCompleted(Action continuation) => OnCompleted(continuation);

        public void OnCompleted(Action continuation)
        {
            if (continuation == null) return;

            var previous = Interlocked.CompareExchange(ref _state, continuation, isReset);
            if (previous == isSet)
            {
                continuation();
            }
        }

        public void GetResult()
        {
        }

        public void Reset()
        {
            Interlocked.Exchange(ref _state, isReset);
        }

        public void Set()
        {
            var completion = Interlocked.Exchange(ref _state, isSet);
            if (completion != isSet && completion != isReset)
            {
                Task.Run(completion);
            }
        }

        public AsyncManualResetEvent GetAwaiter() => this;
    }
}
