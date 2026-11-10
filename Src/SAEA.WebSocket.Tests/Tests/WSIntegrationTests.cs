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
*命名空间：SAEA.WebSocket.Tests
*文件名： WSIntegrationTests
*版本号： net10.0
*唯一标识：13bcd9c9-f177-4c66-977a-679bde017c13
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 19:20:00
*描述：WebSocket 服务端与客户端的本机集成用例，含明文与 TLS 双向
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 19:20:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，WebSocket 服务端与客户端本机集成用例
*
*****************************************************************************/
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.Sockets.Pipeline;
using SAEA.Sockets.Ssl;
using SAEA.WebSocket.Model;
using SAEA.WebSocket.Type;

namespace SAEA.WebSocket.Tests
{
    /// <summary>
    /// 服务端与客户端在本机回环上的端到端用例：明文一条、TLS 服务端一条、TLS 客户端一条。
    /// </summary>
    [TestClass]
    public class WSIntegrationTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(20);

        private const string PfxPassword = "yswenli";

        /// <summary>
        /// 明文连接：客户端发出的文本帧经握手与帧解码后到达服务端装配的处理器。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task WSServer_PlainClient_ReceivesTextMessageThroughConfigurePipeline()
        {
            var port = GetFreePort();
            var received = NewCapture();

            var server = new WSServer(port);
            server.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new CopyingCaptureHandler(received));

            server.Start();

