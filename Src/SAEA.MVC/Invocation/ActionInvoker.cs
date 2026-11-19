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
*命名空间：SAEA.MVC.Invocation
*文件名： ActionInvoker
*版本号： v26.11.16.1
*唯一标识：b402f4eb-7cf9-4814-9e47-86ab8a4a177f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 13:58:19
*描述：action 调用、超时与取消收尾
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 19:15:49
*修改人： yswenli
*版本号： v26.11.16.1
*描述：形参与 Result 属性改为按 action 与任务类型缓存，每请求不再重复取反射元数据
*
*****************************************************************************/
using SAEA.Common.NameValue;
using SAEA.Http.Model;
using SAEA.MVC.Hosting;
using SAEA.MVC.Results;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MVC.Invocation
{
    /// <summary>
    /// action 执行：同步与异步 action 的调用、超时包装与取消源收尾
    /// </summary>
    internal static class ActionInvoker
    {
        // 形参与 Result 属性都属于 action 的固定元数据：GetParameters 每次调用都会复制新数组，
        // GetProperty 每次调用都要重新筛选成员，按 MethodInfo / 任务类型缓存后只读复用
        static readonly ConcurrentDictionary<MethodInfo, ParameterInfo[]> _parametersCache = new ConcurrentDictionary<MethodInfo, ParameterInfo[]>();

        static readonly ConcurrentDictionary<Type, PropertyInfo> _resultPropertyCache = new ConcurrentDictionary<Type, PropertyInfo>();

        /// <summary>
        /// 取 action 的形参元数据，同一 action 只获取一次
        /// </summary>
        /// <param name="action">目标 action</param>
        /// <returns>按声明顺序排列的形参数组</returns>
        static ParameterInfo[] GetParameters(MethodInfo action)
        {
            if (_parametersCache.TryGetValue(action, out var cached)) return cached;

            var parameters = action.GetParameters();

            _parametersCache.TryAdd(action, parameters);

            return parameters;
        }

        /// <summary>
        /// 取任务对象的 Result 属性，同一任务类型只查找一次
        /// </summary>
        /// <param name="taskType">任务运行期类型</param>
        /// <returns>Result 属性；非泛型任务或找不到时为 null</returns>
        static PropertyInfo GetResultProperty(Type taskType)
        {
            if (_resultPropertyCache.TryGetValue(taskType, out var cached)) return cached;

            var property = taskType.GetProperty("Result");

            _resultPropertyCache.TryAdd(taskType, property);

            return property;
        }

        /// <summary>
        /// 异步代理执行方法：未启用超时时直接等待 action 完成；启用超时时等待完成或到点返回 408
        /// </summary>
        /// <param name="action">目标 action</param>
        /// <param name="obj">控制器实例</param>
        /// <param name="nameValues">请求参数</param>
        /// <param name="mvcContext">本次请求的 MVC 上下文，用于写入取消令牌；可能为 null</param>
        /// <param name="webConfig">本次请求的服务器配置，用于判定是否启用超时</param>
        /// <param name="alwaysTimeout">是否无视配置强制启用超时包装，WebSocket 入口依赖 408 收尾必须传 true</param>
        /// <returns>action 结果；超时返回 408 结果</returns>
        internal static async Task<ActionResult> InvokeAsync(MethodInfo action, object obj, NameValueCollection nameValues, SAEA.MVC.Hosting.HttpContext mvcContext, WebConfig webConfig, bool alwaysTimeout = false)
        {
            ActionResult result = null;
            object data = null;

            var @params = GetParameters(action);
            int timeoutMs = (int)(GetActionTimeoutSeconds(webConfig) * 1000);

            // 只有显式配置 ActionTimeout 才启用超时包装：默认的 Timeout 兜底长达 180 秒，实际不会命中，
            // 却要让每个请求额外付出一次线程跳转与带超时的等待
            var withTimeout = alwaysTimeout || webConfig.ActionTimeout > 0;

            // 每次调用一个取消源：超时前由 finally 释放，超时后交给任务观察回调释放
            var cts = new CancellationTokenSource();

            // 标记已交给超时路径处理，避免 finally 提前释放掉还在被 action 观察的取消源
            var handedToTimeout = false;

            try
            {
                // 先落到上下文再调用 action，保证 action 内能读到本次令牌
                if (mvcContext != null)
                    mvcContext.RequestAborted = cts.Token;

                // 创建参数列表
                object[] parameters = null;
                if (@params.Length > 0)
                {
                    parameters = ParameterBinder.FillParameters(@params, nameValues, cts.Token);
                }

                // 检查是否为异步方法（Task / Task<T>）
                bool isAsync = action.ReturnType != null && typeof(Task).IsAssignableFrom(action.ReturnType);

                if (!withTimeout)
                {
                    // 快路径：不设超时。同步 action 直接调用，异步 action 真正 await，等待期间不占用线程
                    if (isAsync)
                    {
                        var task = (Task)action.Invoke(obj, parameters);

                        try
                        {
                            await task.ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            // 旧路径由 Task.Wait 抛出 AggregateException，这里按同样的形态抛回，
                            // 保证 OnException 收到的异常类型与 500 文案逐字不变
                            throw new AggregateException(ex);
                        }

                        // 任务完成，Task<T> 取 Result，非泛型 Task 没有结果
                        if (action.ReturnType.IsGenericType && action.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                        {
                            var resultProperty = GetResultProperty(task.GetType());
                            data = resultProperty?.GetValue(task);
                        }
                    }
                    else
                    {
                        try
                        {
                            data = action.Invoke(obj, parameters);
                        }
                        catch (Exception ex)
                        {
                            // 同上：旧路径经 Task.Run + Wait 抛 AggregateException，异常形态必须保持一致
                            throw new AggregateException(ex);
                        }
                    }
                }
                else if (isAsync)
                {
                    // 处理异步方法
                    var task = (Task)action.Invoke(obj, parameters);

                    // 等待任务完成或超时
                    if (!await WaitWithTimeoutAsync(task, timeoutMs).ConfigureAwait(false))
                    {
                        // 超时
                        handedToTimeout = true;

                        // Cancel() 在调用线程同步执行用户注册的回调，回调阻塞会把 408 拖后甚至自锁，因此放到线程池执行
                        _ = Task.Run(() => CancelQuietly(cts));

                        ObserveAndDispose(task, cts);

                        return new ContentResult($"{action.Name}: The execution of this asynchronous method has timed out", System.Net.HttpStatusCode.RequestTimeout);
                    }

                    // 与原 task.Wait(timeoutMs) 返回 true 后一致：失败任务按 AggregateException 抛出
                    RethrowLikeWait(task);

                    // 任务完成，Task<T> 取 Result，非泛型 Task 没有结果
                    if (action.ReturnType.IsGenericType && action.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                    {
                        var resultProperty = GetResultProperty(task.GetType());
                        data = resultProperty?.GetValue(task);
                    }
                }
                else
                {
                    // 处理同步方法
                    var task = Task.Run(() => action.Invoke(obj, parameters));

                    // 等待任务完成或超时
                    if (!await WaitWithTimeoutAsync(task, timeoutMs).ConfigureAwait(false))
                    {
                        // 超时
                        handedToTimeout = true;

                        // Cancel() 在调用线程同步执行用户注册的回调，回调阻塞会把 408 拖后甚至自锁，因此放到线程池执行
                        _ = Task.Run(() => CancelQuietly(cts));

                        ObserveAndDispose(task, cts);

                        return new ContentResult($"{action.Name}: The execution of this method has timed out", System.Net.HttpStatusCode.RequestTimeout);
                    }

                    // 任务完成，获取结果（已失败时与原 task.Wait 一样抛 AggregateException）
                    data = task.Result;
                }
            }
            finally
            {
                // 非超时路径（正常完成、action 同步抛异常、等待时抛 AggregateException）自己释放
                if (!handedToTimeout) cts.Dispose();
            }

            // 处理各种返回类型
            if (data is ActionResult)
            {
                result = data as ActionResult;
            }
            else if (data is string)
            {
                result = new ContentResult(data as string);
            }
            else
            {
                result = new JsonResult(data);
            }

            return result;
        }

        /// <summary>
        /// 解包 Task.Wait 抛出的 AggregateException
        /// </summary>
        internal static Exception Unwrap(Exception exception)
        {
            while (exception is AggregateException aggregate && aggregate.InnerException != null)
            {
                exception = aggregate.InnerException;
            }

            return exception;
        }

        /// <summary>
        /// 取本次请求的 action 执行超时秒数，仅在启用超时路径时使用
        /// </summary>
        /// <param name="webConfig">本次请求的服务器配置</param>
        /// <returns>ActionTimeout 大于 0 时用它，否则沿用连接空闲回收的 Timeout</returns>
        internal static double GetActionTimeoutSeconds(WebConfig webConfig)
        {
            // 0 与负数都视为未单独配置：需要超时收尾的入口（显式配置 ActionTimeout 的动作与 WebSocket 入口）
            // 继续沿用 Timeout 兜底
            return webConfig.ActionTimeout > 0 ? webConfig.ActionTimeout : webConfig.Timeout;
        }

        /// <summary>
        /// 等待任务完成或到达超时；结束时立即释放计时器，避免长超时配置下计时器常驻
        /// </summary>
        /// <param name="task">待等待的任务</param>
        /// <param name="timeoutMs">最长等待毫秒数</param>
        /// <returns>任务在超时前完成返回 true，超时返回 false</returns>
        static async Task<bool> WaitWithTimeoutAsync(Task task, int timeoutMs)
        {
            // 已完成的任务不必建计时器：原 Task.Wait 在这种情况下也是立即返回
            if (task.IsCompleted)
                return true;

            using (var delayCts = new CancellationTokenSource())
            {
                var delayTask = Task.Delay(timeoutMs, delayCts.Token);

                var completed = await Task.WhenAny(task, delayTask).ConfigureAwait(false);

                if (ReferenceEquals(completed, task))
                {
                    // 任务先完成，主动取消计时器；被取消的延迟任务不会产生未观察异常
                    delayCts.Cancel();
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// 按 Task.Wait 的异常形态重新抛出：失败任务抛 AggregateException，取消任务包一层 TaskCanceledException
        /// </summary>
        /// <param name="task">已结束的任务</param>
        static void RethrowLikeWait(Task task)
        {
            if (task.IsFaulted)
                throw task.Exception;

            if (task.IsCanceled)
                throw new AggregateException(new TaskCanceledException(task));
        }

        /// <summary>
        /// 取消并吞掉竞态异常：取消与任务完成后的释放可能并发，取消回调本身也可能抛异常
        /// </summary>
        /// <param name="cts">待取消的取消源</param>
        static void CancelQuietly(CancellationTokenSource cts)
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // 任务已结束并释放了取消源，此时没有回调需要执行，无需再取消
            }
            catch (AggregateException)
            {
                // 令牌回调抛出的异常不能顶掉 408 响应
            }
        }

        /// <summary>
        /// 观察超时任务的异常并在其结束后释放取消源
        /// </summary>
        /// <param name="task">已超时的任务</param>
        /// <param name="cts">本次调用的取消源</param>
        static void ObserveAndDispose(Task task, CancellationTokenSource cts)
        {
            task.ContinueWith(t =>
            {
                // 读一次 Exception 把异常标记为已观察，避免未观察异常在终结时抛出
                var ignored = t.Exception;

                cts.Dispose();
            }, TaskScheduler.Default);
        }
    }
}