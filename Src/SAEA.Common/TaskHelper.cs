using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Common
{
    /// <summary>
    /// 任务辅助类
    /// </summary>
    public static class TaskHelper
    {
        /// <summary>
        /// 超时取消任务
        /// </summary>
        /// <param name="action"></param>
        /// <param name="timeOut"></param>
        /// <param name="canceled"></param>
        public static async void WaitFor(Action action, int timeOut, Action canceled)
        {
            var cts = new CancellationTokenSource(timeOut);
            cts.Token.Register(canceled);
            await Task.Factory.StartNew(action, cts.Token);
        }

        public static Task Start(Action action)
        {
            return Task.Factory.StartNew(action);
        }
    }
}
