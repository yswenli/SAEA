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
*命名空间：SAEA.Sockets.Pipeline.Tests.Tests.Abstractions
*文件名： TypeRegistryTests
*版本号： v26.11.5.1
*唯一标识：215dbc04-e064-4c31-9c98-44dade4fb117
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：TypeRegistryTests
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：TypeRegistryTests
*
*****************************************************************************/
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Abstractions
{
    /// <summary>
    /// 类型注册表：类型码与 CLR 类型的双向映射、重复与越界校验。
    /// </summary>
    [TestClass]
    public class TypeRegistryTests
    {
        private sealed class LoginRequest
        {
        }

        [TestMethod]
        public void Register_ThenResolve_ReturnsRegisteredTypeAndCode()
        {
            var registry = new TypeRegistry();

            registry.Register(1, typeof(LoginRequest));

            Assert.AreEqual(typeof(LoginRequest), registry.Resolve(1));
            Assert.AreEqual(1, registry.ResolveCode(typeof(LoginRequest)));
        }

        [TestMethod]
        public void Register_DuplicateCode_ThrowsInvalidOperationException()
        {
            var registry = new TypeRegistry();
            registry.Register(1, typeof(LoginRequest));

            Assert.ThrowsException<InvalidOperationException>(
                () => registry.Register(1, typeof(string)));
        }

        [TestMethod]
        public void Register_CodeOutOfRange_ThrowsArgumentOutOfRangeException()
        {
            var registry = new TypeRegistry();

            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => registry.Register(-1, typeof(LoginRequest)));
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => registry.Register(256, typeof(LoginRequest)));
        }

        [TestMethod]
        public void Resolve_UnregisteredCode_ReturnsNull()
        {
            var registry = new TypeRegistry();

            Assert.IsNull(registry.Resolve(200));
        }

        [TestMethod]
        public void ResolveCode_UnregisteredType_ReturnsMinusOne()
        {
            var registry = new TypeRegistry();

            Assert.AreEqual(-1, registry.ResolveCode(typeof(LoginRequest)));
        }
    }
}