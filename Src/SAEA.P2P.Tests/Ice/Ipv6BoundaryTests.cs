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
*命名空间：SAEA.P2P.Tests.Ice
*文件名： Ipv6BoundaryTests
*版本号： net10.0
*唯一标识：8f1c2d47-3b6e-4a95-9c0f-1d2e5a7b4c38
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 22:00:00
*描述：IPv6 验收边界与平台限制说明
*
*=====================================================================
*修改标记
*修改时间：2026/11/16 12:00:00
*修改人： yswenli
*版本号： net10.0
*描述：非回环 IPv6 探针改为断言平台结果
*
*****************************************************************************/
using System;
using System.Net;
using System.Net.Sockets;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SAEA.P2P.Tests.Ice
{
    /// <summary>
    /// IPv6 验收边界用例：记录本轮不自动化的 NAT66 场景，并把非回环地址绑定的平台限制断言成可核对的结果
    /// </summary>
    [TestCategory(P2PTestSuite.Ipv6)]
    [TestClass]
    public class Ipv6BoundaryTests
    {
        /// <summary>
        /// 测试上下文，用于把平台观测结果写进测试输出
        /// </summary>
        public TestContext TestContext { get; set; }

        /// <summary>
        /// NAT66 需要管理员预置 fd00::/8 地址与 v6 NAT 代理，明确记录为不自动化，而不是功能缺失
        /// </summary>
        [TestMethod]
        [Timeout(15000)]
        public void Nat66_需管理员预置地址_默认跳过()
        {
            Assert.Inconclusive("NAT66 不在本轮自动化验收范围（D13）：需管理员预置 fd00::/8 地址并构造 v6 NAT 代理；非管理员下仅 ::1 可绑定，IPv6 验收只覆盖 ::1 主机直连、TURN v6 中继、双栈监听与地址编码单测。");
        }

        /// <summary>
        /// 用裸套接字绑定 fd00::1，把「非管理员只有 ::1 可用」这条平台限制断言成明确结果并写入测试输出
        /// </summary>
        [TestMethod]
        [Timeout(15000)]
        public void Ipv6_非回环地址绑定_观测结果符合平台限制()
        {
            var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
            try
            {
                socket.Bind(new IPEndPoint(IPAddress.Parse("fd00::1"), 0));
                TestContext.WriteLine("本环境可绑定 fd00::1，具备管理员预置时可另行验证 NAT66；本轮仅覆盖 ::1。");
                Assert.IsTrue(socket.IsBound);
            }
            catch (SocketException ex)
            {
                // 地址不可用、无权限、地址族不支持与参数非法都属平台限制；其它错误码说明环境异常，应暴露失败而不是静默通过
                var acceptable = new[]
                {
                    SocketError.AddressNotAvailable,
                    SocketError.AccessDenied,
                    SocketError.AddressFamilyNotSupported,
                    SocketError.InvalidArgument,
                };
                Assert.IsTrue(Array.IndexOf(acceptable, ex.SocketErrorCode) >= 0, ex.SocketErrorCode.ToString());
                TestContext.WriteLine("非管理员环境确认 fd00::1 不可绑定（SocketError=" + ex.SocketErrorCode + "），非管理员环境仅 ::1 可绑定，IPv6 验收仅覆盖 ::1 可自动化部分。");
            }
            finally
            {
                socket.Dispose();
            }
        }
    }
}