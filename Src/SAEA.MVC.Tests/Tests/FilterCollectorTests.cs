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
*文件名： FilterCollectorTests
*版本号： net10.0
*唯一标识：a0153efc-0c6d-45bf-bcb9-7bd90cf7d634
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 15:13:26
*描述：类级与方法级过滤器按 Order 升序收集的直调用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 15:13:26
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖类级与方法级过滤器各自按 Order 升序，以及无过滤器时返回 null
*
*****************************************************************************/
using SAEA.MVC.Attributes;
using SAEA.MVC.Controllers;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Invocation;
using SAEA.MVC.Tests.Attributes;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// 过滤器收集用例：类级与方法级过滤器各自按 Order 升序返回。
    /// </summary>
    [TestClass]
    public class FilterCollectorTests
    {
        /// <summary>
        /// 类级两个过滤器按 Order 升序，方法级两个过滤器同样按 Order 升序；无过滤器时返回 null。
        /// </summary>
        [TestMethod]
        public void FilterCollector_ClassAndMethodFilters_SortedByOrder()
        {
            var classFilters = FilterCollector.CollectClassFilters(typeof(OrderedFilterProbe));

            Assert.IsNotNull(classFilters, "类级过滤器不应为空");
            Assert.AreEqual(2, classFilters.Count, "类级应收集到两个过滤器");
            CollectionAssert.AreEqual(new[] { -1, 9 }, classFilters.Select(b => b.Order).ToArray(), "类级过滤器应按 Order 升序");

            var method = typeof(OrderedFilterProbe).GetMethod("Run");

            var actionFilters = FilterCollector.CollectActionFilters(method);

            Assert.IsNotNull(actionFilters, "方法级过滤器不应为空");
            Assert.AreEqual(2, actionFilters.Count, "方法级应收集到两个过滤器");
            CollectionAssert.AreEqual(new[] { -1, 7 }, actionFilters.Select(b => b.Order).ToArray(), "方法级过滤器应按 Order 升序");

            Assert.IsNull(FilterCollector.CollectClassFilters(typeof(NoFilterProbe)), "类上没有过滤器时应返回 null");
            Assert.IsNull(FilterCollector.CollectActionFilters(typeof(NoFilterProbe).GetMethod("Run")), "方法上没有过滤器时应返回 null");
        }

        /// <summary>
        /// 类上与 action 上各声明两个过滤器，声明顺序与 Order 相反，用于验证收集后确实排序。
        /// </summary>
        [OrderRecordingFilter(Order = 9)]
        [OrderRecordingFilter(Order = -1)]
        private class OrderedFilterProbe : Controller
        {
            /// <summary>
            /// 目标 action。
            /// </summary>
            /// <returns>空文本</returns>
            [OrderRecordingFilter(Order = 7)]
            [OutputCache(10)]
            public string Run()
            {
                return "";
            }
        }

        /// <summary>
        /// 类上与 action 上都没有过滤器的控制器。
        /// </summary>
        private class NoFilterProbe : Controller
        {
            /// <summary>
            /// 目标 action。
            /// </summary>
            /// <returns>空文本</returns>
            public string Run()
            {
                return "";
            }
        }
    }
}
