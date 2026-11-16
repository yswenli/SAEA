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
*命名空间：SAEA.P2P.Tests.Stun
*文件名： StunTransactionTests
*版本号： net10.0
*唯一标识：9efb8692-5b35-4451-9d5f-32f1ff650bb8
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 16:40:00
*描述：STUN 事务层重传时刻、超时与应答匹配验收
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 16:40:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 STUN 事务层用例
*
*****************************************************************************/
using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Stun;

namespace SAEA.P2P.Tests.Stun
{
    /// <summary>
    /// STUN 事务层：重传时刻表、39.5s 判定、TCP 不重传、事务 ID 唯一与迟到应答。
    /// </summary>
    [TestCategory(P2PTestSuite.Unit)]
    [TestClass]
    public class StunTransactionTests
    {
        /// <summary>
        /// 设计时刻表，单位毫秒：首发加 6 次重传
        /// </summary>
        private static readonly int[] ExpectedOffsets = { 0, 500, 1500, 3500, 7500, 15500, 31500 };

        /// <summary>
        /// 固定事务 ID，便于复现与比对
        /// </summary>
        private static readonly byte[] TransactionId =
        {
            0xB7, 0xE7, 0xA7, 0x01, 0xBC, 0x34, 0xD6, 0x86, 0xFA, 0x87, 0xDF, 0xAE
        };

        /// <summary>
        /// 无应答时按 0/500/1500/3500/7500/15500/31500ms 共发送 7 次
        /// </summary>
        [TestMethod]
        public void StunTransaction_无响应_重传时刻与设计一致()
        {
            var clock = new ManualRetryClock();
            using (var layer = new StunTransactionLayer(clock))
            {
                var request = new StunMessage(StunMessageType.Binding, StunClass.Request, TransactionId);
                var sentAt = new List<int>();

                StunTransaction transaction;
                Assert.IsTrue(
                    layer.TryBegin(request, false, bytes => sentAt.Add(clock.ElapsedMilliseconds), out transaction),
                    "首次注册应成功");

                // 首发发生在注册瞬间
                CollectionAssert.AreEqual(new[] { 0 }, sentAt, "首发应发生在事务开始时");

                for (var i = 1; i < ExpectedOffsets.Length; i++)
                {
                    // 每次只推进到目标时刻前 1ms，验证未到点不补发
                    clock.AdvanceTo(ExpectedOffsets[i] - 1);
                    layer.Tick();
                    Assert.AreEqual(i, sentAt.Count, $"未到 {ExpectedOffsets[i]}ms 时不应重传");

                    clock.AdvanceTo(ExpectedOffsets[i]);
                    layer.Tick();
                    Assert.AreEqual(i + 1, sentAt.Count, $"到 {ExpectedOffsets[i]}ms 时应重传");
                    Assert.AreEqual(ExpectedOffsets[i], sentAt[i], "重传时刻与设计不一致");
                }

                CollectionAssert.AreEqual(ExpectedOffsets, sentAt, "重传时刻表与设计不一致");
            }
        }

        /// <summary>
        /// 最后一次发送后再等 16×500ms，即 39500ms 判超时
        /// </summary>
        [TestMethod]
        public void StunTransaction_超过39_5秒_判失败()
        {
            Assert.AreEqual(39500, StunTransactionTiming.GetTimeoutMilliseconds(StunTransactionTiming.RtoMilliseconds), "默认超时应为 39500ms");

            var clock = new ManualRetryClock();
            using (var layer = new StunTransactionLayer(clock))
            {
                var request = new StunMessage(StunMessageType.Binding, StunClass.Request, TransactionId);
                StunTransaction transaction;
                Assert.IsTrue(layer.TryBegin(request, false, bytes => { }, out transaction), "首次注册应成功");

                clock.AdvanceTo(39499);
                layer.Tick();
                Assert.IsFalse(transaction.IsCompleted, "39499ms 时不应判失败");

                clock.AdvanceTo(39500);
                layer.Tick();
                Assert.IsTrue(transaction.IsCompleted, "39500ms 时应判失败");

                var result = transaction.Completion.Result;
                Assert.AreEqual(StunTransactionFailure.Timeout, result.Failure, "失败原因应为超时");
                Assert.IsFalse(result.Succeeded, "超时不应带应答");
                Assert.AreEqual(StunTransactionTiming.TotalSendCount, result.Attempts, "超时前应已发满 7 次");
                Assert.AreEqual(0, layer.PendingCount, "超时后应从待决表移除");
            }
        }

