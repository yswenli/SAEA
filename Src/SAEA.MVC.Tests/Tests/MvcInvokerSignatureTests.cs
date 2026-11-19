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
*文件名： MvcInvokerSignatureTests
*版本号： net10.0
*唯一标识：bc8bdaec-d5cc-4744-8f0b-7e83399e0387
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 15:13:26
*描述：MvcInvoker 公开入口签名的反射断言用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 16:55:31
*修改人： yswenli
*版本号： net10.0
*描述：改为按末位参数类型精确取重载，并新增 string 方法重载的签名断言
*
*****************************************************************************/
using SAEA.MVC.Hosting;
using SAEA.MVC.Results;
using System;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.Common.NameValue;
using SAEA.MVC.Routing;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// MvcInvoker 公开入口签名用例：切分后同步与异步入口的参数表必须保持不变。
    /// </summary>
    [TestClass]
    public class MvcInvokerSignatureTests
    {
        /// <summary>
        /// InvokeResult 与 InvokeResultAsync 均保留 bool 旧重载并新增 string 方法重载，MethodInvoke 已不存在。
        /// </summary>
        [TestMethod]
        public void MvcInvoker_InvokeResult_KeepsPublicSignature()
        {
            // 同名重载会让 GetMethod 抛 AmbiguousMatchException，按末位参数类型精确取
            var syncInvoke = FindInvoke(typeof(MvcInvoker), "InvokeResult", typeof(bool));

            Assert.IsNotNull(syncInvoke, "应存在公开静态方法 InvokeResult(RouteTable, Type, string, NameValueCollection, bool)");
            Assert.IsTrue(syncInvoke.IsPublic && syncInvoke.IsStatic, "InvokeResult 应为公开静态方法");
            Assert.AreEqual(typeof(ActionResult), syncInvoke.ReturnType, "InvokeResult 应返回 ActionResult");
            AssertParameters(syncInvoke.GetParameters(), "InvokeResult", typeof(bool), "bool");

            var syncByMethod = FindInvoke(typeof(MvcInvoker), "InvokeResult", typeof(string));

            Assert.IsNotNull(syncByMethod, "应存在公开静态方法 InvokeResult(RouteTable, Type, string, NameValueCollection, string)");
            Assert.IsTrue(syncByMethod.IsPublic && syncByMethod.IsStatic, "string 重载的 InvokeResult 应为公开静态方法");
            Assert.AreEqual(typeof(ActionResult), syncByMethod.ReturnType, "string 重载的 InvokeResult 应返回 ActionResult");
            AssertParameters(syncByMethod.GetParameters(), "InvokeResult", typeof(string), "string");

            var asyncInvoke = FindInvoke(typeof(MvcInvoker), "InvokeResultAsync", typeof(bool));

            Assert.IsNotNull(asyncInvoke, "应存在公开静态方法 InvokeResultAsync(RouteTable, Type, string, NameValueCollection, bool)");
            Assert.IsTrue(asyncInvoke.IsPublic && asyncInvoke.IsStatic, "InvokeResultAsync 应为公开静态方法");
            Assert.AreEqual(typeof(Task<ActionResult>), asyncInvoke.ReturnType, "InvokeResultAsync 应返回 Task<ActionResult>");
            AssertParameters(asyncInvoke.GetParameters(), "InvokeResultAsync", typeof(bool), "bool");

            var asyncByMethod = FindInvoke(typeof(MvcInvoker), "InvokeResultAsync", typeof(string));

            Assert.IsNotNull(asyncByMethod, "应存在公开静态方法 InvokeResultAsync(RouteTable, Type, string, NameValueCollection, string)");
            Assert.IsTrue(asyncByMethod.IsPublic && asyncByMethod.IsStatic, "string 重载的 InvokeResultAsync 应为公开静态方法");
            Assert.AreEqual(typeof(Task<ActionResult>), asyncByMethod.ReturnType, "string 重载的 InvokeResultAsync 应返回 Task<ActionResult>");
            AssertParameters(asyncByMethod.GetParameters(), "InvokeResultAsync", typeof(string), "string");

            var removed = typeof(MvcInvoker).GetMethod(
                "MethodInvoke",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

            Assert.IsNull(removed, "MethodInvoke 应已删除");
        }

        /// <summary>
        /// 按方法名与末位参数类型取出指定重载。
        /// </summary>
        /// <param name="type">待查找的类型</param>
        /// <param name="methodName">方法名</param>
        /// <param name="lastParameterType">第 5 个参数的类型，用于区分 bool 与 string 重载</param>
        /// <returns>匹配的公开静态方法；没有时返回 null</returns>
        private static MethodInfo FindInvoke(Type type, string methodName, Type lastParameterType)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name != methodName) continue;

                var parameters = method.GetParameters();

                if (parameters.Length == 5 && parameters[4].ParameterType == lastParameterType) return method;
            }

            return null;
        }

        /// <summary>
        /// 断言入口方法的参数表：RouteTable、Type、string、NameValueCollection，末位按重载类型校验。
        /// </summary>
        /// <param name="parameters">入口方法的参数表</param>
        /// <param name="methodName">入口方法名，用于断言消息</param>
        /// <param name="lastParameterType">末位参数的期望类型</param>
        /// <param name="lastLabel">末位参数类型的展示名，用于断言消息</param>
        private static void AssertParameters(ParameterInfo[] parameters, string methodName, Type lastParameterType, string lastLabel)
        {
            Assert.AreEqual(5, parameters.Length, methodName + " 应有 5 个参数");
            Assert.AreEqual(typeof(RouteTable), parameters[0].ParameterType, methodName + " 第 1 个参数应为 RouteTable");
            Assert.AreEqual(typeof(Type), parameters[1].ParameterType, methodName + " 第 2 个参数应为 Type");
            Assert.AreEqual(typeof(string), parameters[2].ParameterType, methodName + " 第 3 个参数应为 string");
            Assert.AreEqual(typeof(NameValueCollection), parameters[3].ParameterType, methodName + " 第 4 个参数应为 NameValueCollection");
            Assert.AreEqual(lastParameterType, parameters[4].ParameterType, methodName + " 第 5 个参数应为 " + lastLabel);
        }
    }
}
