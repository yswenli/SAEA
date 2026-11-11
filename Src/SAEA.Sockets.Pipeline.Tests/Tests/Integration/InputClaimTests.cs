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
*文件名： InputClaimTests
*版本号： v26.11.5.1
*唯一标识：751d807e-a943-4688-a787-8f9153e431c2
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 13:50:40
*描述：入站接管闸门：输入处理器接管 PipeReader 后其他读取入口立即报错，写方向不受影响
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 18:05:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：输入处理器改为存活到用例收尾，适配处理器返回即断开的新契约
*
*****************************************************************************/
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Sockets;
using SAEA.Sockets.Core.Tcp;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Integration
{
    /// <summary>
    /// 入站接管闸门：输入处理器接管 <c>PipeReader</c> 后，其他读取入口立即抛出可定位的异常，
    /// 写方向不受影响。
    /// </summary>
    [TestClass]
    public class InputClaimTests
    {
        private static readonly Type ConnectionType =
            typeof(IReceiveFlowControl).Assembly.GetType("SAEA.Sockets.Core.SaeaConnection", true);

        private static object NewConnection()
        {
            return RuntimeHelpers.GetUninitializedObject(ConnectionType);
        }

        private static void ClaimInput(object connection, string owner)
        {
            ConnectionType
                .GetMethod("ClaimInput", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(connection, new object[] { owner });
        }

        private static Task ReadFromConnection(object connection, byte[] buffer)
        {
            var result = ConnectionType.GetMethod("ReadAsync").Invoke(
                connection,
                new object[] { buffer, 0, buffer.Length, CancellationToken.None });
            return (Task)result;
        }

        /// <summary>
        /// 入站被接管后读取立即抛异常，异常信息带上接管者与实际被拦截的入口
        /// </summary>
        [TestMethod]
        public async Task ReadAsync_AfterClaimInput_ThrowsInvalidOperationException()
        {
            var connection = NewConnection();
            ClaimInput(connection, "测试处理器");

            var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => ReadFromConnection(connection, new byte[16])).ConfigureAwait(false);

            StringAssert.Contains(exception.Message, "测试处理器");
            StringAssert.Contains(exception.Message, "GetStream()");
        }

        /// <summary>
        /// 重复接管保留首次的接管者描述，避免服务端与客户端路径互相覆盖诊断信息
        /// </summary>
        [TestMethod]
        public async Task ClaimInput_CalledTwice_KeepsFirstOwner()
        {
            var connection = NewConnection();
            ClaimInput(connection, "第一个处理器");
            ClaimInput(connection, "第二个处理器");

            var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => ReadFromConnection(connection, new byte[16])).ConfigureAwait(false);

            StringAssert.Contains(exception.Message, "第一个处理器");
            Assert.IsFalse(exception.Message.Contains("第二个处理器"));
        }

        /// <summary>
        /// 客户端配置输入处理器后，GetStream 的读取被闸门拦住，写方向照常可用。
        /// 输入处理器返回即让出连接所有权，因此它在用例断言期间必须保持存活。
        /// </summary>
        [TestMethod]
        public async Task GetStream_WhenInputHandlerConfigured_ReadThrowsAndWriteWorks()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            // 处理器存活到用例收尾：返回后框架会断开连接，写方向的断言必须在返回之前完成。
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                using (var client = ConnectClientWithInputHandler(listener, release.Task, out var remote))
                using (remote)
                {
                    var stream = client.GetStream();

                    await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                        () => stream.ReadAsync(new byte[16], 0, 16)).ConfigureAwait(false);

                    var payload = Encoding.UTF8.GetBytes("write-after-claim");
                    await stream.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);

                    var received = new byte[payload.Length];
                    var read = await ReadExactAsync(remote.GetStream(), received, received.Length)
                        .WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

                    Assert.AreEqual(payload.Length, read);
                    CollectionAssert.AreEqual(payload, received);

                    // 全部断言完成后再让处理器返回，此后连接由框架收口（不在本用例断言范围内）。
                    release.TrySetResult(true);
                }
            }
            finally
            {
                listener.Stop();
            }
        }

        private static SaeaClientSocket ConnectClientWithInputHandler(TcpListener listener, Task release, out TcpClient remote)
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;

            var option = new SocketOptionBuilder()
                .UseStream()
                .SetIP("127.0.0.1")
                .SetPort(port)
                .UseConnectionOpened(_ => { })
                .UseInputHandler((userToken, input, flow, token) => release)
                .Build();

            var client = new SaeaClientSocket(option);

            try
            {
                client.Connect();
                remote = listener.AcceptTcpClientAsync().GetAwaiter().GetResult();
                return client;
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, int count)
        {
            var read = 0;
            while (read < count)
            {
                var current = await stream.ReadAsync(buffer, read, count - read).ConfigureAwait(false);
                if (current <= 0) break;
                read += current;
            }

            return read;
        }
    }
}