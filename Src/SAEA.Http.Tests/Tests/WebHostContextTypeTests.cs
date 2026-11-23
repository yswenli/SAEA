/****************************************************************************
 * 
   ____    _    _____    _      ____             _        _   
  / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
  \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __| 
   ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ 
  |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__| 
                                                              
 *Copyright (c) yswenli All Rights Reserved.
 *CLR版本： net10.0
 *机器名称：WALLE
 *公司名称：yswenli
 *命名空间：SAEA.Http.Tests
 *文件名： WebHostContextTypeTests
 *版本号： net10.0
 *唯一标识：2b3c9f0a-6d1e-4a8f-9a55-7c9f0f1b3d21
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/24 11:20:00
 *描述：WebHost 上下文类型的入参校验与回落语义
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/24 11:20:00
 *修改人： yswenli
 *版本号： net10.0
 *描述：新增，锁定「校验的是入参而不是字段」，非法类型回落到默认上下文而不是产生不可用的构造委托
 *
 *****************************************************************************/
using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.Http.Model;

namespace SAEA.Http.Tests
{
    /// <summary>
    /// <see cref="WebHost"/> 构造时对上下文类型的判定。
    /// </summary>
    [TestClass]
    public class WebHostContextTypeTests
    {
        /// <summary>
        /// 传入不实现 IHttpContext 的类型时应回落到默认上下文，并且能正常创建上下文实例。
        /// </summary>
        [TestMethod]
        public void Ctor_NonContextType_FallsBackToDefaultContext()
        {
            var host = new WebHost(typeof(string));

            var context = host.CreateContext();

            Assert.AreEqual(typeof(HttpContext), context.GetType(), "非法上下文类型应回落到默认上下文");
        }

        /// <summary>
        /// 传入合法的自定义上下文类型时应使用该类型。
        /// </summary>
        [TestMethod]
        public void Ctor_ValidContextType_UsesCustomContext()
        {
            var host = new WebHost(typeof(ProbeHttpContext));

            var context = host.CreateContext();

            Assert.AreEqual(typeof(ProbeHttpContext), context.GetType(), "合法上下文类型应被原样使用");
        }

        /// <summary>
        /// 只用于断言的派生上下文；WebHost 只是构造它，不会真正处理请求。
        /// </summary>
        private sealed class ProbeHttpContext : HttpContext
        {
            /// <summary>
            /// 以宿主服务创建上下文。
            /// </summary>
            /// <param name="webHost">Web 宿主</param>
            public ProbeHttpContext(IWebHost webHost) : base(webHost)
            {
            }
        }
    }
}