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
*命名空间：SAEA.P2P.Ice
*文件名： RouteLadder
*版本号： v26.11.5.1
*唯一标识：4cdcfd4a-f291-4a18-8137-ca6b30643779
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 14:24:38
*描述：ICE 选路阶梯逐级执行器
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 14:26:23
*修改人： yswenli
*版本号： v26.11.5.1
*描述：修正阶段启动耗时未计入剩余总预算
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Common;

namespace SAEA.P2P.Ice
{
    /// <summary>
    /// 选路阶梯逐级执行器：按 UDP 直连 → TCP 直连 → TURN over UDP/TCP/TLS 顺序尝试，成功即停。
    /// 只负责阶段编排与预算，不收集候选、不读写 NAT 行为标签；候选收集与每级实际打洞由调用方在回调里完成。
    /// </summary>
    public sealed class RouteLadder
    {
        private const string LogNode = "RouteLadder";

        private static readonly IceStage[] OrderedStages = new IceStage[]
        {
            IceStage.UdpDirect,
            IceStage.TcpDirect,
            IceStage.TurnUdp,
            IceStage.TurnTcp,
            IceStage.TurnTls
        };

        private readonly IceSessionOptions _options;

        /// <summary>
        /// 以会话配置构造选路阶梯
        /// </summary>
        /// <param name="options">会话配置，含各级阶段预算与会话总预算</param>
        /// <exception cref="ArgumentNullException">配置为空</exception>
        public RouteLadder(IceSessionOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            _options = options;
        }

        /// <summary>
        /// 阶梯的阶段顺序，回退优先级从高到低
        /// </summary>
        public IReadOnlyList<IceStage> Stages
        {
            get { return (IceStage[])OrderedStages.Clone(); }
        }

        /// <summary>
        /// 按阶梯逐级尝试，某级打通立即返回该阶段，失败则进入下一级；五级全失败抛无候选对错误，超出会话总预算抛超时错误
        /// </summary>
        /// <param name="attempt">单级尝试回调：参数为阶段、该级预算与阶段令牌，返回 null 视为该级打通，返回非 null 字符串视为失败原因</param>
        /// <param name="stageStarting">阶段启动回调：在尝试该级前调用，调用方在此补做候选收集；可为 null</param>
        /// <param name="cancellationToken">外层取消令牌，作用到所有阶段</param>
        /// <returns>打通的阶段</returns>
        /// <exception cref="ArgumentNullException">尝试回调为空</exception>
        /// <exception cref="ArgumentException">会话配置无效</exception>
        /// <exception cref="ArgumentOutOfRangeException">会话配置中的预算取值无效</exception>
        /// <exception cref="OperationCanceledException">外层令牌已取消</exception>
        /// <exception cref="P2PException">会话总预算耗尽，或五级全部失败</exception>
        public async Task<IceStage> RunAsync(
            Func<IceStage, TimeSpan, CancellationToken, Task<string>> attempt,
            Func<IceStage, CancellationToken, Task> stageStarting,
            CancellationToken cancellationToken)
        {
            if (attempt == null)
            {
                throw new ArgumentNullException(nameof(attempt));
            }
            _options.Validate();

            var failures = new List<string>();
            var clock = Stopwatch.StartNew();

            for (int i = 0; i < OrderedStages.Length; i++)
            {
                IceStage stage = OrderedStages[i];
                cancellationToken.ThrowIfCancellationRequested();

                // 每级进入时重算剩余总预算，阶段切换与上一级消耗的时间都计入会话预算
                TimeSpan remaining = _options.SessionBudget - clock.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    throw CreateTimeout(stage, failures);
                }

                TimeSpan stageBudget = _options.GetStageBudget(stage);
                if (stageBudget > remaining)
                {
                    stageBudget = remaining;
                }

                using (var stageCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                using (var guard = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    stageCts.CancelAfter(stageBudget);

                    if (stageStarting != null)
                    {
                        try
                        {
                            await stageStarting(stage, stageCts.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                throw;
                            }
                            failures.Add(Describe(stage) + "：阶段启动预算耗尽");
                            continue;
                        }
                    }

                    // stageStarting 可能耗时（第二阶段收集），总预算兜底必须按调用后的真实剩余时间来算
                    remaining = _options.SessionBudget - clock.Elapsed;
                    if (remaining <= TimeSpan.Zero)
                    {
                        throw CreateTimeout(stage, failures);
                    }

                    Task<string> attemptTask;
                    try
                    {
                        attemptTask = attempt(stage, stageBudget, stageCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        failures.Add(Describe(stage) + "：阶段预算耗尽");
                        continue;
                    }

                    string reason;
                    try
                    {
                        // 总预算兜底：阶段实现不响应令牌时也只能在这一级结束，不能把会话拖过总预算
                        Task delayTask = Task.Delay(remaining, guard.Token);
                        Task completed = await Task.WhenAny(attemptTask, delayTask).ConfigureAwait(false);
                        if (completed == delayTask)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            stageCts.Cancel();
                            guard.Cancel();
                            Observe(attemptTask);
                            throw CreateTimeout(stage, failures);
                        }

                        guard.Cancel();
                        reason = await attemptTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        failures.Add(Describe(stage) + "：阶段预算耗尽");
                        continue;
                    }

                    if (reason == null)
                    {
                        return stage;
                    }

                    failures.Add(Describe(stage) + "：" + reason);
                    if (clock.Elapsed >= _options.SessionBudget)
                    {
                        throw CreateTimeout(stage, failures);
                    }
                }
            }

            throw new P2PException(
                ErrorCode.NoCandidatePair,
                "所有选路阶段均失败：" + string.Join("；", failures.ToArray()));
        }

