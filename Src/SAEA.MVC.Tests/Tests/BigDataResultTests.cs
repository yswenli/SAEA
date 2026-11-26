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
 *文件名： BigDataResultTests
 *版本号： net10.0
 *唯一标识：e084af53-fc24-43ae-8276-774a767da27b
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/26 17:40:00
 *描述：无请求上下文时 BigDataResult 构造关闭数据流的用例
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/26 17:40:00
 *修改人： yswenli
 *版本号： net10.0
 *描述：无请求上下文时 BigDataResult 构造关闭数据流的用例
 *
 *****************************************************************************/
using System;
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.Common;
using SAEA.Http.Model;
using SAEA.MVC.Results;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// BigDataResult 在缺少请求上下文时释放数据流的用例。
    /// </summary>
    [TestClass]
    public class BigDataResultTests
    {
        /// <summary>
        /// 无当前上下文时构造抛异常，默认应关闭传入的数据流；leaveOpen 为 true 时保留。
        /// </summary>
        [TestMethod]
        public void Constructor_NoCurrentContext_ThrowsAndDisposesStream()
        {
            var stream = new TrackedMemoryStream();
            var previous = CallContext<IHttpContext>.GetData("ContextBase.Current");

            CallContext<IHttpContext>.SetData("ContextBase.Current", null);

            try
            {
                Assert.ThrowsExactly<NullReferenceException>(
                    () => new BigDataResult(stream),
                    "缺少当前上下文时构造应抛 NullReferenceException");
            }
            finally
            {
                CallContext<IHttpContext>.SetData("ContextBase.Current", previous);
            }

            Assert.IsTrue(stream.IsDisposed, "构造阶段异常后默认应关闭传入的数据流");

            var leaveOpenStream = new TrackedMemoryStream();

            CallContext<IHttpContext>.SetData("ContextBase.Current", null);

            try
            {
                Assert.ThrowsExactly<NullReferenceException>(
                    () => new BigDataResult(leaveOpenStream, leaveOpen: true),
                    "leaveOpen 为 true 时构造同样应因缺少上下文抛异常");
            }
            finally
            {
                CallContext<IHttpContext>.SetData("ContextBase.Current", previous);
            }

            Assert.IsFalse(leaveOpenStream.IsDisposed, "leaveOpen 为 true 时不应关闭传入的数据流");
        }

        /// <summary>
        /// 记录释放状态的最小内存流。
        /// </summary>
        private sealed class TrackedMemoryStream : MemoryStream
        {
            /// <summary>
            /// 是否已被释放。
            /// </summary>
            public bool IsDisposed { get; private set; }

            /// <summary>
            /// 记录释放状态后交给基类释放。
            /// </summary>
            /// <param name="disposing">是否为显式释放</param>
            protected override void Dispose(bool disposing)
            {
                IsDisposed = true;

                base.Dispose(disposing);
            }
        }
    }
}