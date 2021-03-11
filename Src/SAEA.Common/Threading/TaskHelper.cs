/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Commom.Threading
*文件名： TaskHelper
*版本号： v6.0.0.1
*唯一标识：0957f3bb-7462-4ff0-867d-0a8c9411f2eb
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/12 9:33:39
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/12 9:33:39
*修改人： yswenli
*版本号： v6.0.0.1
*描述：
*
*****************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Common.Threading
{
    /// <summary>
    /// 任务辅助类
    /// </summary>
    public static class TaskHelper
    {
        public static Task Run(Action action)
        {
            return Task.Run(action);
        }

        public static Task<T> Run<T>(Func<T> fuc)
        {
            return Task.Run(fuc);
        }

        /// <summary>
        /// LongRunning
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public static Task LongRunning(Action action)
        {
            return Task.Factory.StartNew(action, TaskCreationOptions.LongRunning);
        }
        /// <summary>
        /// LongRunning
        /// </summary>
        /// <param name="action"></param>
        /// <param name="priod">间隔时长</param>
        /// <returns></returns>
        public static Task LongRunning(Action action, int priod)
        {
            return Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    action?.Invoke();

                    Thread.Sleep(priod);
                }
            }, TaskCreationOptions.LongRunning);
        }

        /// <summary>
        /// 指定超时任务
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fuc"></param>
        /// <param name="mill"></param>
        /// <returns></returns>
        public static async Task<T> Run<T>(Func<CancellationToken, T> fuc, int mill = 10 * 1000)
        {
            CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(mill));
            var token = cts.Token;
            return await Task.Run(() => fuc.Invoke(token), token);
        }

        /// <summary>
        /// 任务超时
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="task"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken token)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            CancellationTokenRegistration registration = token.Register(src =>
            {
                ((TaskCompletionSource<bool>)src).TrySetResult(true);
            }, tcs);

            using (registration)
            {
                if (await Task.WhenAny(task, tcs.Task) != task)
                {
                    throw new OperationCanceledException(token);
                }
            }

            return await task;
        }

        /// <summary>
        /// 任务超时
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="task"></param>
        /// <param name="timeout"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<T> WithCancellationTimeout<T>(this Task<T> task, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken))
        {
            CancellationTokenSource timeoutSource = new CancellationTokenSource(timeout);

            CancellationTokenSource linkSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutSource.Token, cancellationToken);

            return await task.WithCancellation(linkSource.Token);

        }

        /// <summary>
        /// 任务超时
        /// </summary>
        /// <param name="action"></param>
        /// <param name="timeout"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task TimeoutAfterAsync(Func<CancellationToken, Task> action, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            var timeoutCts = new CancellationTokenSource(timeout);

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

            try
            {
                await action(linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception)
            {
                var timeoutReached = timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested;

                if (timeoutReached)
                {
                    throw new TimeoutException(exception.Message);
                }

                throw exception;
            }

        }

        /// <summary>
        /// 任务超时
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="action"></param>
        /// <param name="timeout"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<TResult> TimeoutAfterAsync<TResult>(Func<CancellationToken, Task<TResult>> action, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            var timeoutCts = new CancellationTokenSource(timeout);

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);
            try
            {
                return await action(linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception)
            {
                var timeoutReached = timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested;
                if (timeoutReached)
                {
                    throw new TimeoutException(exception.Message);
                }

                throw exception;
            }

        }
    }
}
