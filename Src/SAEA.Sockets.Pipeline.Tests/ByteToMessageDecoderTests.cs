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
*命名空间：SAEA.Sockets.Pipeline.Tests
*文件名： ByteToMessageDecoderTests
*版本号： net10.0
*唯一标识：94f1391c-21bb-4f45-9b0b-3b69dd19a3dc
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/20 15:28:50
*描述：单条输出快路径、派发顺序与输出容器复用的用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/20 15:28:50
*修改人： yswenli
*版本号： net10.0
*描述：单条输出快路径、派发顺序与输出容器复用的用例
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SAEA.Sockets.Pipeline.Tests
{
    /// <summary>
    /// 解码器基类用例：单条输出走同步快路径时下游任务被原样返回、异常语义与慢路径一致、
    /// 多条输出的派发顺序与余项释放不变，以及输出容器在并发帧之间不复用。
    /// 下游由桩上下文模拟，用例不依赖传输层与端口。
    /// </summary>
    [TestClass]
    public class ByteToMessageDecoderTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 单条输出时基类必须把下游任务原样交回：返回的是同一个 Task 实例，
        /// 只有非 async 的快路径才能做到（async 方法一定会包一层自己的任务）。
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task ChannelReadAsync_单条输出_原样返回下游任务()
        {
            var decoder = new ScriptedDecoder();
            decoder.Outputs.Add(new object());

            var pending = NewPending();
            var ctx = new StubContext(msg => pending.Task);

            var returned = decoder.ChannelReadAsync(ctx, NewFrame());

            Assert.AreSame(pending.Task, returned, "单条输出必须直接返回下游任务，不能包一层新的任务。");
            Assert.IsFalse(returned.IsCompleted, "下游未完成时返回的任务不能是已完成状态。");
            Assert.AreEqual(1, ctx.PlainCalls, "单条输出应恰好派发一次。");

            pending.SetResult(true);
            await returned.WaitAsync(WaitTimeout);
        }

        /// <summary>
        /// 真实链上解码器拿到的是链持有的调用视图：单条输出同样走快路径，
        /// 必须从链持有入口派发，且下游任务仍被原样返回。
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task ChannelReadAsync_单条输出链持有视图_经链持有入口派发并原样返回任务()
        {
            var decoder = new ScriptedDecoder();
            decoder.Outputs.Add(new object());

            var pending = NewPending();
            var ctx = new ChainOwnedStubContext(msg => pending.Task);

            var returned = decoder.ChannelReadAsync(ctx, NewFrame());

            Assert.AreSame(pending.Task, returned, "链持有视图同样必须直接返回下游任务。");
            Assert.AreEqual(1, ctx.ChainOwnedCalls, "解码器输出必须按链持有语义交给下游。");
            Assert.AreEqual(0, ctx.PlainCalls, "按链持有派发时不应再走普通透传入口。");

            pending.SetResult(true);
            await returned.WaitAsync(WaitTimeout);
        }

        /// <summary>
        /// 下游同步抛出异常时，快路径同步冒泡同一个异常对象；调用方看到的类型与实例都与改前一致
        /// （改前由 <c>ExceptionDispatchInfo</c> 重抛，异常实例本来就是同一个）。
        /// </summary>
        [TestMethod]
        public void ChannelReadAsync_单条输出下游同步抛异常_原样冒泡同一实例()
        {
            var decoder = new ScriptedDecoder();
            decoder.Outputs.Add(new object());

            var boom = new InvalidOperationException("downstream-sync-failure");
            var ctx = new StubContext(msg => { throw boom; });

            var caught = Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                decoder.ChannelReadAsync(ctx, NewFrame());
            });

            Assert.AreSame(boom, caught, "同步失败必须冒泡下游抛出的同一个异常实例。");
            Assert.AreEqual("downstream-sync-failure", caught.Message);
        }

        /// <summary>
        /// 下游返回失败任务时，调用方 await 得到的仍是同一个异常实例，类型与消息都不变。
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task ChannelReadAsync_单条输出下游返回失败任务_await后异常类型与消息一致()
        {
            var decoder = new ScriptedDecoder();
            decoder.Outputs.Add(new object());

            var boom = new InvalidOperationException("downstream-faulted-failure");
            var ctx = new StubContext(msg => Task.FromException(boom));

            var returned = decoder.ChannelReadAsync(ctx, NewFrame());

            var caught = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => returned).ConfigureAwait(false);

            Assert.AreSame(boom, caught, "下游任务的异常必须原样交给调用方，不能被包装。");
            Assert.AreEqual("downstream-faulted-failure", caught.Message);
        }

        /// <summary>
        /// 多条输出仍按序派发：第 2 条失败后停止派发，只释放尚未开始的第 3 条，
        /// 已开始派发的第 1、2 条由入站边界负责，基类不再重复释放。
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task ChannelReadAsync_多条输出第二条失败_按序派发且只释放未开始项()
        {
            var first = new DisposableMessage("first");
            var second = new DisposableMessage("second");
            var third = new DisposableMessage("third");

            var decoder = new ScriptedDecoder();
            decoder.Outputs.Add(first);
            decoder.Outputs.Add(second);
            decoder.Outputs.Add(third);

            var dispatched = new List<object>();
            var boom = new InvalidOperationException("second-failure");
            var ctx = new StubContext(msg =>
            {
                dispatched.Add(msg);
                return ReferenceEquals(msg, second) ? Task.FromException(boom) : Task.CompletedTask;
            });

            var caught = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => decoder.ChannelReadAsync(ctx, NewFrame())).ConfigureAwait(false);

            Assert.AreSame(boom, caught, "派发失败必须原样冒泡。");
            CollectionAssert.AreEqual(new List<object> { first, second }, dispatched, "必须按输出顺序派发，失败后不再继续。");
            Assert.IsTrue(third.IsDisposed, "尚未开始的余项必须由基类释放。");
            Assert.IsFalse(first.IsDisposed, "已开始派发的项由入站边界负责，基类不能重复释放。");
            Assert.IsFalse(second.IsDisposed, "失败项同样已开始派发，基类不能重复释放。");
        }

        /// <summary>
        /// 零条输出仍走慢路径：不派发任何消息，返回的任务正常完成。
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task ChannelReadAsync_零条输出_不派发且正常完成()
        {
            var decoder = new ScriptedDecoder();
            var ctx = new StubContext(msg => throw new AssertFailedException("零条输出不应派发任何消息。"));

            var returned = decoder.ChannelReadAsync(ctx, NewFrame());

            Assert.AreEqual(0, ctx.PlainCalls, "零条输出不应派发任何消息。");
            await returned.WaitAsync(WaitTimeout);
            Assert.IsTrue(returned.IsCompletedSuccessfully, "零条输出应返回已完成的任务。");
        }

        /// <summary>
        /// 解码异常仍按原语义处理：先向下游通知一次，再把同一个异常冒泡给调用方，输出照常派发。
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task ChannelReadAsync_解码抛异常_通知下游一次并原样冒泡()
        {
            var message = new object();
            var boom = new InvalidOperationException("decode-failure");

            var decoder = new ScriptedDecoder { DecodeFailure = boom };
            decoder.Outputs.Add(message);

            var ctx = new StubContext(msg => Task.CompletedTask);

            var caught = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => decoder.ChannelReadAsync(ctx, NewFrame())).ConfigureAwait(false);

            Assert.AreSame(boom, caught, "解码异常必须原样冒泡，不能被通知结果替换。");
            Assert.AreSame(boom, ctx.LastNotified, "解码失败必须向下游通知一次。");
            Assert.AreEqual(1, ctx.PlainCalls, "解码失败前已追加的输出仍要派发。");
        }

        /// <summary>
        /// 解码取消不做异常通知，但仍原样冒泡，语义与改前一致。
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task ChannelReadAsync_解码取消_不通知下游仍原样冒泡()
        {
            var boom = new OperationCanceledException("decode-canceled");

            var decoder = new ScriptedDecoder { DecodeFailure = boom };
            var ctx = new StubContext(msg => Task.CompletedTask);

            var caught = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                () => decoder.ChannelReadAsync(ctx, NewFrame())).ConfigureAwait(false);

            Assert.AreSame(boom, caught, "取消除原样冒泡外不做其他处理。");
            Assert.IsNull(ctx.LastNotified, "取消不应向下游通知异常。");
        }

        /// <summary>
        /// 输出容器只在派发结束后才能被下一帧复用：上一帧还挂在下游时，新一帧必须换一份容器，
        /// 两帧不能共用同一个 List；上一帧结束后应重新复用同一份，且进入解码前已清空。
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task ChannelReadAsync_派发未结束时再解一帧_两帧不共用输出容器()
        {
            var firstMessage = new object();
            var pending = NewPending();

            // 第一帧产出两条输出：第 1 条挂起，容器因此一直被占用到派发结束。
            var decoder = new ScriptedDecoder();
            decoder.Outputs.Add(firstMessage);
            decoder.Outputs.Add(new object());

            var ctx = new StubContext(msg => ReferenceEquals(msg, firstMessage) ? pending.Task : Task.CompletedTask);

            var firstTask = decoder.ChannelReadAsync(ctx, NewFrame());
            Assert.IsFalse(firstTask.IsCompleted, "第一帧应挂在下游未完成的任务上，容器此时仍被占用。");

            decoder.Outputs.Clear();
            decoder.Outputs.Add(new object());
            await decoder.ChannelReadAsync(ctx, NewFrame()).WaitAsync(WaitTimeout);

            Assert.AreEqual(2, decoder.SeenOutputs.Count);
            Assert.AreNotSame(decoder.SeenOutputs[0], decoder.SeenOutputs[1],
                "上一帧派发未结束时，新一帧不能共用同一份输出容器。");

            pending.SetResult(true);
            await firstTask.WaitAsync(WaitTimeout);

            await decoder.ChannelReadAsync(ctx, NewFrame()).WaitAsync(WaitTimeout);

            Assert.AreSame(decoder.SeenOutputs[0], decoder.SeenOutputs[2], "上一帧结束后应重新复用同一份容器。");
            Assert.AreEqual(0, decoder.EntryCounts[2], "复用的容器进入解码前必须已清空。");
        }

        /// <summary>创建待完成的任务源；异步续接不能内联执行，避免用例观察到错误的完成时机。</summary>
        /// <returns>尚未完成的任务源</returns>
        private static TaskCompletionSource<bool> NewPending()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        /// <summary>创建一帧供解码器使用；解码器用例不解析内容，只要是非池化背衬的帧即可。</summary>
        /// <returns>一字节的非池化帧</returns>
        private static PooledMessage NewFrame()
        {
            var buffer = new byte[] { 1 };
            return new PooledMessage(buffer, buffer, buffer.Length, false);
        }

        /// <summary>按脚本产出输出的解码器：可指定解码异常，并记录每次拿到的输出容器。</summary>
        private sealed class ScriptedDecoder : ByteToMessageDecoder
        {
            internal readonly List<object> Outputs = new List<object>();

            internal readonly List<List<object>> SeenOutputs = new List<List<object>>();

            internal readonly List<int> EntryCounts = new List<int>();

            /// <summary>解码时要抛出的异常；为 null 表示解码成功。</summary>
            internal Exception DecodeFailure { get; set; }

            /// <summary>记录容器后按脚本追加输出；需要时抛出解码异常。</summary>
            /// <param name="ctx">当前节点上下文</param>
            /// <param name="frame">完整帧</param>
            /// <param name="output">本次解码的输出容器</param>
            protected override void Decode(IChannelHandlerContext ctx, PooledMessage frame, List<object> output)
            {
                SeenOutputs.Add(output);
                EntryCounts.Add(output.Count);

                foreach (var item in Outputs)
                    output.Add(item);

                if (DecodeFailure != null)
                    throw DecodeFailure;
            }
        }

        /// <summary>记录被释放的入站消息，用于断言余项释放与所有权边界。</summary>
        private sealed class DisposableMessage : IDisposable
        {
            private readonly string _name;

            internal DisposableMessage(string name)
            {
                _name = name;
            }

            internal bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }

            public override string ToString()
            {
                return _name;
            }
        }

        /// <summary>模拟下游的桩上下文：按委托决定每次转发返回的任务，并记录异常通知。</summary>
        private class StubContext : IChannelHandlerContext
        {
            protected readonly Func<object, Task> Fire;

            internal StubContext(Func<object, Task> fire)
            {
                Fire = fire;
            }

            /// <summary>最近一次向下游通知的异常。</summary>
            internal Exception LastNotified { get; private set; }

            /// <summary>普通转发入口被调用的次数。</summary>
            internal int PlainCalls { get; private set; }

            public string ChannelId
            {
                get { return "decoder-test"; }
            }

            public IChannel Channel
            {
                get { return null; }
            }

            public IChannelPipeline Pipeline
            {
                get { return null; }
            }

            public IChannelHandler Handler
            {
                get { return null; }
            }

            public CancellationToken CancellationToken
            {
                get { return CancellationToken.None; }
            }

            public Task FireChannelReadAsync(object msg)
            {
                PlainCalls++;
                return Fire(msg);
            }

            public Task WriteAsync(object msg)
            {
                throw new AssertFailedException("解码器用例不应发起出站写。");
            }

            public Task FireExceptionCaughtAsync(Exception ex)
            {
                LastNotified = ex;
                return Task.CompletedTask;
            }

            public Task CloseAfterWriteAsync(string reason)
            {
                throw new AssertFailedException("解码器用例不应发起关闭等待。");
            }
        }

        /// <summary>模拟链上真实调用视图的桩上下文：同时提供链持有转发入口。</summary>
        private sealed class ChainOwnedStubContext : StubContext, IInboundChainOwnedContext
        {
            internal ChainOwnedStubContext(Func<object, Task> fire)
                : base(fire)
            {
            }

            /// <summary>链持有入口被调用的次数。</summary>
            internal int ChainOwnedCalls { get; private set; }

            Task IInboundChainOwnedContext.FireChainOwnedChannelReadAsync(object msg)
            {
                ChainOwnedCalls++;
                return Fire(msg);
            }
        }
    }
}