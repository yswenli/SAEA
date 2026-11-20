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
*命名空间：SAEA.MVC.Tests
*文件名： ActionInvokerDelegateTests
*版本号： net10.0
*唯一标识：e6efebbc-b88c-43b1-8d9e-5d6c1be541e8
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/20 09:30:00
*描述：action 编译委托调用、任务结果读取与反射回退的用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/20 09:30:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，直调 ActionInvoker 覆盖无参/有参、Task<T> 取值、异常外壳、超时配置下正常完成与回退路径
*
*****************************************************************************/
using System;
using System.Collections;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.Common.NameValue;
using SAEA.Http.Model;
using SAEA.MVC.Invocation;
using SAEA.MVC.Results;
using SAEA.MVC.Tests.Controllers;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// action 编译委托的用例：直调 ActionInvoker，不启端口也不依赖请求上下文；
    /// 既锁定委托绑定的实例与参数顺序，也锁定异常外壳、超时判定与反射回退的形态。
    /// </summary>
    [TestClass]
    public class ActionInvokerDelegateTests
    {
        /// <summary>
        /// 无参同步 action 经编译委托返回 ActionResult，并且读写的是本次传入的控制器实例。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task ActionInvoker_NoArgSyncAction_InvokesPassedInstance()
        {
            var action = GetAction("CallSiteProbe");

            var first = new ActionCallProbeController();

            var result = await ActionInvoker.InvokeAsync(action, first, null, null, new WebConfig()).ConfigureAwait(false);

            Assert.IsInstanceOfType(result, typeof(ContentResult), "同步 action 返回 ActionResult 时应原样带回");

            Assert.AreEqual("probe-call:1", ContentText(result), "首次调用应返回带次数的文本");

            Assert.AreEqual(1, first.Calls, "编译委托必须写回本次传入的实例状态");

            // 换一个实例再调同一个 action：委托不能绑定到上一个实例
            var second = new ActionCallProbeController();

            result = await ActionInvoker.InvokeAsync(action, second, null, null, new WebConfig()).ConfigureAwait(false);

            Assert.AreEqual("probe-call:1", ContentText(result), "编译委托应绑定本次传入的实例，而不是首次调用的实例");

            Assert.AreEqual(1, first.Calls, "第二个实例的调用不应影响第一个实例的状态");

            Assert.AreEqual(1, second.Calls, "第二个实例应各自累计自己的执行次数");
        }

        /// <summary>
        /// 单参 action 经编译委托按顺序拿到实参。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task ActionInvoker_OneParameterAction_PassesArgumentInOrder()
        {
            var action = GetAction("JoinOne");

            var nameValues = new NameValueCollection();

            nameValues.Add("name", "abc");

            var result = await ActionInvoker.InvokeAsync(action, new ActionCallProbeController(), nameValues, null, new WebConfig()).ConfigureAwait(false);

            Assert.AreEqual("one:abc", ContentText(result), "单参 action 应拿到绑定后的参数值");
        }

        /// <summary>
        /// 双参 action 经编译委托按声明顺序拿到两个实参，值类型参数不被错位。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task ActionInvoker_TwoParametersAction_PassesArgumentsInOrder()
        {
            var action = GetAction("JoinTwo");

            var nameValues = new NameValueCollection();

            nameValues.Add("left", "abc");

            nameValues.Add("right", "42");

            var result = await ActionInvoker.InvokeAsync(action, new ActionCallProbeController(), nameValues, null, new WebConfig()).ConfigureAwait(false);

            Assert.AreEqual("abc|42", ContentText(result), "两个实参应按声明顺序传入，不能互换");
        }

        /// <summary>
        /// Task&lt;int&gt; action 的结果读取委托要返回装箱后的值类型结果。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task ActionInvoker_TaskOfValueTypeAction_ReturnsBoxedResult()
        {
            var action = GetAction("NumberAsync");

            var result = await ActionInvoker.InvokeAsync(action, new ActionCallProbeController(), null, null, new WebConfig()).ConfigureAwait(false);

            Assert.IsInstanceOfType(result, typeof(JsonResult), "非 ActionResult 结果按 JsonResult 输出");

            Assert.AreEqual("42", ContentText(result), "Task<int> 的结果应装箱后原值输出");
        }

        /// <summary>
        /// 非泛型 Task action 没有结果可取，data 保持 null。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task ActionInvoker_NonGenericTaskAction_ReturnsNullData()
        {
            var action = GetAction("PingAsync");

            var result = await ActionInvoker.InvokeAsync(action, new ActionCallProbeController(), null, null, new WebConfig()).ConfigureAwait(false);

            // data 为 null 时固定走 JsonResult(data)，说明非泛型任务没有被当成 Task<T> 去读 Result
            Assert.IsInstanceOfType(result, typeof(JsonResult), "非泛型 Task 不读结果，应按 null 数据输出");

            Assert.AreEqual("null", ContentText(result), "null 数据序列化后为 null 文本");
        }

        /// <summary>
        /// 同步 action 抛异常时仍抛 AggregateException，内层保留 TargetInvocationException 外壳。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task ActionInvoker_SyncActionThrows_KeepsTargetInvocationShell()
        {
            var action = GetAction("ThrowsProbe");

            try
            {
                await ActionInvoker.InvokeAsync(action, new ActionCallProbeController(), null, null, new WebConfig()).ConfigureAwait(false);

                Assert.Fail("action 抛异常时 InvokeAsync 必须抛出 AggregateException");
            }
            catch (AggregateException ex)
            {
                // 编译委托不会自动带反射外壳，实现主动补了一层，这里锁定该选择：
                // 外壳变了会让 500 文案与 Unwrap 结果一起变化
                Assert.IsInstanceOfType(ex.InnerException, typeof(TargetInvocationException), "AggregateException 内层应保留 TargetInvocationException 外壳");

                Assert.IsInstanceOfType(ex.InnerException.InnerException, typeof(InvalidOperationException), "外壳内层应是用例 action 抛出的用户异常");

                Assert.AreEqual("probe-boom", ex.InnerException.InnerException.Message, "用户异常的消息必须原样保留");

                Assert.IsInstanceOfType(ActionInvoker.Unwrap(ex), typeof(TargetInvocationException), "Unwrap 后仍应是外壳类型，与反射调用时一致");
            }
        }

        /// <summary>
        /// 配置 ActionTimeout 后，快速完成的同步 action 不能被误判为超时。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task ActionInvoker_SyncActionWithActionTimeout_CompletesWithoutTimeout()
        {
            var action = GetAction("FastSync");

            var webConfig = new WebConfig { ActionTimeout = 5 };

            var result = await ActionInvoker.InvokeAsync(action, new ActionCallProbeController(), null, null, webConfig).ConfigureAwait(false);

            Assert.AreEqual(HttpStatusCode.OK, result.Status, "正常完成的同步 action 不应返回 408");

            Assert.AreEqual("probe-fast-sync", ContentText(result), "超时包装不能改变同步 action 的返回值");
        }

        /// <summary>
        /// 配置 ActionTimeout 后，快速完成的异步 action 不能被误判为超时。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task ActionInvoker_AsyncActionWithActionTimeout_CompletesWithoutTimeout()
        {
            var action = GetAction("FastAsync");

            var webConfig = new WebConfig { ActionTimeout = 5 };

            var result = await ActionInvoker.InvokeAsync(action, new ActionCallProbeController(), null, null, webConfig).ConfigureAwait(false);

            Assert.AreEqual(HttpStatusCode.OK, result.Status, "正常完成的异步 action 不应返回 408");

            Assert.AreEqual("probe-fast-async", ContentText(result), "超时包装不能改变 Task<string> 的结果");
        }

        /// <summary>
        /// 静态 action 不能编译委托，回退到反射调用仍要返回正确结果，且回退标记只记录一次。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task ActionInvoker_StaticAction_FallsBackToReflection()
        {
            var action = typeof(ActionCallProbeController).GetMethod("StaticEcho", BindingFlags.Public | BindingFlags.Static);

            Assert.IsNotNull(action, "探针控制器应存在静态 action");

            var instance = new ActionCallProbeController();

            var first = await ActionInvoker.InvokeAsync(action, instance, null, null, new WebConfig()).ConfigureAwait(false);

            var second = await ActionInvoker.InvokeAsync(action, instance, null, null, new WebConfig()).ConfigureAwait(false);

            Assert.AreEqual("probe-static", ContentText(first), "回退路径必须返回与反射调用一致的结果");

            Assert.AreEqual("probe-static", ContentText(second), "第二次调用仍应走回退并返回同样结果");

            // 回退标记留在调用站点缓存里：条目存在且两个调用委托都为 null，
            // 后续请求直接按标记走 MethodInfo.Invoke，不会再尝试编译
            var cacheField = typeof(ActionInvoker).GetField("_callSiteCache", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(cacheField, "ActionInvoker 应按 action 缓存调用站点");

            var cache = cacheField.GetValue(null) as IDictionary;

            Assert.IsNotNull(cache, "调用站点缓存应可枚举");

            Assert.IsTrue(cache.Contains(action), "回退场景也必须写入调用站点，避免每次调用都重新尝试编译");

            var callSite = cache[action];

            var callSiteType = callSite.GetType();

            Assert.IsNull(callSiteType.GetField("NoArgInvoker", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(callSite), "不可编译的 action 不应留下 0 参委托");

            Assert.IsNull(callSiteType.GetField("ArgsInvoker", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(callSite), "不可编译的 action 不应留下有参委托");
        }

        /// <summary>
        /// 按名称取探针控制器的公开实例 action。
        /// </summary>
        /// <param name="name">action 名称</param>
        /// <returns>action 的方法元数据</returns>
        static MethodInfo GetAction(string name)
        {
            var action = typeof(ActionCallProbeController).GetMethod(name, BindingFlags.Public | BindingFlags.Instance);

            Assert.IsNotNull(action, "探针控制器应存在 action：" + name);

            return action;
        }

        /// <summary>
        /// 读 ActionResult 的文本正文。
        /// </summary>
        /// <param name="result">action 结果</param>
        /// <returns>按结果自身编码解出的正文；无正文时为 null</returns>
        static string ContentText(ActionResult result)
        {
            if (result.Content == null) return null;

            return result.ContentEncoding.GetString(result.Content);
        }
    }
}