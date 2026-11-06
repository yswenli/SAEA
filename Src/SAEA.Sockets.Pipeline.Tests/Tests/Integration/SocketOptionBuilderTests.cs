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
                .UseIocp()
                .UseConnectionOpened(OnConnectionOpened)
                .UseInputHandler(Handler);

            Assert.ThrowsException<InvalidOperationException>(() => builder.Build());
        }

        [TestMethod]
        public void ConnectionOpenedWithoutUseStream_ThrowsInvalidOperationException()
        {
            var builder = SocketOptionBuilder.Instance
                .UseIocp()
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