        /// <summary>
        /// 构造会话总预算超时异常，带上停在哪一级与此前失败摘要
        /// </summary>
        /// <param name="stage">停住的阶段</param>
        /// <param name="failures">此前失败摘要</param>
        /// <returns>带 IceTimeout 错误码的异常</returns>
        private P2PException CreateTimeout(IceStage stage, List<string> failures)
        {
            string message = "选路会话超出总预算（"
                + _options.SessionBudget.TotalMilliseconds
                + "ms），停在阶段："
                + Describe(stage);
            if (failures != null && failures.Count > 0)
            {
                message += "；此前失败：" + string.Join("；", failures.ToArray());
            }
            return new P2PException(ErrorCode.IceTimeout, message);
        }

        /// <summary>
        /// 阶段的中文名称，用于错误消息与失败摘要
        /// </summary>
        /// <param name="stage">选路阶段</param>
        /// <returns>阶段中文名；未知取值返回「未知阶段」</returns>
        private static string Describe(IceStage stage)
        {
            switch (stage)
            {
                case IceStage.UdpDirect:
                    return "UDP 直连";
                case IceStage.TcpDirect:
                    return "TCP 直连";
                case IceStage.TurnUdp:
                    return "TURN over UDP";
                case IceStage.TurnTcp:
                    return "TURN over TCP";
                case IceStage.TurnTls:
                    return "TURN over TLS";
                default:
                    return "未知阶段";
            }
        }

        /// <summary>
        /// 观察超时后遗留的阶段任务，避免未处理异常在终结器线程上冒泡
        /// </summary>
        /// <param name="task">已放弃等待的任务</param>
        private static void Observe(Task task)
        {
            if (task == null)
            {
                return;
            }

            task.ContinueWith(
                t =>
                {
                    try
                    {
                        P2PLogHelper.Debug(LogNode, "遗留阶段任务异常：" + t.Exception.GetBaseException().GetType().Name);
                    }
                    catch (Exception)
                    {
                        // 观察目的已达成（读取 Exception 即标记已观察），日志失败不能再抛
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }
}