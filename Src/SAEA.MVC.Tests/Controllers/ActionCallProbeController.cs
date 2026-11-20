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
*文件名： ActionCallProbeController
*版本号： net10.0
*唯一标识：e92cd547-56e1-4909-8f03-0fa1ac91a5ba
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/20 09:30:00
*描述：编译委托调用与回退路径用例的探针控制器
*
*=====================================================================
*修改标记
*修改时间：2026/11/20 09:30:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，提供无参/有参/异步/抛异常/静态回退等形态的 action 供 ActionInvoker 直调用例使用
*
*****************************************************************************/
using System;
using System.Threading.Tasks;

using SAEA.MVC.Controllers;
using SAEA.MVC.Results;

namespace SAEA.MVC.Tests.Controllers
{
    /// <summary>
    /// 编译委托调用与回退路径用例的探针控制器：所有 action 都不读取请求上下文，
    /// 用例直接调用 ActionInvoker，不需要启动监听端口。
    /// </summary>
    public class ActionCallProbeController : Controller
    {
        int _calls;

        /// <summary>
        /// 无参 action 已执行的次数，用于确认编译委托绑定的是本次传入的实例
        /// </summary>
        public int Calls
        {
            get { return _calls; }
        }

        /// <summary>
        /// 无参同步 action：自增实例状态并返回带次数的文本结果。
        /// </summary>
        /// <returns>带执行次数的文本结果</returns>
        public ActionResult CallSiteProbe()
        {
            _calls++;

            return new ContentResult("probe-call:" + _calls);
        }

        /// <summary>
        /// 单参 action：回显唯一参数。
        /// </summary>
        /// <param name="name">请求参数 name</param>
        /// <returns>带前缀的回显文本</returns>
        public string JoinOne(string name)
        {
            return "one:" + name;
        }

        /// <summary>
        /// 双参 action：按声明顺序拼接两个参数，用于确认实参没有错位。
        /// </summary>
        /// <param name="left">请求参数 left</param>
        /// <param name="right">请求参数 right</param>
        /// <returns>两个参数的拼接文本</returns>
        public string JoinTwo(string left, int right)
        {
            return left + "|" + right;
        }

        /// <summary>
        /// 返回 Task&lt;int&gt; 的 action：结果类型是值类型，用于确认装箱后的值正确。
        /// </summary>
        /// <returns>结果为 42 的任务</returns>
        public Task<int> NumberAsync()
        {
            return Task.FromResult(42);
        }

        /// <summary>
        /// 返回非泛型 Task 的 action：没有结果可取。
        /// </summary>
        /// <returns>已完成的任务</returns>
        public Task PingAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 同步抛异常的 action：用于锁定编译路径的异常外壳形态。
        /// </summary>
        /// <returns>不会正常返回</returns>
        public string ThrowsProbe()
        {
            throw new InvalidOperationException("probe-boom");
        }

        /// <summary>
        /// 带超时配置时也要正常完成的同步 action。
        /// </summary>
        /// <returns>固定文本</returns>
        public string FastSync()
        {
            return "probe-fast-sync";
        }

        /// <summary>
        /// 带超时配置时也要正常完成的异步 action：内部真正让出线程，确保走完整的等待与计时器路径。
        /// </summary>
        /// <returns>固定文本的任务</returns>
        public async Task<string> FastAsync()
        {
            await Task.Delay(1).ConfigureAwait(false);

            return "probe-fast-async";
        }

        /// <summary>
        /// 静态 action：路由不会选中静态方法，编译委托也不支持静态目标，因此这条路径固定走反射回退。
        /// </summary>
        /// <returns>固定文本</returns>
        public static string StaticEcho()
        {
            return "probe-static";
        }
    }
}