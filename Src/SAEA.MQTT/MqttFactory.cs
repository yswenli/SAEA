using SAEA.MQTT.Adapter;
using SAEA.MQTT.Client;
using SAEA.MQTT.Diagnostics;
using SAEA.MQTT.Implementations;
using SAEA.MQTT.LowLevelClient;
using SAEA.MQTT.Server;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAEA.MQTT
{
    /// <summary>
    /// MQTT 工厂类，用于创建客户端和服务器实例。
    /// </summary>
    public sealed class MqttFactory : IMqttFactory
    {
        IMqttClientAdapterFactory _clientAdapterFactory;

        /// <summary>
        /// 默认构造函数，使用默认的日志记录器。
        /// </summary>
        public MqttFactory() : this(new MqttNetLogger())
        {
        }

        /// <summary>
        /// 使用指定的日志记录器构造 MQTT 工厂实例。
        /// </summary>
        /// <param name="logger">日志记录器实例。</param>
        public MqttFactory(IMqttNetLogger logger)
        {
            DefaultLogger = logger ?? throw new ArgumentNullException(nameof(logger));
            _clientAdapterFactory = new MqttClientAdapterFactory(logger);
        }

        /// <summary>
        /// 获取默认的日志记录器。
        /// </summary>
        public IMqttNetLogger DefaultLogger { get; }

        /// <summary>
        /// 获取默认的服务器适配器列表。
        /// </summary>
        public IList<Func<IMqttFactory, IMqttServerAdapter>> DefaultServerAdapters { get; } = new List<Func<IMqttFactory, IMqttServerAdapter>>
            {
                factory => new MqttTcpServerAdapter(factory.DefaultLogger)
            };

        /// <summary>
        /// 获取工厂的属性字典。
        /// </summary>
        public IDictionary<object, object> Properties { get; } = new Dictionary<object, object>();

        /// <summary>
        /// 使用指定的客户端适配器工厂。
        /// </summary>
        /// <param name="clientAdapterFactory">客户端适配器工厂实例。</param>
        /// <returns>当前的 MQTT 工厂实例。</returns>
        public IMqttFactory UseClientAdapterFactory(IMqttClientAdapterFactory clientAdapterFactory)
        {
            _clientAdapterFactory = clientAdapterFactory ?? throw new ArgumentNullException(nameof(clientAdapterFactory));
            return this;
        }

        /// <summary>
        /// 创建低级别的 MQTT 客户端。
        /// </summary>
        /// <returns>低级别的 MQTT 客户端实例。</returns>
        public ILowLevelMqttClient CreateLowLevelMqttClient()
        {
            return CreateLowLevelMqttClient(DefaultLogger);
        }

        /// <summary>
        /// 使用指定的日志记录器创建低级别的 MQTT 客户端。
        /// </summary>
        /// <param name="logger">日志记录器实例。</param>
        /// <returns>低级别的 MQTT 客户端实例。</returns>
        public ILowLevelMqttClient CreateLowLevelMqttClient(IMqttNetLogger logger)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            return new LowLevelMqttClient(_clientAdapterFactory, logger);
        }

        /// <summary>
        /// 使用指定的客户端适配器工厂创建低级别的 MQTT 客户端。
        /// </summary>
        /// <param name="clientAdapterFactory">客户端适配器工厂实例。</param>
        /// <returns>低级别的 MQTT 客户端实例。</returns>
        public ILowLevelMqttClient CreateLowLevelMqttClient(IMqttClientAdapterFactory clientAdapterFactory)
        {
            if (clientAdapterFactory == null) throw new ArgumentNullException(nameof(clientAdapterFactory));

            return new LowLevelMqttClient(_clientAdapterFactory, DefaultLogger);
        }

        /// <summary>
        /// 使用指定的日志记录器和客户端适配器工厂创建低级别的 MQTT 客户端。
        /// </summary>
        /// <param name="logger">日志记录器实例。</param>
        /// <param name="clientAdapterFactory">客户端适配器工厂实例。</param>
        /// <returns>低级别的 MQTT 客户端实例。</returns>
        public ILowLevelMqttClient CreateLowLevelMqttClient(IMqttNetLogger logger, IMqttClientAdapterFactory clientAdapterFactory)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            if (clientAdapterFactory == null) throw new ArgumentNullException(nameof(clientAdapterFactory));

            return new LowLevelMqttClient(_clientAdapterFactory, logger);
        }

        /// <summary>
        /// 创建 MQTT 客户端。
        /// </summary>
        /// <returns>MQTT 客户端实例。</returns>
        public IMqttClient CreateMqttClient()
        {
            return CreateMqttClient(DefaultLogger);
        }

        /// <summary>
        /// 使用指定的日志记录器创建 MQTT 客户端。
        /// </summary>
        /// <param name="logger">日志记录器实例。</param>
        /// <returns>MQTT 客户端实例。</returns>
        public IMqttClient CreateMqttClient(IMqttNetLogger logger)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            return new MqttClient(_clientAdapterFactory, logger);
        }

        /// <summary>
        /// 使用指定的客户端适配器工厂创建 MQTT 客户端。
        /// </summary>
        /// <param name="clientAdapterFactory">客户端适配器工厂实例。</param>
        /// <returns>MQTT 客户端实例。</returns>
        public IMqttClient CreateMqttClient(IMqttClientAdapterFactory clientAdapterFactory)
        {
            if (clientAdapterFactory == null) throw new ArgumentNullException(nameof(clientAdapterFactory));

            return new MqttClient(clientAdapterFactory, DefaultLogger);
        }

        /// <summary>
        /// 使用指定的日志记录器和客户端适配器工厂创建 MQTT 客户端。
        /// </summary>
        /// <param name="logger">日志记录器实例。</param>
        /// <param name="clientAdapterFactory">客户端适配器工厂实例。</param>
        /// <returns>MQTT 客户端实例。</returns>
        public IMqttClient CreateMqttClient(IMqttNetLogger logger, IMqttClientAdapterFactory clientAdapterFactory)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            if (clientAdapterFactory == null) throw new ArgumentNullException(nameof(clientAdapterFactory));

            return new MqttClient(clientAdapterFactory, logger);
        }

        /// <summary>
        /// 创建 MQTT 服务器。
        /// </summary>
        /// <returns>MQTT 服务器实例。</returns>
        public IMqttServer CreateMqttServer()
        {
            return CreateMqttServer(DefaultLogger);
        }

        /// <summary>
        /// 使用指定的日志记录器创建 MQTT 服务器。
        /// </summary>
        /// <param name="logger">日志记录器实例。</param>
        /// <returns>MQTT 服务器实例。</returns>
        public IMqttServer CreateMqttServer(IMqttNetLogger logger)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            var serverAdapters = DefaultServerAdapters.Select(a => a.Invoke(this));
            return CreateMqttServer(serverAdapters, logger);
        }

        /// <summary>
        /// 使用指定的服务器适配器和日志记录器创建 MQTT 服务器。
        /// </summary>
        /// <param name="serverAdapters">服务器适配器集合。</param>
        /// <param name="logger">日志记录器实例。</param>
        /// <returns>MQTT 服务器实例。</returns>
        public IMqttServer CreateMqttServer(IEnumerable<IMqttServerAdapter> serverAdapters, IMqttNetLogger logger)
        {
            if (serverAdapters == null) throw new ArgumentNullException(nameof(serverAdapters));
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            return new MqttServer(serverAdapters, logger);
        }

        /// <summary>
        /// 使用指定的服务器适配器创建 MQTT 服务器。
        /// </summary>
        /// <param name="serverAdapters">服务器适配器集合。</param>
        /// <returns>MQTT 服务器实例。</returns>
        public IMqttServer CreateMqttServer(IEnumerable<IMqttServerAdapter> serverAdapters)
        {
            if (serverAdapters == null) throw new ArgumentNullException(nameof(serverAdapters));

            return new MqttServer(serverAdapters, DefaultLogger);
        }
    }
}