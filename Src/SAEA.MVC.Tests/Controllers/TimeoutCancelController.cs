/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.MVC.Tests.Controllers
*文件名： TimeoutCancelController
*版本号： net10.0
*唯一标识：e460fa2c-e36a-4721-973a-d424ad192624
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/17 17:44:45
*描述：超时取消与 405 文案用例的控制器
*
*=====================================================================
*修改标记
*修改时间：2026/11/17 18:40:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 405 文案探针（属性路由与两段式）、混合参数令牌注入探针与同步/异步超时取消探针；ArmShortTimeout 改为只设 WebConfig.ActionTimeout
*
*****************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MVC.Tests.Controllers
{
    /// <summary>
    /// 超时取消与 405 文案用例的控制器：数字末段的动词限定入口用于验证文案保留完整路径，
    /// 慢动作在超时取消后置位静态信号，用例据此断言令牌确实被取消。
    /// </summary>
    public class TimeoutCancelController : Controller
    {
        static TaskCompletionSource<bool> _asyncCancelled = CreateSignals();

        static readonly ManualResetEventSlim _syncCancelled = new ManualResetEventSlim(false);

        /// <summary>
        /// 异步动作观察到取消后的信号：令牌被取消时完成，值为 true。
        /// </summary>
        public static TaskCompletionSource<bool> AsyncCancelled
        {
            get { return _asyncCancelled; }
        }

        /// <summary>
        /// 同步动作观察到取消后的信号：令牌被取消时置位。
        /// </summary>
        public static ManualResetEventSlim SyncCancelled
        {
            get { return _syncCancelled; }
        }

        /// <summary>
        /// 重置两个静态信号，保证同一用例重复运行也能通过。
        /// </summary>
        public static void ResetSignals()
        {
            _asyncCancelled = CreateSignals();

            _syncCancelled.Reset();
        }

        /// <summary>
        /// 数字末段的只允许 GET 入口，用于验证 405 文案保留完整请求路径。
        /// </summary>
        /// <param name="id">路径中的 {id} 段</param>
        /// <returns>带 id 的回显文本</returns>
        [Route("/timeoutcancel/verb405/{id}")]
        [HttpGet]
        public string Verb405(string id)
        {
            return "timeoutcancel-verb405:" + id;
        }

        /// <summary>
        /// 两段式地址的只允许 GET 入口，用于验证两段式 405 文案保留完整请求路径。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpGet]
        public string GetOnlyEcho()
        {
            return "timeoutcancel-getonly-echo";
        }

        /// <summary>
        /// 令牌夹在两个普通形参之间的入口，用于验证框架按声明顺序把令牌插回原位。
        /// </summary>
        /// <param name="name">请求参数 name</param>
        /// <param name="token">框架注入的本次请求取消令牌</param>
        /// <param name="id">请求参数 id</param>
        /// <returns>名称、id、令牌可用性与是否已取消的拼接文本</returns>
        [HttpGet]
        public string MixedParameters(string name, CancellationToken token, int id)
        {
            return name + "|" + id + "|" + (token.CanBeCanceled ? "True" : "False") + "|" + (token.IsCancellationRequested ? "True" : "False");
        }

        /// <summary>
        /// 把本宿主的动作超时改成 1 秒：动作超时与连接空闲回收已分开，只调动作超时不会回收连接，
        /// 无需再动 Timeout（连接空闲回收时间）。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpGet]
        public string ArmShortTimeout()
        {
            HttpContext.Current.WebConfig.ActionTimeout = 1;

            return "timeoutcancel-armed";
        }

        /// <summary>
        /// 带取消令牌的异步慢动作：令牌取消后置位异步信号，再把取消异常抛回框架。
        /// </summary>
        /// <param name="token">框架注入的本次请求取消令牌</param>
        /// <returns>未取消时返回固定文本</returns>
        public async Task<string> SlowAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), token).ConfigureAwait(false);

                return "timeoutcancel-slowasync-done";
            }
            catch (OperationCanceledException)
            {
                _asyncCancelled.TrySetResult(true);

                throw;
            }
        }

        /// <summary>
        /// 同步慢动作：轮询上下文里的取消令牌，最多等 10 秒，取消后置位同步信号并返回。
        /// </summary>
        /// <returns>取消后返回取消标记，否则返回未取消标记</returns>
        public string SlowSync()
        {
            var context = HttpContext.Current as SAEA.MVC.HttpContext;

            var deadline = DateTime.UtcNow.AddSeconds(10);

            while (DateTime.UtcNow < deadline)
            {
                if (context != null && context.RequestAborted.IsCancellationRequested)
                {
                    _syncCancelled.Set();

                    return "timeoutcancel-slowsync-cancelled";
                }

                Thread.Sleep(50);
            }

            return "timeoutcancel-slowsync-timeout";
        }

        /// <summary>
        /// 创建可跨线程完成且不内联续体的信号源。
        /// </summary>
        /// <returns>新的信号源</returns>
        static TaskCompletionSource<bool> CreateSignals()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}