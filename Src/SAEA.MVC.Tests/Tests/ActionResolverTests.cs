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
*文件名： ActionResolverTests
*版本号： net10.0
*唯一标识：1cc02077-93f9-4f4d-b15d-5592260eb8af
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 15:13:26
*描述：action 解析与 WSController 基类回落的直调用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 15:13:26
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖派生类未 override 时回落到 WSController 声明层，且普通控制器不受影响
*
*****************************************************************************/
using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Controllers;
using SAEA.MVC.Invocation;
using SAEA.MVC.Tests.Controllers;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// action 解析用例：WSController 派生类未 override OpenAsync 时回落到基类声明层。
    /// </summary>
    [TestClass]
    public class ActionResolverTests
    {
        /// <summary>
        /// 派生类未 override OpenAsync 时，GET 与 POST 都回落到 WSController 声明的 OpenAsync。
        /// </summary>
        [TestMethod]
        public void ActionResolver_WsControllerNotOverridden_FallsBackToBaseAsync()
        {
            var getResult = ActionResolver.Resolve(typeof(NoOverrideWsProbe), "open", false);

            Assert.AreEqual(1, getResult.Count, "GET 应回落到基类声明的 OpenAsync");
            Assert.AreEqual("OpenAsync", getResult[0].Name, "回落到的方法名应为 OpenAsync");
            Assert.AreEqual(typeof(WSController), getResult[0].DeclaringType, "回落方法应声明在 WSController 上");

            var postResult = ActionResolver.Resolve(typeof(NoOverrideWsProbe), "open", true);

            Assert.AreEqual(1, postResult.Count, "POST 应回落到基类声明的 OpenAsync");
            Assert.AreEqual("OpenAsync", postResult[0].Name, "回落到的方法名应为 OpenAsync");
            Assert.AreEqual(typeof(WSController), postResult[0].DeclaringType, "回落方法应声明在 WSController 上");

            var basicResult = ActionResolver.Resolve(typeof(BasicController), "open", false);

            Assert.AreEqual(0, basicResult.Count, "普通控制器不应命中 WSController 的 OpenAsync");
        }

        /// <summary>
        /// 未 override 任何 WebSocket 入口的派生控制器。
        /// </summary>
        private class NoOverrideWsProbe : WSController
        {
        }
    }
}
