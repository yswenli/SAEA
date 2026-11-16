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
*命名空间：SAEA.P2P.Tests.Nat
*文件名： VirtualHostTests
*版本号： net10.0
*唯一标识：39239880-4d82-4b3b-a6a0-2f9c3b58cad6
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 11:52:00
*描述：虚拟主机夹具的端点与收发验收
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 11:52:00
*修改人： yswenli
*版本号： net10.0
*描述：新增虚拟主机用例
*
*****************************************************************************/
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SAEA.P2P.Tests.Nat
{
    /// <summary>
    /// 虚拟主机夹具：地址隔离与跨主机收发。
    /// </summary>
    [TestCategory(P2PTestSuite.Nat)]
    [TestClass]
    public class VirtualHostTests
    {
        /// <summary>
        /// 虚拟主机绑到 127.0.0.2 后，本地端点是该地址上的独立端口
        /// </summary>
        [TestMethod]
        public void VirtualHost_绑定127_0_0_2_可获得独立本地端点()
        {
            using (var host = new VirtualHost("127.0.0.2"))
            using (var first = host.BindUdp())
            using (var second = host.BindUdp())
            {
                var firstEndPoint = VirtualHost.EndPointOf(first);
                var secondEndPoint = VirtualHost.EndPointOf(second);

                Assert.AreEqual(IPAddress.Parse("127.0.0.2"), firstEndPoint.Address);
                Assert.AreEqual(IPAddress.Parse("127.0.0.2"), secondEndPoint.Address);
                Assert.AreNotEqual(0, firstEndPoint.Port);
                Assert.AreNotEqual(firstEndPoint.Port, secondEndPoint.Port);
            }
        }

        /// <summary>
        /// 两台虚拟主机互发 UDP 报文，来源端点是发送方主机的真实端点
        /// </summary>
        [TestMethod]
        public void VirtualHost_两台主机互发_来源为发送方真实端点()
        {
            using (var hostA = new VirtualHost("127.0.0.2"))
            using (var hostB = new VirtualHost("127.0.0.3"))
            using (var socketA = hostA.BindUdp())
            using (var socketB = hostB.BindUdp())
            {
                socketB.ReceiveTimeout = 5000;

                var message = Encoding.UTF8.GetBytes("hello");
                var endPointB = VirtualHost.EndPointOf(socketB);
                socketA.SendTo(message, endPointB);

                var buffer = new byte[message.Length];
                EndPoint from = new IPEndPoint(IPAddress.Any, 0);
                var read = socketB.ReceiveFrom(buffer, ref from);

                Assert.AreEqual(message.Length, read);
                CollectionAssert.AreEqual(message, buffer);
                Assert.AreEqual(VirtualHost.EndPointOf(socketA), from);
            }
        }

        /// <summary>
        /// 虚拟主机创建的 TCP 监听套接字绑定在本主机地址上
        /// </summary>
        [TestMethod]
        public void VirtualHost_绑定Tcp监听_本地端点在本主机地址()
        {
            using (var host = new VirtualHost("127.0.0.4"))
            using (var listener = host.BindTcpListener())
            {
                var endPoint = VirtualHost.EndPointOf(listener);

                Assert.AreEqual(IPAddress.Parse("127.0.0.4"), endPoint.Address);
                Assert.AreNotEqual(0, endPoint.Port);
            }
        }
    }
}