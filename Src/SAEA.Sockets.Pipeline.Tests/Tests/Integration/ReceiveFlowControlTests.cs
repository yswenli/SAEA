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
*命名空间：SAEA.Sockets.Pipeline.Tests.Tests.Integration
*文件名： ReceiveFlowControlTests
*版本号： v26.11.5.1
*唯一标识：8bbc26d8-b069-411d-be58-62d819a732d3
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：接收闸门的双来源语义：Pipe 背压（_receivePaused）与应用级暂停（_appPaused）互相独立， 以及把这些闸门适配给 IReceiveFlowControl 的适配器行为
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：接收闸门的双来源语义：Pipe 背压（_receivePaused）与应用级暂停（_appPaused）互相独立， 以及把这些闸门适配给 IReceiveFlowControl 的适配器行为
*
*****************************************************************************/
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Sockets;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Integration
{
    /// <summary>
    /// 接收闸门的双来源语义：Pipe 背压（_receivePaused）与应用级暂停（_appPaused）互相独立，
    /// 以及把这些闸门适配给 IReceiveFlowControl 的适配器行为。
    /// 不启动真实 socket：连接对象用未初始化实例，只操作闸门字段。
    /// </summary>
    [TestClass]
    public class ReceiveFlowControlTests
    {
        private static readonly Type ConnectionType =
            typeof(IReceiveFlowControl).Assembly.GetType("SAEA.Sockets.Core.SaeaConnection", true);

        private static readonly Type FlowControlType =
            typeof(IReceiveFlowControl).Assembly.GetType("SAEA.Sockets.Core.ReceiveFlowControl", true);

        private static object NewConnection()
        {
            return RuntimeHelpers.GetUninitializedObject(ConnectionType);
        }

        private static IReceiveFlowControl NewFlowControl(object connection, Action onClose)
        {
            return (IReceiveFlowControl)Activator.CreateInstance(
                FlowControlType,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { connection, (object)onClose },
                null);
        }

        private static void PauseApplication(object connection)
        {
            ConnectionType.GetMethod("PauseApplication").Invoke(connection, null);
        }

        private static void ResumeApplication(object connection)
        {
            ConnectionType.GetMethod("ResumeApplication").Invoke(connection, null);
        }

        private static bool IsApplicationPaused(object connection)
        {
            return (bool)ConnectionType.GetProperty("IsApplicationPaused").GetValue(connection);
        }

        private static bool IsReceivePaused(object connection)
        {
            return (bool)ConnectionType.GetProperty("IsReceivePaused").GetValue(connection);
        }

        private static void SetPipeBackpressure(object connection, bool paused)
        {
            ConnectionType
                .GetField("_receivePaused", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(connection, paused);
        }

        [TestMethod]
        public void PauseApplication_MarksBothGates()
        {
            var connection = NewConnection();

            Assert.IsFalse(IsApplicationPaused(connection));
            Assert.IsFalse(IsReceivePaused(connection));

            PauseApplication(connection);

            Assert.IsTrue(IsApplicationPaused(connection));
            Assert.IsTrue(IsReceivePaused(connection));
        }

        [TestMethod]
        public void ResumeApplication_ClearsApplicationGate()
        {
            var connection = NewConnection();
            PauseApplication(connection);

            ResumeApplication(connection);

            Assert.IsFalse(IsApplicationPaused(connection));
            Assert.IsFalse(IsReceivePaused(connection));
        }

        [TestMethod]
        public void ResumeApplication_KeepsPipeBackpressureGate()
        {
            var connection = NewConnection();
            SetPipeBackpressure(connection, true);
            PauseApplication(connection);

            ResumeApplication(connection);

            Assert.IsFalse(IsApplicationPaused(connection));
            Assert.IsTrue(IsReceivePaused(connection));
        }

        [TestMethod]
        public void ApplicationAndPipeGates_AreIndependent()
        {
            var connection = NewConnection();

            SetPipeBackpressure(connection, true);
            Assert.IsFalse(IsApplicationPaused(connection));
            Assert.IsTrue(IsReceivePaused(connection));

            PauseApplication(connection);
            SetPipeBackpressure(connection, false);
            Assert.IsTrue(IsApplicationPaused(connection));
            Assert.IsTrue(IsReceivePaused(connection));

            ResumeApplication(connection);
            Assert.IsFalse(IsApplicationPaused(connection));
            Assert.IsFalse(IsReceivePaused(connection));
        }

        [TestMethod]
        public void FlowControl_PauseAndResume_TrackApplicationGate()
        {
            var connection = NewConnection();
            var flow = NewFlowControl(connection, null);

            Assert.IsFalse(flow.IsPaused);

            flow.Pause();
            Assert.IsTrue(flow.IsPaused);
            Assert.IsTrue(IsReceivePaused(connection));

            flow.Resume();
            Assert.IsFalse(flow.IsPaused);
            Assert.IsFalse(IsReceivePaused(connection));
        }

        [TestMethod]
        public void FlowControl_CloseAsync_InvokesCloseCallbackAndCompletes()
        {
            var connection = NewConnection();
            var closeCount = 0;
            var flow = NewFlowControl(connection, () => closeCount++);

            var first = flow.CloseAsync("test");
            Assert.IsTrue(first.IsCompleted);
            Assert.AreEqual(1, closeCount);

            var second = flow.CloseAsync("test-again");
            Assert.IsTrue(second.IsCompleted);
            Assert.AreEqual(2, closeCount);
        }
    }
}