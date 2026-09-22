using System;
using System.Threading;

using SAEA.QueueSocket;

namespace SAEA.QueueSocketTest
{
    public static class QueueServerHarness
    {
        static readonly object _lock = new object();

        static QServer _server;
        static int _port;

        static long _disconnectedCount;

        static bool _stopped;

        public static int Port { get { lock (_lock) { return _port; } } }

        public static QServer Server { get { lock (_lock) { return _server; } } }

        public static string Address { get { return "127.0.0.1:" + Port; } }

        public static long DisconnectedCount { get { return Interlocked.Read(ref _disconnectedCount); } }

        public static string NewTopic(string prefix)
        {
            return prefix + "-" + Guid.NewGuid().ToString("N");
        }

        public static void Start()
        {
            lock (_lock)
            {
                if (_server != null) return;

                if (_stopped) throw new InvalidOperationException("QueueServerHarness cannot be restarted after Stop()");

                _port = TestHarness.GetFreeTcpPort();

                _server = new QServer(port: _port);
                _server.OnDisconnected += Server_OnDisconnected;
                _server.Start();
            }

            TestHarness.WaitUntil(() => Port > 0, 3000, 10).GetAwaiter().GetResult();
        }

        public static void Stop()
        {
            lock (_lock)
            {
                if (_stopped) return;

                if (_server == null) return;

                try { _server.Stop(); }
                catch (Exception ex) { SAEA.Common.ConsoleHelper.WriteLine("QueueServerHarness.Stop failed: " + ex.Message); }

                _stopped = true;
                _server = null;
            }
        }

        private static void Server_OnDisconnected(string id, Exception ex)
        {
            Interlocked.Increment(ref _disconnectedCount);
        }

        public static Producer CreateProducer()
        {
            return new Producer("producer-" + Guid.NewGuid().ToString("N"), Address);
        }

        public static Consumer CreateConsumer(string topic)
        {
            var consumer = new Consumer("consumer-" + Guid.NewGuid().ToString("N"), Address);
            consumer.Subscribe(topic);
            consumer.Start();
            return consumer;
        }

        public static QClient CreateSubscriber(string topic)
        {
            var client = new QClient("subscriber-" + Guid.NewGuid().ToString("N"), Address);
            client.Connect();
            client.Subscribe(topic);
            return client;
        }
    }
}
