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
*文件名： RouteTableIndexTests
*版本号： net10.0
*唯一标识：104439af-f1d6-442f-926e-4a5160e2de00
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/20 16:20:00
*描述：控制器名索引与路由缓存键的单元用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/20 16:20:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖 FindController 的大小写、裸名与后缀、列表顺序、重新赋值与原地追加，以及结构键缓存的复用与 null 缓存
*
*****************************************************************************/
using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Controllers;
using SAEA.MVC.Routing;
using SAEA.MVC.Tests.Controllers;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// 控制器名索引与路由缓存键的用例：不启端口，只验证查找语义与条目复用。
    /// </summary>
    [TestClass]
    public class RouteTableIndexTests
    {
        List<Type> _originalTypes;

        /// <summary>
        /// 保存当前静态类型列表，供用例结束后还原。
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            _originalTypes = RouteTable.Types;
        }

        /// <summary>
        /// 还原静态类型列表，避免用例之间互相污染。
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            RouteTable.Types = _originalTypes;
        }

        /// <summary>
        /// 控制器名大小写不影响命中结果。
        /// </summary>
        [TestMethod]
        public void FindController_NameCaseInsensitive_ReturnsControllerType()
        {
            RouteTable.Types = new List<Type> { typeof(BasicController) };

            var table = new RouteTable();

            Assert.AreSame(typeof(BasicController), table.FindController("basic"));
            Assert.AreSame(typeof(BasicController), table.FindController("BASIC"));
        }

        /// <summary>
        /// 裸名与带 Controller 后缀的名字命中同一类型。
        /// </summary>
        [TestMethod]
        public void FindController_BareNameAndSuffixedName_ReturnSameType()
        {
            RouteTable.Types = new List<Type> { typeof(BenchProbeController) };

            var table = new RouteTable();

            var bare = table.FindController("BenchProbe");
            var suffixed = table.FindController("BenchProbeController");

            Assert.AreSame(typeof(BenchProbeController), bare);
            Assert.AreSame(typeof(BenchProbeController), suffixed);
            Assert.AreSame(bare, suffixed);
        }

        /// <summary>
        /// 裸名冲突时取列表顺序里第一个命中的类型，带后缀的全名仍命中自身。
        /// </summary>
        [TestMethod]
        public void FindController_SameBareName_ReturnsFirstInListOrder()
        {
            var table = new RouteTable();

            RouteTable.Types = new List<Type> { typeof(IndexOrderProbeController), typeof(IndexOrderProbe) };

            Assert.AreSame(typeof(IndexOrderProbeController), table.FindController("IndexOrderProbe"));
            Assert.AreSame(typeof(IndexOrderProbeController), table.FindController("IndexOrderProbeController"));

            RouteTable.Types = new List<Type> { typeof(IndexOrderProbe), typeof(IndexOrderProbeController) };

            Assert.AreSame(typeof(IndexOrderProbe), table.FindController("IndexOrderProbe"));
            Assert.AreSame(typeof(IndexOrderProbeController), table.FindController("IndexOrderProbeController"));
        }

        /// <summary>
        /// 重新赋值 Types 后索引跟随新列表，不再返回旧列表里的类型。
        /// </summary>
        [TestMethod]
        public void FindController_TypesReassigned_IndexFollowsNewList()
        {
            var table = new RouteTable();

            RouteTable.Types = new List<Type> { typeof(BasicController) };

            Assert.AreSame(typeof(BasicController), table.FindController("Basic"));

            RouteTable.Types = new List<Type> { typeof(FastPathController) };

            Assert.IsNull(table.FindController("Basic"));
            Assert.AreSame(typeof(FastPathController), table.FindController("FastPath"));
        }

        /// <summary>
        /// 绕过属性 setter 原地追加后，长度不一致触发兜底重建，新类型可被找到。
        /// </summary>
        [TestMethod]
        public void FindController_TypesAddedInPlace_FindsNewType()
        {
            var table = new RouteTable();

            RouteTable.Types = new List<Type> { typeof(BasicController) };

            Assert.IsNull(table.FindController("FastPath"));

            RouteTable.Types.Add(typeof(FastPathController));

            Assert.AreSame(typeof(FastPathController), table.FindController("FastPath"));
        }

        /// <summary>
        /// 同一控制器、action 与动词连续两次取缓存返回同一实例，未声明动词的 action 可用其它动词键直接命中同一实例。
        /// </summary>
        [TestMethod]
        public void GetOrAdd_SameControllerActionMethod_ReturnsSameEntry()
        {
            var table = new RouteTable();

            var first = table.GetOrAdd(typeof(CacheKeyProbeController), "Plain", "GET");
            var second = table.GetOrAdd(typeof(CacheKeyProbeController), "Plain", "GET");

            Assert.IsNotNull(first);
            Assert.AreSame(first, second);

            var post = table.GetOrAdd(typeof(CacheKeyProbeController), "Plain", "POST");

            Assert.AreSame(first, post);
            Assert.AreSame(first.Instance, post.Instance);
        }

        /// <summary>
        /// 未知 action 返回 null，第二次调用仍命中缓存的 null，不回退成非 null。
        /// </summary>
        [TestMethod]
        public void GetOrAdd_UnknownAction_ReturnsNullTwice()
        {
            var table = new RouteTable();

            Assert.IsNull(table.GetOrAdd(typeof(BasicController), "NoSuchAction", "GET"));
            Assert.IsNull(table.GetOrAdd(typeof(BasicController), "NoSuchAction", "GET"));
        }

        /// <summary>
        /// 只认后缀的入口：裸名命中，带 Controller 的名字反而查不到。
        /// </summary>
        [TestMethod]
        public void FindControllerBySuffix_WithSuffixedSegment_HitsController()
        {
            RouteTable.Types = new List<Type> { typeof(BenchProbeController) };

            var table = new RouteTable();

            Assert.AreSame(typeof(BenchProbeController), table.FindControllerBySuffix("BenchProbe"));
            Assert.IsNull(table.FindControllerBySuffix("BenchProbeController"));
        }

        /// <summary>
        /// 裸名与后缀同时存在时，合并入口取列表下标更小的类型，只认后缀的入口始终命中带后缀的类型。
        /// </summary>
        [TestMethod]
        public void FindController_BothBareAndSuffixedPresent_ReturnsLowerListIndex()
        {
            var table = new RouteTable();

            RouteTable.Types = new List<Type> { typeof(IndexOrderProbeController), typeof(IndexOrderProbe) };

            Assert.AreSame(typeof(IndexOrderProbeController), table.FindController("IndexOrderProbe"));
            Assert.AreSame(typeof(IndexOrderProbeController), table.FindControllerBySuffix("IndexOrderProbe"));

            RouteTable.Types = new List<Type> { typeof(IndexOrderProbe), typeof(IndexOrderProbeController) };

            Assert.AreSame(typeof(IndexOrderProbe), table.FindController("IndexOrderProbe"));
            Assert.AreSame(typeof(IndexOrderProbeController), table.FindControllerBySuffix("IndexOrderProbe"));
        }

        /// <summary>
        /// 空名与 null 名在两个入口都返回 null。
        /// </summary>
        [TestMethod]
        public void FindController_EmptyName_ReturnsNull()
        {
            RouteTable.Types = new List<Type> { typeof(BenchProbeController) };

            var table = new RouteTable();

            Assert.IsNull(table.FindController(string.Empty));
            Assert.IsNull(table.FindController(null));
            Assert.IsNull(table.FindControllerBySuffix(string.Empty));
            Assert.IsNull(table.FindControllerBySuffix(null));
        }

        /// <summary>
        /// 供裸名与后缀名冲突用例使用的裸名控制器。
        /// </summary>
        class IndexOrderProbe : Controller
        {
        }

        /// <summary>
        /// 供裸名与后缀名冲突用例使用的带后缀控制器。
        /// </summary>
        class IndexOrderProbeController : Controller
        {
        }

        /// <summary>
        /// 供裸名与后缀名一致性用例使用的控制器。
        /// </summary>
        class BenchProbeController : Controller
        {
        }

        /// <summary>
        /// 供路由缓存键用例使用的控制器：Plain 未声明动词，按全部动词放行。
        /// </summary>
        class CacheKeyProbeController : Controller
        {
            public string Plain()
            {
                return "plain";
            }
        }
    }
}