        /// <summary>
        /// 可靠传输上只发一次，同样在 39500ms 判失败
        /// </summary>
        [TestMethod]
        public void StunTransaction_TCP模式_不重传()
        {
            var clock = new ManualRetryClock();
            using (var layer = new StunTransactionLayer(clock))
            {
                var request = new StunMessage(StunMessageType.Binding, StunClass.Request, TransactionId);
                var sendCount = 0;

                StunTransaction transaction;
                Assert.IsTrue(layer.TryBegin(request, true, bytes => sendCount++, out transaction), "首次注册应成功");

                clock.AdvanceTo(20000);
                layer.Tick();
                Assert.AreEqual(1, sendCount, "可靠传输上不应重传");

                clock.AdvanceTo(39499);
                layer.Tick();
                Assert.IsFalse(transaction.IsCompleted, "39499ms 时不应判失败");

                clock.AdvanceTo(39500);
                layer.Tick();
                Assert.AreEqual(1, sendCount, "可靠传输上超时前也不应重传");
                Assert.AreEqual(StunTransactionFailure.Timeout, transaction.Completion.Result.Failure, "可靠传输仍应按 39.5s 超时");
            }
        }

        /// <summary>
        /// 同一事务 ID 只允许一个未决请求
        /// </summary>
        [TestMethod]
        public void StunTransaction_同ID重复注册_被拒绝()
        {
            var clock = new ManualRetryClock();
            using (var layer = new StunTransactionLayer(clock))
            {
                var request = new StunMessage(StunMessageType.Binding, StunClass.Request, TransactionId);

                StunTransaction first;
                Assert.IsTrue(layer.TryBegin(request, false, bytes => { }, out first), "首次注册应成功");

                StunTransaction second;
                Assert.IsFalse(layer.TryBegin(request, false, bytes => { }, out second), "同 ID 重复注册应被拒绝");
                Assert.IsNull(second, "被拒绝时不应产出事务");
                Assert.AreEqual(1, layer.PendingCount, "待决表应只有一条");
            }
        }

        /// <summary>
        /// 事务超时后到达的应答被丢弃，不改变已有结果
        /// </summary>
        [TestMethod]
        public void StunTransaction_迟到响应_不影响已结束事务()
        {
            var clock = new ManualRetryClock();
            using (var layer = new StunTransactionLayer(clock))
            {
                var request = new StunMessage(StunMessageType.Binding, StunClass.Request, TransactionId);
                StunTransaction transaction;
                Assert.IsTrue(layer.TryBegin(request, false, bytes => { }, out transaction), "首次注册应成功");

                clock.AdvanceTo(39500);
                layer.Tick();
                Assert.AreEqual(StunTransactionFailure.Timeout, transaction.Completion.Result.Failure, "应先判超时");

                var late = new StunMessage(StunMessageType.Binding, StunClass.SuccessResponse, TransactionId);
                Assert.IsFalse(layer.HandleResponse(late), "迟到应答不应完成任何事务");
                Assert.AreEqual(StunTransactionFailure.Timeout, transaction.Completion.Result.Failure, "迟到应答不应改写已结束事务的结果");
                Assert.AreEqual(StunTransactionState.Failed, transaction.State, "状态应保持失败");
            }
        }

        /// <summary>
        /// 事务 ID 相同的成功应答应完成事务
        /// </summary>
        [TestMethod]
        public void StunTransaction_匹配应答_完成事务()
        {
            var clock = new ManualRetryClock();
            using (var layer = new StunTransactionLayer(clock))
            {
                var request = new StunMessage(StunMessageType.Binding, StunClass.Request, TransactionId);
                StunTransaction transaction;
                Assert.IsTrue(layer.TryBegin(request, false, bytes => { }, out transaction), "首次注册应成功");

                var response = new StunMessage(StunMessageType.Binding, StunClass.SuccessResponse, TransactionId);
                Assert.IsTrue(layer.HandleResponse(response), "同 ID 应答应完成事务");

                var result = transaction.Completion.Result;
                Assert.IsTrue(result.Succeeded, "应标记为收到应答");
                Assert.AreSame(response, result.Response, "结果应携带原应答对象");
                Assert.AreEqual(StunTransactionState.Completed, transaction.State, "状态应为已完成");
                Assert.AreEqual(0, layer.PendingCount, "完成后的应答应从待决表移除");

                Assert.IsFalse(layer.HandleResponse(response), "已完成事务不应再被同一应答完成");
            }
        }

        /// <summary>
        /// 可手工推进的时钟；时间从固定起点单调增加
        /// </summary>
        private sealed class ManualRetryClock : IRetryClock
        {
            /// <summary>
            /// 时钟起点
            /// </summary>
            private readonly DateTime _origin = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);

            /// <summary>
            /// 当前已推进的毫秒数
            /// </summary>
            private int _elapsedMilliseconds;

            /// <summary>
            /// 当前 UTC 时间
            /// </summary>
            public DateTime UtcNow
            {
                get { return _origin.AddMilliseconds(_elapsedMilliseconds); }
            }

            /// <summary>
            /// 当前推进到的毫秒数
            /// </summary>
            public int ElapsedMilliseconds
            {
                get { return _elapsedMilliseconds; }
            }

            /// <summary>
            /// 推进到指定毫秒数；只允许向前
            /// </summary>
            /// <param name="milliseconds">目标毫秒数</param>
            public void AdvanceTo(int milliseconds)
            {
                if (milliseconds < _elapsedMilliseconds)
                {
                    throw new ArgumentOutOfRangeException(nameof(milliseconds));
                }
                _elapsedMilliseconds = milliseconds;
            }
        }
    }
}