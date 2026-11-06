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
*文件名： SocketOptionBuilderTests
*版本号： v26.11.5.1
*唯一标识：d15bf650-b0e8-4dd3-bbcc-de302ab5fc14
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：验收 10 的 ①②③ 与客户端钩子拒绝
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：验收 10 的 ①②③ 与客户端钩子拒绝
*
*****************************************************************************/
using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Sockets;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Integration
{
    /// <summary>
    /// 验收 10 的 ①②③ 与客户端钩子拒绝。
    /// </summary>
    [TestClass]
    public class SocketOptionBuilderTests
    {
        private static Task Handler(IUserToken token, PipeReader reader, IReceiveFlowControl flow, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private static void OnConnectionOpened(ChannelInfo channel)
        {
        }

        [TestMethod]
        public void InputHandlerWithoutUseStream_ThrowsInvalidOperationException()
        {
            var builder = SocketOptionBuilder.Instance
                .UsePacket()
                .UseConnectionOpened(OnConnectionOpened)
                .UseInputHandler(Handler);

            Assert.ThrowsException<InvalidOperationException>(() => builder.Build());
        }

        [TestMethod]
        public void ConnectionOpenedWithoutUseStream_ThrowsInvalidOperationException()
        {
            var builder = SocketOptionBuilder.Instance
                .UsePacket()
                .UseConnectionOpened(OnConnectionOpened);

            Assert.ThrowsException<InvalidOperationException>(() => builder.Build());
        }

        [TestMethod]
        public void ConnectionOpenedOnlyWithUseStream_BuildsSuccessfully()
        {
            var builder = SocketOptionBuilder.Instance
                .UseStream()
                .UseConnectionOpened(OnConnectionOpened);

            var option = builder.Build();

            Assert.IsNotNull(option);
            var pipelineOption = (IPipelineSocketOption)option;
            Assert.IsNotNull(pipelineOption.ConnectionOpened);
            Assert.IsNull(pipelineOption.InputHandler);
        }

        [TestMethod]
        public void InputHandlerWithoutConnectionOpened_ThrowsInvalidOperationException()
        {
            var builder = SocketOptionBuilder.Instance
                .UseStream()
                .UseInputHandler(Handler);

            Assert.ThrowsException<InvalidOperationException>(() => builder.Build());
        }

        [TestMethod]
        public void ClientWithHook_ThrowsNotSupportedException()
        {
            var option = SocketOptionBuilder.Instance
                .UseStream()
                .UseConnectionOpened(OnConnectionOpened)
                .Build();

            Assert.ThrowsException<NotSupportedException>(() => new SaeaClientSocket(option));
        }
    }
}