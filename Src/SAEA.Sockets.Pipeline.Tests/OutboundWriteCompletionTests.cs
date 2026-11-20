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
*文件名： OutboundWriteCompletionTests
*版本号： net10.0
*唯一标识：59fa6419-383c-4102-ae13-cc8b36ad6e2f
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/20 14:10:09
*描述：业务写公共任务的完成形态用例：同步完成当场可用、同步失败交付原异常
*
*=====================================================================
*修改标记
*修改时间：2026/11/20 14:10:09
*修改人： yswenli
*版本号： net10.0
*描述：业务写公共任务的完成形态用例：同步完成当场可用、同步失败交付原异常
*
*****************************************************************************/
using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SAEA.Sockets.Pipeline.Tests
{
    /// <summary>
    /// 业务写完成形态用例：整条出站链在同一次调用内跑完时，公共任务必须当场完成且不留下待写计数；
    /// 同步失败也必须交付下游抛出的原异常实例。下游只装配记录器、不接传输层，避免用例依赖端口与执行器。
    /// </summary>
    [TestClass]
    public class OutboundWriteCompletionTests
    {
        /// <summary>
        /// 同步完成的写（串行器空闲快路径 + 同步下游）返回时公共任务必须已经完成：
        /// 这条路径不建完成源，靠的就是「完成形态在返回前已落地」。
        /// </summary>
        [TestMethod]
        public void WriteAsync_出站链同步完成_公共任务已完成且待写计数归零()
        {
            using (var pipeline = NewPipeline())
            {
                pipeline.AddLast(new SyncRecordingOutboundHandler());
                pipeline.AddLast(new OutboundSerializingHandler());

                var task = pipeline.WriteAsync("m1");

                Assert.AreEqual(TaskStatus.RanToCompletion, task.Status, "同步完成的写必须在返回时就已完成。");
                Assert.AreEqual(0, pipeline.PendingWriteCount, "同步完成后不能留下待写计数，否则收口等待会空转。");
                Assert.AreEqual(0, pipeline.CloseState, "业务写不得改变关闭状态。");
            }
        }

        /// <summary>
        /// 下游同步抛错时，公共任务在返回时已带同一个异常实例失败：
        /// 同步失败必须自己补一个完成源，否则调用方会拿到永远不结束的任务。
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task WriteAsync_下游同步抛错_公共任务带原异常实例()
        {
            using (var pipeline = NewPipeline())
            {
                var error = new TestWriteException("下游同步写失败");
                pipeline.AddLast(new ThrowingOutboundHandler(error));

                var task = pipeline.WriteAsync("m1");

                Assert.IsTrue(task.IsCompleted, "同步失败也必须在返回时就已完成。");
                Assert.AreEqual(TaskStatus.Faulted, task.Status, "非取消类失败必须落成故障任务。");

                var caught = await Assert.ThrowsExceptionAsync<TestWriteException>(() => task).ConfigureAwait(false);
                Assert.AreSame(error, caught, "公共任务必须交付下游抛出的同一个异常实例。");
                Assert.AreEqual(0, pipeline.PendingWriteCount, "失败完成后同样不能留下待写计数。");
            }
        }

        /// <summary>创建一条不接传输层的通道链；业务写预算放开，由各用例自行控制下游。</summary>
        /// <returns>已通过结构校验的空链</returns>
        private static ChannelPipeline NewPipeline()
        {
            return ChannelPipeline.Create(new ChannelPipelineOptions
            {
                Splitter = new TakeAvailableSplitter(64 * 1024),
                QueueCapacity = 8,
                MaxConcurrentMessages = 1,
                MaxPendingWrites = 0
            });
        }

        /// <summary>用例专用的失败类型：按类型断言异常能被原样交付，而不是被包装或改写。</summary>
        internal sealed class TestWriteException : Exception
        {
            /// <summary>以指定消息创建异常。</summary>
            /// <param name="message">异常消息</param>
            internal TestWriteException(string message)
                : base(message)
            {
            }
        }

        /// <summary>同步记录下游：记录消息后立刻返回已完成任务，不向链头转发，因此用例不需要挂传输层。</summary>
        private sealed class SyncRecordingOutboundHandler : OutboundHandlerAdapter
        {
            /// <summary>下游收到的消息条数。</summary>
            internal int ReceivedCount { get; private set; }

            /// <summary>记录消息并同步完成。</summary>
            /// <param name="ctx">当前节点上下文</param>
            /// <param name="msg">待发送消息</param>
            /// <returns>已完成的任务</returns>
            public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
            {
                ReceivedCount++;
                return Task.CompletedTask;
            }
        }

        /// <summary>同步抛错的下游：用来验证同步失败路径仍然交付原异常实例。</summary>
        private sealed class ThrowingOutboundHandler : OutboundHandlerAdapter
        {
            private readonly Exception _error;

            /// <summary>以指定异常创建同步抛错的下游。</summary>
            /// <param name="error">每次写都要抛出的异常实例</param>
            internal ThrowingOutboundHandler(Exception error)
            {
                _error = error;
            }

            /// <summary>同步抛出构造时给定的异常实例。</summary>
            /// <param name="ctx">当前节点上下文</param>
            /// <param name="msg">待发送消息</param>
            /// <returns>本方法不返回，总是抛出构造时给定的异常</returns>
            public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
            {
                throw _error;
            }
        }
    }
}