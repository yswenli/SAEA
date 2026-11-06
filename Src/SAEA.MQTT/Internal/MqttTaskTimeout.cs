/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MQTT.Internal
*文件名： MqttTaskTimeout
*版本号： v26.4.23.1
*唯一标识：49e8a793-3fbd-48a9-b8e2-5c7504a945b8
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttTaskTimeout类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;
using SAEA.MQTT.Exceptions;

namespace SAEA.MQTT.Internal
{
    /// <summary>
    /// 为异步操作附加超时控制的辅助类
    /// </summary>
    public static class MqttTaskTimeout
    {
        /// <summary>
        /// 在超时时间内执行异步操作，超时抛出 MqttCommunicationTimedOutException，调用方取消时原样抛出 OperationCanceledException
        /// </summary>
        /// <param name="action">接收取消令牌的异步操作，不能为 null</param>
        /// <param name="timeout">允许的最长执行时间</param>
        /// <param name="cancellationToken">调用方的取消令牌</param>
        /// <returns>异步操作完成时结束的任务</returns>
        public static async Task WaitAsync(Func<CancellationToken, Task> action, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            using (var timeoutCts = new CancellationTokenSource(timeout))
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken))
            {
                try
                {
                    await action(linkedCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException exception)
                {
                    var timeoutReached = timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested;
                    if (timeoutReached)
                    {
                        throw new MqttCommunicationTimedOutException(exception);
                    }

                    throw;
                }
            }
        }

        /// <summary>
        /// 在超时时间内执行带返回值的异步操作，超时抛出 MqttCommunicationTimedOutException，调用方取消时原样抛出 OperationCanceledException
        /// </summary>
        /// <typeparam name="TResult">操作的返回值类型</typeparam>
        /// <param name="action">接收取消令牌的异步操作，不能为 null</param>
        /// <param name="timeout">允许的最长执行时间</param>
        /// <param name="cancellationToken">调用方的取消令牌</param>
        /// <returns>异步操作的返回值</returns>
        public static async Task<TResult> WaitAsync<TResult>(Func<CancellationToken, Task<TResult>> action, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            using (var timeoutCts = new CancellationTokenSource(timeout))
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken))
            {
                try
                {
                    return await action(linkedCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException exception)
                {
                    var timeoutReached = timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested;
                    if (timeoutReached)
                    {
                        throw new MqttCommunicationTimedOutException(exception);
                    }

                    throw;
                }
            }
        }     
    }
}