            try
            {
                var client = new WSClient("127.0.0.1", port);
                try
                {
                    Assert.IsTrue(client.Connect(15000), "明文 WebSocket 握手应在超时前完成。");

                    client.Send("plain-hello");

                    var message = await received.Task.WaitAsync(WaitTimeout).ConfigureAwait(false);

                    Assert.AreEqual((byte)WSProtocalType.Text, message.Type, "文本帧应保持 Text 操作码。");
                    Assert.AreEqual("plain-hello", System.Text.Encoding.UTF8.GetString(message.Content.ToArray()),
                        "服务端收到的帧体应与客户端发送的一致。");
                }
                finally
                {
                    client.Close();
                }
            }
            finally
            {
                server.Stop();
            }
        }

        /// <summary>
        /// TLS 服务端：带证书启动后，配置了 TLS 选项的客户端能完成握手并把帧送达服务端处理器。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task WSServer_WithTlsCertificate_ReceivesFrameFromTlsClient()
        {
            var port = GetFreePort();
            var received = NewCapture();

            await WithTlsServerAsync(port, server =>
            {
                server.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new CopyingCaptureHandler(received));
            }, async server =>
            {
                var client = new WSClient("127.0.0.1", port, SubProtocolType.Default, "", 64 * 1024, tls =>
                {
                    tls.TargetHost = "127.0.0.1";
                    tls.AllowInvalidCertificates = true;
                });

                try
                {
                    Assert.IsTrue(client.Connect(15000), "TLS WebSocket 握手应在超时前完成。");

                    client.Send("tls-hello");

                    var message = await received.Task.WaitAsync(WaitTimeout).ConfigureAwait(false);

                    Assert.AreEqual("tls-hello", System.Text.Encoding.UTF8.GetString(message.Content.ToArray()),
                        "TLS 通道上收到明文字段应与会话内容一致。");
                }
                finally
                {
                    client.Close();
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// TLS 客户端：配置了 TLS 选项的客户端能收到服务端经同一条加密连接回的帧。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task WSClient_WithTlsOptions_ReceivesServerReply()
        {
            var port = GetFreePort();
            var reply = NewCapture();

            await WithTlsServerAsync(port, server =>
            {
                // 收到什么就原样回一份，验证双向加密通道都已打通。
                server.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new EchoingHandler());
            }, async server =>
            {
                var client = new WSClient("127.0.0.1", port, SubProtocolType.Default, "", 64 * 1024, tls =>
                {
                    tls.TargetHost = "127.0.0.1";
                    tls.AllowInvalidCertificates = true;
                });
                client.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new CopyingCaptureHandler(reply));

                try
                {
                    Assert.IsTrue(client.Connect(15000), "TLS WebSocket 握手应在超时前完成。");

                    client.Send("tls-echo");

                    var message = await reply.Task.WaitAsync(WaitTimeout).ConfigureAwait(false);

                    Assert.AreEqual((byte)WSProtocalType.Text, message.Type, "回包应保持 Text 操作码。");
                    Assert.AreEqual("tls-echo", System.Text.Encoding.UTF8.GetString(message.Content.ToArray()),
                        "客户端应收到服务端回写的同一份内容。");
                }
                finally
                {
                    client.Close();
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 用现场生成的自签证书启动一个 TLS 服务端并执行用例，结束后删除临时证书并停止服务端。
        /// </summary>
        /// <param name="port">监听端口。</param>
        /// <param name="configure">服务端链装配回调，在启动前调用。</param>
        /// <param name="test">用例主体，接收已启动的服务端。</param>
        /// <returns>用例执行完成后的任务。</returns>
        private static async Task WithTlsServerAsync(
            int port,
            Action<WSServer> configure,
            Func<WSServer, Task> test)
        {
            var pfxPath = Path.Combine(Path.GetTempPath(), "saea-ws-" + Guid.NewGuid().ToString("N") + ".pfx");

            using (var bundle = SaeaCertificateFactory.CreateSelfSigned(new SaeaCertificateOptions
            {
                Subject = "CN=127.0.0.1",
                DnsNames = new[] { "localhost" },
                IpAddresses = new[] { "127.0.0.1" }
            }))
            {
                bundle.SavePfx(pfxPath, PfxPassword);

                var server = new WSServer(port, SslProtocols.Tls12, pfxPath, PfxPassword);
                configure(server);

                server.Start();

                try
                {
                    await test(server).ConfigureAwait(false);
                }
                finally
                {
                    server.Stop();
                    TryDelete(pfxPath);
                }
            }
        }

        /// <summary>
        /// 取一个当前空闲的本机端口，先占后放，避免用例并行时撞端口。
        /// </summary>
        /// <returns>可监听的端口号。</returns>
        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        private static TaskCompletionSource<WSProtocal> NewCapture()
        {
            return new TaskCompletionSource<WSProtocal>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException)
            {
                // 证书文件只用于本用例，删不掉不影响结论。
            }
        }

        /// <summary>
        /// 捕获第一条业务帧并复制帧体：帧体的池化缓冲在本次派发结束后由链归还，不能跨回调持有。
        /// </summary>
        private sealed class CopyingCaptureHandler : InboundHandlerAdapter
        {
            private readonly TaskCompletionSource<WSProtocal> _capture;

            /// <summary>
            /// 绑定捕获目标。
            /// </summary>
            /// <param name="capture">捕获完成源。</param>
            public CopyingCaptureHandler(TaskCompletionSource<WSProtocal> capture)
            {
                _capture = capture;
            }

            /// <inheritdoc />
            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var protocal = msg as WSProtocal;
                if (protocal != null)
                    _capture.TrySetResult(new WSProtocal(protocal.Type, protocal.Content.ToArray()));

                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// 把收到的业务帧原样回写给对端，用于验证双向通道。
        /// </summary>
        private sealed class EchoingHandler : InboundHandlerAdapter
        {
            /// <inheritdoc />
            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var protocal = msg as WSProtocal;
                if (protocal == null)
                {
                    await ctx.FireChannelReadAsync(msg).ConfigureAwait(false);
                    return;
                }

                // 先复制再写出：原帧体在本次派发结束后会被链归还。
                await ctx.Pipeline.WriteAsync(new WSProtocal(protocal.Type, protocal.Content.ToArray()))
                    .ConfigureAwait(false);
            }
        }
    }
}