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
*修改时间：2026/11/20 14:29:10
*修改人： yswenli
*版本号： v26.11.16.1
*描述：取消源只在超时路径或 action 声明 CancellationToken 形参时才建，其余路径注入默认令牌，省掉每请求一次取消源分配
*
*****************************************************************************/
using SAEA.Common.NameValue;
using SAEA.Http.Model;
using SAEA.MVC.Hosting;
using SAEA.MVC.Results;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
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

        // 调用委托同样属于 action 的固定信息：首次调用时编译并缓存，之后只做一次字典查找。
        // 编译失败时把委托留空即为「不可编译」标记，该 action 之后永久走 MethodInfo.Invoke
        static readonly ConcurrentDictionary<MethodInfo, ActionCallSite> _callSiteCache = new ConcurrentDictionary<MethodInfo, ActionCallSite>();

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
        /// 判断 action 是否声明了 <see cref="CancellationToken"/> 形参：该形参会拿到本次调用的令牌，
        /// 用户代码可能读它的可取消性，这种调用仍要落地真正的取消源
        /// </summary>
        /// <param name="params">action 的形参元数据</param>
        /// <returns>声明了 CancellationToken 形参时返回 true</returns>
        static bool HasCancellationTokenParameter(ParameterInfo[] @params)
        {
            for (var i = 0; i < @params.Length; i++)
            {
                if (@params[i].ParameterType == typeof(CancellationToken)) return true;
            }

            return false;
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
        /// 单个 action 的调用站点：编译委托（为空表示不可编译，走反射回退）与任务结果读取委托
        /// </summary>
        sealed class ActionCallSite
        {
            internal readonly MethodInfo Action;

            internal readonly bool HasTaskResult;

            internal readonly Func<object, object> NoArgInvoker;

            internal readonly Func<object, object[], object> ArgsInvoker;

            internal readonly Func<object, object> ResultReader;

            internal ActionCallSite(MethodInfo action, bool hasTaskResult, Func<object, object> noArgInvoker, Func<object, object[], object> argsInvoker, Func<object, object> resultReader)
            {
                Action = action;
                HasTaskResult = hasTaskResult;
                NoArgInvoker = noArgInvoker;
                ArgsInvoker = argsInvoker;
                ResultReader = resultReader;
            }
        }

        /// <summary>
        /// 取 action 的调用站点，同一 action 只构建一次
        /// </summary>
        /// <param name="action">目标 action</param>
        /// <returns>含编译委托或回退标记的调用站点</returns>
        static ActionCallSite GetCallSite(MethodInfo action)
        {
            if (_callSiteCache.TryGetValue(action, out var cached)) return cached;

            var callSite = CreateCallSite(action);

            _callSiteCache.TryAdd(action, callSite);

            // 并发首次调用可能各建一份等价的委托，以先写入的那份为准，保证后续只读同一副本
            return _callSiteCache.TryGetValue(action, out cached) ? cached : callSite;
        }

        /// <summary>
        /// 构建调用站点：能编译就编译，编译失败只留回退标记
        /// </summary>
        /// <param name="action">目标 action</param>
        /// <returns>构建好的调用站点</returns>
        static ActionCallSite CreateCallSite(MethodInfo action)
        {
            var parameters = GetParameters(action);

            var hasTaskResult = action.ReturnType != null && action.ReturnType.IsGenericType && action.ReturnType.GetGenericTypeDefinition() == typeof(Task<>);

            Func<object, object> noArgInvoker = null;

            Func<object, object[], object> argsInvoker = null;

            Func<object, object> resultReader = null;

            if (CanCompile(action))
            {
                try
                {
                    if (parameters.Length == 0)
                        noArgInvoker = CompileNoArgInvoker(action);
                    else
                        argsInvoker = CompileArgsInvoker(action, parameters);
                }
                catch (Exception)
                {
                    // 编译失败不再重试：委托保持为 null，该 action 后续调用全部走反射回退
                    noArgInvoker = null;

                    argsInvoker = null;
                }

                if (hasTaskResult)
                {
                    try
                    {
                        resultReader = CompileResultReader(action.ReturnType.GetGenericArguments()[0]);
                    }
                    catch (Exception)
                    {
                        // 结果读取编译失败只影响取值方式，回退到 Result 属性反射读取
                        resultReader = null;
                    }
                }
            }

            return new ActionCallSite(action, hasTaskResult, noArgInvoker, argsInvoker, resultReader);
        }

        /// <summary>
        /// 判断 action 是否适合编译委托：不适合的形态直接走反射，避免改变原有语义
        /// </summary>
        /// <param name="action">目标 action</param>
        /// <returns>可以编译时返回 true</returns>
        static bool CanCompile(MethodInfo action)
        {
            if (action == null || action.DeclaringType == null) return false;

            // 非 public 成员或不可见类型在动态代码里可能受访问权限限制，交给反射
            if (!action.IsPublic || !action.DeclaringType.IsVisible) return false;

            // 静态方法需要 null 目标，而调用点始终传入控制器实例，保留反射调用以免改变目标语义
            if (action.IsStatic) return false;

            if (action.ContainsGenericParameters) return false;

            if (action.ReturnType == null || action.ReturnType.IsByRef || action.ReturnType.IsPointer) return false;

            var parameters = GetParameters(action);

            for (var i = 0; i < parameters.Length; i++)
            {
                var parameterType = parameters[i].ParameterType;

                // byref（ref/out）与指针形参无法从 object 拆箱成目标类型，交给反射处理
                if (parameterType.IsByRef || parameterType.IsPointer) return false;
            }

            return true;
        }

        /// <summary>
        /// 编译无参 action 的调用委托
        /// </summary>
        /// <param name="action">目标 action</param>
        /// <returns>入参为控制器实例、返回装箱结果的委托</returns>
        static Func<object, object> CompileNoArgInvoker(MethodInfo action)
        {
            var instance = Expression.Parameter(typeof(object), "instance");

            var call = Expression.Call(Expression.Convert(instance, action.DeclaringType), action);

            return Expression.Lambda<Func<object, object>>(BoxCall(action.ReturnType, call), instance).Compile();
        }

        /// <summary>
        /// 编译有参 action 的调用委托
        /// </summary>
        /// <param name="action">目标 action</param>
        /// <param name="parameters">按声明顺序排列的形参元数据</param>
        /// <returns>入参为控制器实例与实参数组、返回装箱结果的委托</returns>
        static Func<object, object[], object> CompileArgsInvoker(MethodInfo action, ParameterInfo[] parameters)
        {
            var instance = Expression.Parameter(typeof(object), "instance");

            var args = Expression.Parameter(typeof(object[]), "args");

            var callArgs = new Expression[parameters.Length];

            for (var i = 0; i < parameters.Length; i++)
            {
                // 与 MethodInfo.Invoke 一样按声明顺序把实参数组元素转成对应形参类型
                callArgs[i] = Expression.Convert(Expression.ArrayIndex(args, Expression.Constant(i)), parameters[i].ParameterType);
            }

            var call = Expression.Call(Expression.Convert(instance, action.DeclaringType), action, callArgs);

            return Expression.Lambda<Func<object, object[], object>>(BoxCall(action.ReturnType, call), instance, args).Compile();
        }

        /// <summary>
        /// 把 action 调用结果统一转成 object：void 与反射调用一致返回 null
        /// </summary>
        /// <param name="returnType">action 的返回类型</param>
        /// <param name="call">已构造的调用表达式</param>
        /// <returns>返回 object 的表达式</returns>
        static Expression BoxCall(Type returnType, MethodCallExpression call)
        {
            if (returnType == typeof(void))
                return Expression.Block(call, Expression.Constant(null, typeof(object)));

            return Expression.Convert(call, typeof(object));
        }

        /// <summary>
        /// 编译 Task&lt;T&gt; 的结果读取委托
        /// </summary>
        /// <param name="resultType">任务的泛型结果类型</param>
        /// <returns>入参为任务对象、返回装箱结果的委托</returns>
        static Func<object, object> CompileResultReader(Type resultType)
        {
            var taskType = typeof(Task<>).MakeGenericType(resultType);

            var task = Expression.Parameter(typeof(object), "task");

            // 只有任务已完成才会读取结果，这里直接取 Result，不再等待也不再抛任务包装异常
            var result = Expression.Property(Expression.Convert(task, taskType), "Result");

            return Expression.Lambda<Func<object, object>>(Expression.Convert(result, typeof(object)), task).Compile();
        }

        /// <summary>
        /// 执行 action：有编译委托时直接调用，否则回退到反射调用
        /// </summary>
        /// <param name="callSite">本次 action 的调用站点</param>
        /// <param name="obj">控制器实例</param>
        /// <param name="parameters">实参数组；无参 action 为 null</param>
        /// <returns>action 的返回值</returns>
        static object InvokeAction(ActionCallSite callSite, object obj, object[] parameters)
        {
            if (parameters == null || parameters.Length == 0)
            {
                var noArgInvoker = callSite.NoArgInvoker;

                if (noArgInvoker != null)
                {
                    try
                    {
                        return noArgInvoker(obj);
                    }
                    catch (Exception ex)
                    {
                        // 编译委托没有反射的异常外壳，这里补一层同类型外壳，异常链与 500 文案保持与反射调用一致
                        throw new TargetInvocationException(ex);
                    }
                }
            }
            else
            {
                var argsInvoker = callSite.ArgsInvoker;

                if (argsInvoker != null)
                {
                    try
                    {
                        return argsInvoker(obj, parameters);
                    }
                    catch (Exception ex)
                    {
                        throw new TargetInvocationException(ex);
                    }
                }
            }

            // 回退路径与优化前完全一致，包括 TargetInvocationException 的形态
            return callSite.Action.Invoke(obj, parameters);
        }

        /// <summary>
        /// 读取已完成任务的结果：优先用编译委托，委托缺席或取值失败时回退到 Result 属性反射
        /// </summary>
        /// <param name="callSite">本次 action 的调用站点</param>
        /// <param name="task">已完成的任务</param>
        /// <returns>任务结果；非泛型任务为 null</returns>
        static object GetTaskResult(ActionCallSite callSite, Task task)
        {
            var resultReader = callSite.ResultReader;

            if (resultReader != null)
            {
                try
                {
                    return resultReader(task);
                }
                catch (Exception)
                {
                    // 任务类型与声明不符等情况下转为反射读取，取值语义以 Result 属性为准
                }
            }

            var resultProperty = GetResultProperty(task.GetType());

            return resultProperty?.GetValue(task);
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

            // 调用站点（编译委托或回退标记）按 action 缓存，首次请求建好，之后每请求只做一次字典查找
            var callSite = GetCallSite(action);

            int timeoutMs = (int)(GetActionTimeoutSeconds(webConfig) * 1000);

            // 只有显式配置 ActionTimeout 才启用超时包装：默认的 Timeout 兜底长达 180 秒，实际不会命中，
            // 却要让每个请求额外付出一次线程跳转与带超时的等待
            var withTimeout = alwaysTimeout || webConfig.ActionTimeout > 0;

            // 取消源只在本次调用真会交付可用令牌时才建：超时路径会取消它，action 声明了
            // CancellationToken 形参时用户代码会读到它。其余路径没有任何取消者，令牌永不触发，
            // 每请求多建一个取消源只是白付一次分配
            var withToken = withTimeout || HasCancellationTokenParameter(@params);

            var cts = withToken ? new CancellationTokenSource() : null;

            // 没有令牌消费者的路径注入永不触发的默认令牌，与不建取消源时的语义一致
            var token = cts != null ? cts.Token : CancellationToken.None;

            // 标记已交给超时路径处理，避免 finally 提前释放掉还在被 action 观察的取消源
            var handedToTimeout = false;

            try
            {
                // 先落到上下文再调用 action，保证 action 内能读到本次令牌
                if (mvcContext != null)
                    mvcContext.RequestAborted = token;

                // 创建参数列表
                object[] parameters = null;
                if (@params.Length > 0)
                {
                    parameters = ParameterBinder.FillParameters(@params, nameValues, token);
                }

                // 检查是否为异步方法（Task / Task<T>）
                bool isAsync = action.ReturnType != null && typeof(Task).IsAssignableFrom(action.ReturnType);

                if (!withTimeout)
                {
                    // 快路径：不设超时。同步 action 直接调用，异步 action 真正 await，等待期间不占用线程
                    if (isAsync)
                    {
                        var task = (Task)InvokeAction(callSite, obj, parameters);

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
                        if (callSite.HasTaskResult)
                        {
                            data = GetTaskResult(callSite, task);
                        }
                    }
                    else
                    {
                        try
                        {
                            data = InvokeAction(callSite, obj, parameters);
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
                    var task = (Task)InvokeAction(callSite, obj, parameters);

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
                    if (callSite.HasTaskResult)
                    {
                        data = GetTaskResult(callSite, task);
                    }
                }
                else
                {
                    // 处理同步方法
                    var task = Task.Run(() => InvokeAction(callSite, obj, parameters));

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
                // 非超时路径（正常完成、action 同步抛异常、等待时抛 AggregateException）自己释放；
                // 没有取消源的路径无需释放
                if (cts != null && !handedToTimeout) cts.Dispose();
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