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
*命名空间：SAEA.Http.Tests
*文件名： PipelineAdapterTests
*版本号： net10.0
*唯一标识：eca345df-8ac4-46c6-8977-a8a86c608c8c
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/9 15:20:00
*描述：SAEA.Http 通道链适配层的用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/9 15:20:00
*修改人： yswenli
*版本号： net10.0
*描述：SAEA.Http 通道链适配层的用例
*
*****************************************************************************/
using System;
using System.IO.Pipelines;
using System.Net;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Sockets;
using SAEA.Sockets.Core;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Pipeline;

namespace SAEA.Http.Tests
{
    /// <summary>通道链适配层的行为用例。</summary>
    [TestClass]
    public class PipelineAdapterTests
    {
        private const int TimeoutMilliseconds = 5000;

        /// <summary>发送委托同步抛错时，适配器应翻译成 ChannelClosedException。</summary>
        [TestMethod]
        public async Task PipelineRawByteStream_SendAsync_TranslatesSyncException()
        {
            var owner = new TrackingDisposable();
            var stream = new PipelineRawByteStream(
                "sync-fault",
                new Pipe().Reader,
                new ThrowingFlowControl(),
                new ThrowingServerSocket());

            await ExpectChannelClosedAsync(
                () => stream.SendAsync(new ReadOnlyMemory<byte>(new byte[] { 1 }), owner))
                .ConfigureAwait(false);

            Assert.IsFalse(owner.IsDisposed, "适配器不得自行释放所有权对象。");
        }

        /// <summary>发送委托返回故障 Task 时，适配器应翻译成 ChannelClosedException。</summary>
        [TestMethod]
        public async Task PipelineRawByteStream_SendAsync_TranslatesAsyncFault()
        {
            var owner = new TrackingDisposable();
            var stream = new PipelineRawByteStream(
                "async-fault",
                new Pipe().Reader,
                new ThrowingFlowControl(),
                (data, token) => Task.FromException(new InvalidOperationException("模拟异步发送失败")));

            await ExpectChannelClosedAsync(
                () => stream.SendAsync(new ReadOnlyMemory<byte>(new byte[] { 1 }), owner))
                .ConfigureAwait(false);

            Assert.IsFalse(owner.IsDisposed, "适配器不得自行释放所有权对象。");
        }

        /// <summary>登记存在时移除应成功、返回同一通道并释放通道属性。</summary>
        [TestMethod]
        public void PipelineHostRegistry_TryRemoveChannel_RemovesAndReturnsTrue()
        {
            var registry = new PipelineHostRegistry();
            var channel = new DefaultChannel("remove-hit", null);
            channel.SetAttribute("key", "value");
            registry.Set(channel);

            IChannel removed;
            var result = registry.TryRemoveChannel("remove-hit", out removed);

            Assert.IsTrue(result, "已登记的连接应移除成功。");
            Assert.AreSame(channel, removed, "应返回被移除的通道实例。");
            Assert.AreEqual(0, registry.Count, "移除后登记数应归零。");
            Assert.IsNull(channel.GetAttribute("key"), "移除登记后应释放通道属性。");
        }

        /// <summary>登记不存在时移除应返回 false，输出参数为 null。</summary>
        [TestMethod]
        public void PipelineHostRegistry_TryRemoveChannel_Missing_ReturnsFalse()
        {
            var registry = new PipelineHostRegistry();

            IChannel removed;
            var result = registry.TryRemoveChannel("remove-miss", out removed);

            Assert.IsFalse(result, "未登记的连接不应移除成功。");
            Assert.IsNull(removed, "未命中时应输出 null。");
        }

        private static async Task ExpectChannelClosedAsync(Func<Task> action)
        {
            var assertion = Assert.ThrowsExactlyAsync<ChannelClosedException>(action);
            await WithTimeoutAsync(assertion).ConfigureAwait(false);
        }

        private static async Task WithTimeoutAsync(Task task)
        {
            var winner = await Task.WhenAny(task, Task.Delay(TimeoutMilliseconds)).ConfigureAwait(false);
            Assert.AreSame(task, winner, "异步用例在超时前未完成。");
            await task.ConfigureAwait(false);
        }

        private sealed class TrackingDisposable : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        private sealed class ThrowingFlowControl : IReceiveFlowControl
        {
            public bool IsPaused
            {
                get { return false; }
            }

            public void Pause()
            {
                throw new NotSupportedException();
            }

            public void Resume()
            {
                throw new NotSupportedException();
            }

            public Task CloseAsync(string reason)
            {
                throw new NotSupportedException();
            }
        }

        /// <summary>IServerSocket 测试替身：只让带所有权对象的发送同步抛错。</summary>
        private sealed class ThrowingServerSocket : IServerSocket
        {
            public ISocketOption SocketOption
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            public event OnAcceptedHandler OnAccepted
            {
                add { throw new NotSupportedException(); }
                remove { throw new NotSupportedException(); }
            }

            public event OnErrorHandler OnError
            {
                add { throw new NotSupportedException(); }
                remove { throw new NotSupportedException(); }
            }

            public event OnServerReceiveSpanHandler OnServerReceiveSpan
            {
                add { throw new NotSupportedException(); }
                remove { throw new NotSupportedException(); }
            }

            public event OnDisconnectedHandler OnDisconnected
            {
                add { throw new NotSupportedException(); }
                remove { throw new NotSupportedException(); }
            }

            public SessionManager SessionManager
            {
                get { throw new NotSupportedException(); }
            }

            public void Start(int backlog = 10000)
            {
                throw new NotSupportedException();
            }

            public int ClientCounts
            {
                get { throw new NotSupportedException(); }
            }

            public object GetCurrentObj(string sessionID)
            {
                throw new NotSupportedException();
            }

            public void Send(string sessionID, ReadOnlySpan<byte> data)
            {
                throw new NotSupportedException();
            }

            public void SendAsync(string sessionID, ReadOnlyMemory<byte> data)
            {
                throw new NotSupportedException();
            }

            public void SendAsync(string sessionID, ISocketProtocal protocal)
            {
                throw new NotSupportedException();
            }

            public Task SendAsync(string sessionID, ReadOnlyMemory<byte> data, IDisposable owner)
            {
                throw new InvalidOperationException("模拟同步发送失败");
            }

            public void End(string sessionID, ReadOnlyMemory<byte> data)
            {
                throw new NotSupportedException();
            }

            public void SendAsync(IPEndPoint ipEndPoint, ReadOnlyMemory<byte> data)
            {
                throw new NotSupportedException();
            }

            public void Stop()
            {
                throw new NotSupportedException();
            }

            public void Disconnect(string sessionID)
            {
                throw new NotSupportedException();
            }

            public bool IsDisposed { get; set; }

            public void Dispose()
            {
            }
        }
    }
}