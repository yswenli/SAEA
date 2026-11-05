using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Client.Disconnecting;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Server;
using SAEA.MQTT.Tests.TestInfrastructure;

namespace SAEA.MQTT.Tests.SessionExpiry
{
    [TestClass]
    public class SessionExpiryTests
    {
        static readonly DateTimeOffset StartTime = new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);
        static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        FakeMqttTimeProvider _time;
        TestClockScope _clockScope;

        [TestInitialize]
        public void Initialize()
        {
            _time = new FakeMqttTimeProvider(StartTime);
            _clockScope = new TestClockScope(_time);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (_clockScope != null)
            {
                _clockScope.Dispose();
            }
        }

        [TestMethod]
        public async Task ExpiryZero_RemovesSessionImmediately()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync(builder => builder.WithPersistentSessions());

            const string clientId = "expiry-zero";

            var client = await ConnectV5Async(server, clientId, false, 0, timeout.Token);
            await DisconnectAsync(client, timeout.Token);

            await WaitUntilAsync(
                async () => !await HasSessionAsync(server, clientId).ConfigureAwait(false),
                "Session should be removed immediately when the expiry interval is 0.");

            var reconnected = await ConnectV5Async(server, clientId, false, 0, timeout.Token);
            try
            {
                Assert.IsFalse(
                    reconnected.ConnectResult.IsSessionPresent,
                    "CleanStart=0 with no existing session must report SessionPresent=0.");
            }
            finally
            {
                await DisconnectAsync(reconnected, timeout.Token);
            }
        }

        [TestMethod]
        public async Task Expiry30_SessionPresentBeforeExpiry()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync(builder => builder.WithPersistentSessions());

            const string clientId = "expiry-30-present";

            var client = await ConnectV5Async(server, clientId, false, 30, timeout.Token);
            await DisconnectAsync(client, timeout.Token);

            await WaitUntilAsync(
                () => server.ClientSessionsManager.SessionExpiryManager.IsScheduled(clientId),
                "The session expiry task should be scheduled after the connection is closed.");

            _time.Advance(TimeSpan.FromSeconds(29));

            var reconnected = await ConnectV5Async(server, clientId, false, 30, timeout.Token);
            try
            {
                Assert.IsTrue(
                    reconnected.ConnectResult.IsSessionPresent,
                    "The session must still be present 29 seconds after disconnecting with expiry=30.");
            }
            finally
            {
                await DisconnectAsync(reconnected, timeout.Token);
            }
        }

        [TestMethod]
        public async Task Expiry30_SessionGoneAfterExpiry()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync(builder => builder.WithPersistentSessions());

            const string clientId = "expiry-30-gone";

            var client = await ConnectV5Async(server, clientId, false, 30, timeout.Token);
            await DisconnectAsync(client, timeout.Token);

            await WaitUntilAsync(
                () => server.ClientSessionsManager.SessionExpiryManager.IsScheduled(clientId),
                "The session expiry task should be scheduled after the connection is closed.");

            _time.Advance(TimeSpan.FromSeconds(31));
            server.ClientSessionsManager.SessionExpiryManager.RunDueTasks();

            await WaitUntilAsync(
                async () => !await HasSessionAsync(server, clientId).ConfigureAwait(false),
                "The session must be removed after the expiry interval elapsed.");

            var reconnected = await ConnectV5Async(server, clientId, false, 30, timeout.Token);
            try
            {
                Assert.IsFalse(
                    reconnected.ConnectResult.IsSessionPresent,
                    "The session must not be present after it expired.");
            }
            finally
            {
                await DisconnectAsync(reconnected, timeout.Token);
            }
        }

        [TestMethod]
        public async Task ExpiryMaxValue_NeverExpires()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync(builder => builder.WithPersistentSessions());

            const string clientId = "expiry-max";

            var client = await ConnectV5Async(server, clientId, false, uint.MaxValue, timeout.Token);
            await DisconnectAsync(client, timeout.Token);

            // uint.MaxValue 不登记到期任务；等待连接清理完成即可。
            await WaitUntilAsync(
                async () => !await HasConnectionAsync(server, clientId).ConfigureAwait(false),
                "The connection should be cleaned up after disconnecting.");

            Assert.IsFalse(
                server.ClientSessionsManager.SessionExpiryManager.IsScheduled(clientId),
                "No expiry task must be scheduled for uint.MaxValue.");

            _time.Advance(TimeSpan.FromDays(100));
            server.ClientSessionsManager.SessionExpiryManager.RunDueTasks();

            var reconnected = await ConnectV5Async(server, clientId, false, uint.MaxValue, timeout.Token);
            try
            {
                Assert.IsTrue(
                    reconnected.ConnectResult.IsSessionPresent,
                    "A session with expiry=uint.MaxValue must never expire.");
            }
            finally
            {
                await DisconnectAsync(reconnected, timeout.Token);
            }
        }

        [TestMethod]
        public async Task CleanStartOne_OverridesExistingSession()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync(builder => builder.WithPersistentSessions());

            const string clientId = "clean-start-one";

            var persistent = await ConnectV5Async(server, clientId, false, 30, timeout.Token);
            await DisconnectAsync(persistent, timeout.Token);

            await WaitUntilAsync(
                () => server.ClientSessionsManager.SessionExpiryManager.IsScheduled(clientId),
                "The session expiry task should be scheduled after the connection is closed.");

            var cleanClient = await ConnectV5Async(server, clientId, true, null, timeout.Token);
            try
            {
                Assert.IsFalse(
                    cleanClient.ConnectResult.IsSessionPresent,
                    "CleanStart=1 must discard the existing session and report SessionPresent=0.");
            }
            finally
            {
                await DisconnectAsync(cleanClient, timeout.Token);
            }
        }

        [TestMethod]
        public async Task MaximumSessionExpiryInterval_DownAdjusted()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync(builder => builder
                .WithPersistentSessions()
                .WithMaximumSessionExpiryInterval(10));

            const string clientId = "expiry-down-adjusted";

            var client = await ConnectV5Async(server, clientId, false, 30, timeout.Token);
            try
            {
                Assert.AreEqual(
                    10u,
                    client.ConnectResult.SessionExpiryInterval,
                    "The server must down-adjust the requested session expiry interval and report it in CONNACK.");
            }
            finally
            {
                await DisconnectAsync(client, timeout.Token);
            }

            await WaitUntilAsync(
                () => server.ClientSessionsManager.SessionExpiryManager.IsScheduled(clientId),
                "The session expiry task should be scheduled after the connection is closed.");

            _time.Advance(TimeSpan.FromSeconds(11));
            server.ClientSessionsManager.SessionExpiryManager.RunDueTasks();

            await WaitUntilAsync(
                async () => !await HasSessionAsync(server, clientId).ConfigureAwait(false),
                "The session must be removed after the down-adjusted expiry interval elapsed.");
        }

        static async Task<MqttTestClient> ConnectV5Async(
            MqttTestServer server,
            string clientId,
            bool cleanSession,
            uint? sessionExpiryInterval,
            CancellationToken cancellationToken)
        {
            return await MqttTestClient.ConnectAsync(
                server.Port,
                MqttProtocolVersion.V500,
                clientId,
                options =>
                {
                    options.WithCleanSession(cleanSession);
                    if (sessionExpiryInterval.HasValue)
                    {
                        options.WithSessionExpiryInterval(sessionExpiryInterval.Value);
                    }
                },
                cancellationToken).ConfigureAwait(false);
        }

        static async Task DisconnectAsync(MqttTestClient client, CancellationToken cancellationToken)
        {
            try
            {
                if (client.Client.IsConnected)
                {
                    await client.Client
                        .DisconnectAsync(new MqttClientDisconnectOptions(), cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }
        }

        static async Task<bool> HasSessionAsync(MqttTestServer server, string clientId)
        {
            var sessions = await server.Server.GetSessionStatusAsync().ConfigureAwait(false);
            return sessions.Any(s => s.ClientId == clientId);
        }

        static async Task<bool> HasConnectionAsync(MqttTestServer server, string clientId)
        {
            var connections = await server.Server.GetClientStatusAsync().ConfigureAwait(false);
            return connections.Any(c => c.ClientId == clientId);
        }

        static async Task WaitUntilAsync(Func<bool> condition, string message)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < WaitTimeout)
            {
                if (condition())
                {
                    return;
                }

                await Task.Delay(10).ConfigureAwait(false);
            }

            Assert.Fail(message);
        }

        static async Task WaitUntilAsync(Func<Task<bool>> condition, string message)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < WaitTimeout)
            {
                if (await condition().ConfigureAwait(false))
                {
                    return;
                }

                await Task.Delay(10).ConfigureAwait(false);
            }

            Assert.Fail(message);
        }
    }
}