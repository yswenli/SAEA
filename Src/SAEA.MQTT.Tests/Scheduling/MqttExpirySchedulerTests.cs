using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Internal;
using SAEA.MQTT.Tests.TestInfrastructure;

namespace SAEA.MQTT.Tests.Scheduling
{
    /// <summary>
    /// 到期任务调度器的排序、取消、一次性执行与异常隔离行为
    /// </summary>
    [TestClass]
    public class MqttExpirySchedulerTests
    {
        static readonly DateTimeOffset StartTime = new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);

        FakeMqttTimeProvider _time;
        TestClockScope _clockScope;
        MqttExpiryScheduler _scheduler;

        /// <summary>
        /// 安装假时钟并新建一个调度器实例
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _time = new FakeMqttTimeProvider(StartTime);
            _clockScope = new TestClockScope(_time);
            _scheduler = new MqttExpiryScheduler();
        }

        /// <summary>
        /// 先释放调度器再恢复时间提供者，避免后台线程在时钟回退后继续运行
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            if (_scheduler != null)
            {
                _scheduler.Dispose();
            }

            if (_clockScope != null)
            {
                _clockScope.Dispose();
            }
        }

        /// <summary>
        /// 验证到期的多个任务按到期时间先后执行，而非按注册顺序
        /// </summary>
        [TestMethod]
        public void RunDueTasks_ExecutesTasksInDueOrder()
        {
            var executed = new List<string>();
            var syncRoot = new object();

            _scheduler.Schedule(_time.UtcNow.AddSeconds(3), () => Record(executed, syncRoot, "A"));
            _scheduler.Schedule(_time.UtcNow.AddSeconds(1), () => Record(executed, syncRoot, "B"));
            _scheduler.Schedule(_time.UtcNow.AddSeconds(2), () => Record(executed, syncRoot, "C"));

            _scheduler.Start();
            _time.Advance(TimeSpan.FromSeconds(3));
            _scheduler.RunDueTasks();

            lock (syncRoot)
            {
                CollectionAssert.AreEqual(new[] { "B", "C", "A" }, executed);
            }
        }

        /// <summary>
        /// 验证未到期任务不会执行，越过到期时间后才执行且仅执行一次
        /// </summary>
        [TestMethod]
        public void RunDueTasks_DoesNotExecuteTaskBeforeDue()
        {
            var executionCount = 0;

            _scheduler.Schedule(_time.UtcNow.AddSeconds(5), () => executionCount++);

            _time.Advance(TimeSpan.FromSeconds(4));
            _scheduler.RunDueTasks();
            Assert.AreEqual(0, executionCount);

            _time.Advance(TimeSpan.FromSeconds(2));
            _scheduler.RunDueTasks();
            Assert.AreEqual(1, executionCount);
        }

        /// <summary>
        /// 验证已取消（句柄已释放）的任务即使到期也不执行
        /// </summary>
        [TestMethod]
        public void RunDueTasks_SkipsCancelledTask()
        {
            var executed = false;

            var handle = _scheduler.Schedule(_time.UtcNow.AddSeconds(1), () => executed = true);
            handle.Dispose();

            _time.Advance(TimeSpan.FromSeconds(10));
            _scheduler.RunDueTasks();

            Assert.IsFalse(executed);
        }

        /// <summary>
        /// 验证一次 RunDueTasks 调用会执行所有已到期任务
        /// </summary>
        [TestMethod]
        public void RunDueTasks_ExecutesAllDueTasksInSingleCall()
        {
            var executed = new List<int>();

            _scheduler.Schedule(_time.UtcNow.AddSeconds(1), () => executed.Add(1));
            _scheduler.Schedule(_time.UtcNow.AddSeconds(2), () => executed.Add(2));
            _scheduler.Schedule(_time.UtcNow.AddSeconds(3), () => executed.Add(3));

            _time.Advance(TimeSpan.FromSeconds(10));
            _scheduler.RunDueTasks();

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, executed);
        }

        /// <summary>
        /// 验证某个任务回调抛异常时异常被调度器吞掉，后续到期任务仍继续执行
        /// </summary>
        [TestMethod]
        public void RunDueTasks_ContinuesAfterCallbackThrows()
        {
            var firstExecuted = false;
            var secondExecuted = false;

            _scheduler.Schedule(_time.UtcNow.AddSeconds(1), () =>
            {
                firstExecuted = true;
                throw new InvalidOperationException("boom");
            });
            _scheduler.Schedule(_time.UtcNow.AddSeconds(2), () => secondExecuted = true);

            _time.Advance(TimeSpan.FromSeconds(10));

            // 异常必须在调度器内部被吞掉并记录日志，不能传播到调用方。
            _scheduler.RunDueTasks();

            Assert.IsTrue(firstExecuted);
            Assert.IsTrue(secondExecuted);
        }

        /// <summary>
        /// 验证调度器释放后（重复释放亦安全）不再执行任何到期任务
        /// </summary>
        [TestMethod]
        public void RunDueTasks_DoesNothingAfterSchedulerDisposed()
        {
            var executed = false;

            _scheduler.Schedule(_time.UtcNow.AddSeconds(1), () => executed = true);
            _scheduler.Dispose();
            _scheduler.Dispose();

            _time.Advance(TimeSpan.FromSeconds(10));
            _scheduler.RunDueTasks();

            Assert.IsFalse(executed);
        }

        /// <summary>
        /// 验证到期时间相同的任务按注册顺序执行
        /// </summary>
        [TestMethod]
        public void RunDueTasks_ExecutesSameDueAtInRegistrationOrder()
        {
            var executed = new List<string>();

            _scheduler.Schedule(_time.UtcNow.AddSeconds(1), () => executed.Add("A"));
            _scheduler.Schedule(_time.UtcNow.AddSeconds(1), () => executed.Add("B"));

            _time.Advance(TimeSpan.FromSeconds(1));
            _scheduler.RunDueTasks();

            CollectionAssert.AreEqual(new[] { "A", "B" }, executed);
        }

        /// <summary>
        /// 验证调度器释放后再排程不抛异常，返回的句柄可释放且任务不会被执行
        /// </summary>
        [TestMethod]
        public void Schedule_AfterSchedulerDisposed_DoesNotExecute()
        {
            var executed = false;

            _scheduler.Dispose();
            var handle = _scheduler.Schedule(_time.UtcNow.AddSeconds(1), () => executed = true);

            Assert.IsNotNull(handle);
            handle.Dispose();

            _time.Advance(TimeSpan.FromSeconds(10));
            _scheduler.RunDueTasks();

            Assert.IsFalse(executed);
        }

        /// <summary>
        /// 验证重复调用 Start 不会重复调度同一条任务，回调只执行一次
        /// </summary>
        [TestMethod]
        public void Start_CanBeCalledMultipleTimes()
        {
            var executionCount = 0;

            _scheduler.Schedule(_time.UtcNow.AddSeconds(1), () => executionCount++);
            _scheduler.Start();
            _scheduler.Start();

            _time.Advance(TimeSpan.FromSeconds(1));
            _scheduler.RunDueTasks();

            Assert.AreEqual(1, executionCount);
        }

        static void Record(List<string> executed, object syncRoot, string value)
        {
            lock (syncRoot)
            {
                executed.Add(value);
            }
        }
    }
}