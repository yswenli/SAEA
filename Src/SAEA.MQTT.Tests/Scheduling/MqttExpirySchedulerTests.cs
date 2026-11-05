using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Internal;
using SAEA.MQTT.Tests.TestInfrastructure;

namespace SAEA.MQTT.Tests.Scheduling
{
    [TestClass]
    public class MqttExpirySchedulerTests
    {
        static readonly DateTimeOffset StartTime = new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);

        FakeMqttTimeProvider _time;
        TestClockScope _clockScope;
        MqttExpiryScheduler _scheduler;

        [TestInitialize]
        public void Initialize()
        {
            _time = new FakeMqttTimeProvider(StartTime);
            _clockScope = new TestClockScope(_time);
            _scheduler = new MqttExpiryScheduler();
        }

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