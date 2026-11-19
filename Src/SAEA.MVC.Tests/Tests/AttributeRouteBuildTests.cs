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
*文件名： AttributeRouteBuildTests
*版本号： net10.0
*唯一标识：a8462b99-7fa0-456a-9200-e9b38ab00a9a
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/17 18:20:00
*描述：属性路由构建期校验用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 15:13:26
*修改人： yswenli
*版本号： net10.0
*描述：InternalsVisibleTo 打开后改为直调 AttributeRouteTable.Build，去掉类型全名字符串与反射拆包
*
*****************************************************************************/
using SAEA.MVC.Attributes;
using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Routing;
using SAEA.MVC.Tests.Tests.Support;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// 属性路由构建期校验用例。
    /// 负向模板只定义在本类内部的私有类型上，它们不继承 Controller，因此不会被
    /// SAEAMvcApplication 构造期的 RegistAll 扫进控制器集合，既有用例不受影响。
    /// </summary>
    [TestClass]
    public class AttributeRouteBuildTests
    {
        /// <summary>
        /// 负向用例用到的全部类型，用于断言扫描结果里没有它们。
        /// </summary>
        private static readonly Type[] NegativeTypes =
        {
            typeof(DuplicateStaticRouteA),
            typeof(DuplicateStaticRouteB),
            typeof(ShapeRouteA),
            typeof(ShapeRouteB),
            typeof(OptionalTemplateRoute),
            typeof(CatchAllTemplateRoute),
            typeof(ConstrainedTemplateRoute),
            typeof(MultiSourceRoute)
        };

        /// <summary>
        /// 同一静态路径与同一动词被两个 action 声明时，构建期抛异常且消息含两个归属。
        /// </summary>
        [TestMethod]
        public void Build_SameStaticPathSameVerb_ThrowsWithBothOwners()
        {
            var exception = Assert.ThrowsException<InvalidOperationException>(
                () => BuildAttributeRouteTable(typeof(DuplicateStaticRouteA), typeof(DuplicateStaticRouteB)));

            StringAssert.Contains(exception.Message, "DuplicateStaticRouteA/Index", "异常消息应包含第一个声明的 Controller/Action");
            StringAssert.Contains(exception.Message, "DuplicateStaticRouteB/Index", "异常消息应包含第二个声明的 Controller/Action");
        }

        /// <summary>
        /// {id} 与 {uid} 路径形状等价，视为同路径同动词冲突。
        /// </summary>
        [TestMethod]
        public void Build_ParameterNameDiffersButShapeSame_ThrowsWithBothOwners()
        {
            var exception = Assert.ThrowsException<InvalidOperationException>(
                () => BuildAttributeRouteTable(typeof(ShapeRouteA), typeof(ShapeRouteB)));

            StringAssert.Contains(exception.Message, "ShapeRouteA/Get", "异常消息应包含第一个声明的 Controller/Action");
            StringAssert.Contains(exception.Message, "ShapeRouteB/Get", "异常消息应包含第二个声明的 Controller/Action");
            StringAssert.Contains(exception.Message, "/routedemo/negshape/{}", "异常消息应按路径形状报告冲突");
        }

        /// <summary>
        /// 可选参数模板 {id?} 在构建期抛 ArgumentException。
        /// </summary>
        [TestMethod]
        public void Build_OptionalParameterTemplate_ThrowsArgumentException()
        {
            var exception = Assert.ThrowsException<ArgumentException>(
                () => BuildAttributeRouteTable(typeof(OptionalTemplateRoute)));

            StringAssert.Contains(exception.Message, "{id?}", "异常消息应指出非法模板 {id?}");
        }

        /// <summary>
        /// 通配模板 {*x} 在构建期抛 ArgumentException。
        /// </summary>
        [TestMethod]
        public void Build_CatchAllTemplate_ThrowsArgumentException()
        {
            var exception = Assert.ThrowsException<ArgumentException>(
                () => BuildAttributeRouteTable(typeof(CatchAllTemplateRoute)));

            StringAssert.Contains(exception.Message, "{*x}", "异常消息应指出非法模板 {*x}");
        }

        /// <summary>
        /// 类型约束模板 {id:int} 在构建期抛 ArgumentException。
        /// </summary>
        [TestMethod]
        public void Build_TypeConstraintTemplate_ThrowsArgumentException()
        {
            var exception = Assert.ThrowsException<ArgumentException>(
                () => BuildAttributeRouteTable(typeof(ConstrainedTemplateRoute)));

            StringAssert.Contains(exception.Message, "{id:int}", "异常消息应指出非法模板 {id:int}");
        }

        /// <summary>
        /// 同一 action 同时声明 [Route] 与 [HttpGet(模板)] 时，构建期抛 ArgumentException。
        /// </summary>
        [TestMethod]
        public void Build_MultipleTemplateSources_ThrowsArgumentException()
        {
            var exception = Assert.ThrowsException<ArgumentException>(
                () => BuildAttributeRouteTable(typeof(MultiSourceRoute)));

            StringAssert.Contains(exception.Message, "MultiSourceRoute/Index", "异常消息应指出声明位置");

            StringAssert.Contains(exception.Message, "路径声明不唯一", "异常消息应说明路径来源不唯一");
        }

        /// <summary>
        /// 程序集扫描不会把负向模板类型当成控制器，既有用例因此不受影响。
        /// </summary>
        [TestMethod]
        public void RegistAll_NegativeRouteTypes_NotScannedAsControllers()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();

            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                // 构造期已跑过 RegistAll 与属性路由构建，这里断言静态类型集合里没有负向类型
                Assert.IsTrue(RouteTable.Types.Count > 0, "RegistAll 应至少发现一个真实控制器");

                foreach (var negativeType in NegativeTypes)
                {
                    Assert.IsFalse(
                        RouteTable.Types.Contains(negativeType),
                        "负向模板类型 " + negativeType.Name + " 不应进入控制器扫描结果");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 直调 internal 的 AttributeRouteTable.Build，把只在测试内定义的类型交给构建期校验。
        /// </summary>
        /// <param name="controllerTypes">参与构建的类型，不要求继承 Controller</param>
        private static void BuildAttributeRouteTable(params Type[] controllerTypes)
        {
            AttributeRouteTable.Build(new RouteTable(), new List<Type>(controllerTypes));
        }

        /// <summary>
        /// 与 DuplicateStaticRouteB 声明同一路径与同一动词。
        /// </summary>
        private class DuplicateStaticRouteA
        {
            /// <summary>
            /// 冲突声明之一。
            /// </summary>
            /// <returns>固定文本</returns>
            [Route("routedemo/negdup/a")]
            public string Index()
            {
                return "a";
            }
        }

        /// <summary>
        /// 与 DuplicateStaticRouteA 声明同一路径与同一动词。
        /// </summary>
        private class DuplicateStaticRouteB
        {
            /// <summary>
            /// 冲突声明之二。
            /// </summary>
            /// <returns>固定文本</returns>
            [Route("routedemo/negdup/a")]
            public string Index()
            {
                return "b";
            }
        }

        /// <summary>
        /// 声明 {id} 形状的路径参数模板。
        /// </summary>
        private class ShapeRouteA
        {
            /// <summary>
            /// 冲突声明之一，占位名为 id。
            /// </summary>
            /// <param name="id">路径参数</param>
            /// <returns>固定文本</returns>
            [HttpGetAttribute("routedemo/negshape/{id}")]
            public string Get(string id)
            {
                return id;
            }
        }

        /// <summary>
        /// 声明 {uid} 形状的路径参数模板，与 ShapeRouteA 形状等价。
        /// </summary>
        private class ShapeRouteB
        {
            /// <summary>
            /// 冲突声明之二，占位名为 uid。
            /// </summary>
            /// <param name="uid">路径参数</param>
            /// <returns>固定文本</returns>
            [HttpGetAttribute("routedemo/negshape/{uid}")]
            public string Get(string uid)
            {
                return uid;
            }
        }

        /// <summary>
        /// 声明可选参数模板，构建期应被拒绝。
        /// </summary>
        private class OptionalTemplateRoute
        {
            /// <summary>
            /// 非法模板入口。
            /// </summary>
            /// <param name="id">路径参数</param>
            /// <returns>固定文本</returns>
            [Route("routedemo/neginvalid/{id?}")]
            public string Index(string id)
            {
                return id;
            }
        }

        /// <summary>
        /// 声明通配模板，构建期应被拒绝。
        /// </summary>
        private class CatchAllTemplateRoute
        {
            /// <summary>
            /// 非法模板入口。
            /// </summary>
            /// <param name="x">路径参数</param>
            /// <returns>固定文本</returns>
            [Route("routedemo/neginvalid/{*x}")]
            public string Index(string x)
            {
                return x;
            }
        }

        /// <summary>
        /// 声明类型约束模板，构建期应被拒绝。
        /// </summary>
        private class ConstrainedTemplateRoute
        {
            /// <summary>
            /// 非法模板入口。
            /// </summary>
            /// <param name="id">路径参数</param>
            /// <returns>固定文本</returns>
            [Route("routedemo/neginvalid/{id:int}")]
            public string Index(string id)
            {
                return id;
            }
        }

        /// <summary>
        /// 同一 action 同时声明两个带模板的特性，构建期应被拒绝。
        /// </summary>
        private class MultiSourceRoute
        {
            /// <summary>
            /// 路径来源不唯一的入口。
            /// </summary>
            /// <returns>固定文本</returns>
            [Route("routedemo/negmulti/a")]
            [HttpGetAttribute("routedemo/negmulti/b")]
            public string Index()
            {
                return "multi";
            }
        }
    }
}