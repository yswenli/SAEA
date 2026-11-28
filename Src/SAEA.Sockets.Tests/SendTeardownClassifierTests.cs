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
*命名空间：SAEA.Sockets.Tests
*文件名： SendTeardownClassifierTests
*版本号： net10.0
*唯一标识：808a308f-57b8-48d7-8853-daeb745ac26a
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/28 14:01:49
*描述：发送失败收尾分级判定的表驱动用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/28 14:01:49
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖预期收尾、真实网络故障、普通异常、内层异常与空值
*
 *****************************************************************************/
using System;
using System.Net.Sockets;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.Sockets.Core;
using SAEA.Sockets.Model;

namespace SAEA.Sockets.Tests
{
    /// <summary>
    /// 发送失败收尾分级用例：连接关闭、重置与回收类失败判为预期收尾，真实网络故障与普通异常不降级。
    /// </summary>
    [TestClass]
    public class SendTeardownClassifierTests
    {
        /// <summary>套接字已释放属预期收尾。</summary>
        [TestMethod]
        public void IsExpectedSendTeardown_DisposedSocket_ReturnsTrue()
        {
            Assert.IsTrue(SendTeardownClassifier.IsExpectedSendTeardown(new ObjectDisposedException("socket")));
        }

        /// <summary>连接关闭、重置或回收类错误码属预期收尾。</summary>
        /// <param name="socketError">SocketException 的错误码</param>
        [TestMethod]
        [DataRow((int)SocketError.NotConnected)]
        [DataRow((int)SocketError.ConnectionReset)]
        [DataRow((int)SocketError.ConnectionAborted)]
        [DataRow((int)SocketError.NetworkReset)]
        [DataRow((int)SocketError.Shutdown)]
        [DataRow((int)SocketError.Disconnecting)]
        [DataRow((int)SocketError.OperationAborted)]
        public void IsExpectedSendTeardown_TeardownSocketError_ReturnsTrue(int socketError)
        {
            Assert.IsTrue(SendTeardownClassifier.IsExpectedSendTeardown(new SocketException(socketError)), "错误码：" + socketError);
        }

        /// <summary>真实网络故障不能吞成收尾。</summary>
        /// <param name="socketError">SocketException 的错误码</param>
        [TestMethod]
        [DataRow((int)SocketError.AccessDenied)]
        [DataRow((int)SocketError.TimedOut)]
        [DataRow((int)SocketError.HostUnreachable)]
        [DataRow((int)SocketError.NetworkDown)]
        [DataRow((int)SocketError.NetworkUnreachable)]
        public void IsExpectedSendTeardown_RealNetworkFailure_ReturnsFalse(int socketError)
        {
            Assert.IsFalse(SendTeardownClassifier.IsExpectedSendTeardown(new SocketException(socketError)), "错误码：" + socketError);
        }

        /// <summary>普通异常不属于收尾分级。</summary>
        [TestMethod]
        public void IsExpectedSendTeardown_PlainException_ReturnsFalse()
        {
            Assert.IsFalse(SendTeardownClassifier.IsExpectedSendTeardown(new InvalidOperationException("plain")));
        }

        /// <summary>框架自身异常不属于收尾分级。</summary>
        [TestMethod]
        public void IsExpectedSendTeardown_KernelException_ReturnsFalse()
        {
            Assert.IsFalse(SendTeardownClassifier.IsExpectedSendTeardown(new KernelException("kernel")));
        }

        /// <summary>空异常不视为收尾失败。</summary>
        [TestMethod]
        public void IsExpectedSendTeardown_Null_ReturnsFalse()
        {
            Assert.IsFalse(SendTeardownClassifier.IsExpectedSendTeardown(null));
        }

        /// <summary>内层异常链中的收尾类失败同样识别。</summary>
        [TestMethod]
        public void IsExpectedSendTeardown_InnerSocketTeardown_ReturnsTrue()
        {
            var exception = new InvalidOperationException("outer", new SocketException((int)SocketError.ConnectionReset));

            Assert.IsTrue(SendTeardownClassifier.IsExpectedSendTeardown(exception));
        }
    }
}
