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
*命名空间：SAEA.P2P.Tests.Core
*文件名： BuilderTests
*版本号： net10.0
*唯一标识：a6a7fbcb-e254-4760-b087-4e597564a9da
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 12:10:00
*描述：客户端构建器与快捷入口用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 12:10:00
*修改人： yswenli
*版本号： net10.0
*描述：客户端构建器与快捷入口用例
*
*****************************************************************************/
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P;
using SAEA.P2P.Builder;
using SAEA.P2P.Common;
using SAEA.P2P.Ice;

namespace SAEA.P2P.Tests.Core
{
    /// <summary>
    /// 客户端构建器与快捷入口的配置校验用例：构建只装配配置、不连接服务端，因此不需要启动信令服务端。
    /// </summary>
    [TestCategory(P2PTestSuite.Unit)]
    [TestClass]
    public class BuilderTests
    {
        /// <summary>
        /// 配置完整的构建器只返回未注册客户端，注册由调用方显式发起
        /// </summary>
        [TestMethod]
        public void P2PClientBuilder_配置服务器与节点标识_返回未注册客户端()
        {
            using (var client = new P2PClientBuilder()
                .SetServer("127.0.0.1", 39654)
                .SetNodeId("builder-a")
                .BuildClient())
            {
                Assert.IsFalse(client.IsRegistered);
                Assert.AreEqual("builder-a", client.PeerId);
            }
        }

        /// <summary>
        /// 缺少节点标识时应在构建阶段失败，避免带着空标识进入信令握手
        /// </summary>
        [TestMethod]
        public void P2PClientBuilder_未设置节点标识_抛RegisterPeerIdMissing()
        {
            var ex = Assert.ThrowsExactly<P2PException>(() => new P2PClientBuilder()
                .SetServer("127.0.0.1", 39654)
                .BuildClient());

            Assert.AreEqual(ErrorCode.RegisterPeerIdMissing, ex.ErrorCode);
        }

        /// <summary>
        /// 同时打开在 Windows 上不可用，构建期必须拦下而不是等到 ICE 收集阶段才发现
        /// </summary>
        [TestMethod]
        public void P2PClientBuilder_Windows启用同时打开_校验失败并说明原因()
        {
            // 判定条件只在 Windows 的 TCP 栈上可复现
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Inconclusive("该用例只在 Windows 上验证：Windows 的 TCP 栈不支持同时打开");
            }

            var ice = new IceSessionOptions();
            ice.Ice = new IceOptions();
            ice.Ice.EnableTcpSimultaneousOpen = true;

            var ex = Assert.ThrowsExactly<P2PException>(() => new P2PClientBuilder()
                .SetServer("127.0.0.1", 39654)
                .SetNodeId("builder-so")
                .UseIce(ice)
                .BuildClient());

            Assert.AreEqual(ErrorCode.IceTcpSimultaneousOpenUnsupported, ex.ErrorCode);
            StringAssert.Contains(ex.Message, "同时打开");
        }

        /// <summary>
        /// 快捷入口的默认参数应等价于最小构建器配置，返回的客户端同样未注册
        /// </summary>
        [TestMethod]
        public void P2PQuick_ClientFull_默认参数_返回未注册客户端()
        {
            using (var client = P2PQuick.ClientFull("127.0.0.1", 39654, "quick-a"))
            {
                Assert.IsNotNull(client);
                Assert.IsFalse(client.IsRegistered);
            }
        }
    }
}