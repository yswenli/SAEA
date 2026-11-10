using System;
using System.Threading;

using SAEA.QueueSocket;
using SAEA.QueueSocket.Model;

namespace SAEA.QueueSocketTest
{
    /// <summary>
    /// 进程内 QueueSocket 服务端测试夹具，提供随机端口、主题命名与客户端工厂
    /// </summary>
    public static class QueueServerHarness
    {
        static readonly object _lock = new object();

        static QServer _server;
        static int _port;

        static long _disconnectedCount;

        static bool _stopped;

        /// <summary>
        /// 服务端监听的 TCP 端口，未启动时为 0
        /// </summary>
        public static int Port { get { lock (_lock) { return _port; } } }

        /// <summary>
        /// 当前运行中的 QServer 实例，未启动或已停止时为 null
        /// </summary>
        public static QServer Server { get { lock (_lock) { return _server; } } }

        /// <summary>
        /// 服务端连接地址，格式为 127.0.0.1:端口
        /// </summary>
        public static string Address { get { return "127.0.0.1:" + Port; } }

        /// <summary>
        /// 服务端累计观察到的客户端断开次数
        /// </summary>
        public static long DisconnectedCount { get { return Interlocked.Read(ref _disconnectedCount); } }

        /// <summary>
        /// 生成带随机后缀的主题名，避免不同用例之间互相干扰
        /// </summary>
        /// <param name="prefix">主题名前缀，通常为用例编号</param>
        /// <returns>前缀加 32 位随机标识的主题名</returns>
        public static string NewTopic(string prefix)
        {
            return prefix + "-" + Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// 在随机空闲端口启动进程内 QServer，重复调用直接返回；调用 Stop 后不可再启动
        /// </summary>
        public static void Start()
        {
            lock (_lock)
            {
                if (_server != null) return;

                if (_stopped) throw new InvalidOperationException("QueueServerHarness cannot be restarted after Stop()");

                _port = TestHarness.GetFreeTcpPort();

                _server = new QServer(port: _port);
                // 服务端不再暴露断开事件，连接收口统计改由处理器链的 ChannelInactive 通知承担。
                _server.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new DisconnectCounter(id2 => Interlocked.Increment(ref _disconnectedCount)));
                _server.Start();
            }

            TestHarness.WaitUntil(() => Port > 0, 3000, 10).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 停止 QServer 并清空实例，可重复调用；停止出错只打印日志
        /// </summary>
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

        /// <summary>
        /// 创建带随机客户端名称的生产者，连接由 Producer 自身按需建立
        /// </summary>
        /// <returns>指向当前服务端地址的生产者，调用方负责释放</returns>
        public static Producer CreateProducer()
        {
            return new Producer("producer-" + Guid.NewGuid().ToString("N"), Address);
        }

        /// <summary>
        /// 创建消费者并订阅指定主题，返回前已开始接收消息
        /// </summary>
        /// <param name="topic">订阅的主题名</param>
        /// <param name="onMessage">收到消息时的处理回调，可为 null；回调内需调用 Dispose 归还缓冲</param>
        /// <returns>已启动的消费者，调用方负责释放</returns>
        public static Consumer CreateConsumer(string topic, Action<QueueMsg> onMessage = null)
        {
            var consumer = new Consumer("consumer-" + Guid.NewGuid().ToString("N"), Address);
            consumer.Subscribe(topic);
            // 接收处理器必须在 Start 之前装配，连接建立后链结构不再变化。
            if (onMessage != null)
            {
                consumer.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new QueueMessageSink(onMessage));
            }
            consumer.Start();
            return consumer;
        }

        /// <summary>
        /// 创建通用 QClient 并连接、订阅指定主题，用于验证退订行为
        /// </summary>
        /// <param name="topic">订阅的主题名</param>
        /// <param name="onMessage">收到消息时的处理回调，可为 null；回调内需调用 Dispose 归还缓冲</param>
        /// <returns>已完成连接与订阅的客户端，调用方负责释放</returns>
        public static QClient CreateSubscriber(string topic, Action<QueueMsg> onMessage = null)
        {
            var client = new QClient("subscriber-" + Guid.NewGuid().ToString("N"), Address);
            if (onMessage != null)
            {
                client.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(new QueueMessageSink(onMessage));
            }
            client.Connect();
            client.Subscribe(topic);
            return client;
        }
    }
}
