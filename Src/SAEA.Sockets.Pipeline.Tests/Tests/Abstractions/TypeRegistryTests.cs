using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Abstractions
{
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