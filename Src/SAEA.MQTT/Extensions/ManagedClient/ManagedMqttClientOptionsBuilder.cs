/****************************************************************************
 * 
   ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MQTT.Extensions.ManagedClient
*文件名： ManagedMqttClientOptionsBuilder
*版本号： v26.4.23.1
*唯一标识：ea7ba2ce-323a-438f-9637-135cf5b0871a
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Server;

namespace SAEA.MQTT.Extensions.ManagedClient
{
    public class ManagedMqttClientOptionsBuilder
    {
        readonly ManagedMqttClientOptions _options = new ManagedMqttClientOptions();
        MqttClientOptionsBuilder _clientOptionsBuilder;

        public ManagedMqttClientOptionsBuilder WithMaxPendingMessages(int value)
        {
            _options.MaxPendingMessages = value;
            return this;
        }

        public ManagedMqttClientOptionsBuilder WithPendingMessagesOverflowStrategy(MqttPendingMessagesOverflowStrategy value)
        {
            _options.PendingMessagesOverflowStrategy = value;
            return this;
        }

        public ManagedMqttClientOptionsBuilder WithAutoReconnectDelay(TimeSpan value)
        {
            _options.AutoReconnectDelay = value;
            return this;
        }

        public ManagedMqttClientOptionsBuilder WithStorage(IManagedMqttClientStorage value)
        {
            _options.Storage = value;
            return this;
        }

        public ManagedMqttClientOptionsBuilder WithClientOptions(IMqttClientOptions value)
        {
            if (_clientOptionsBuilder != null)
            {
                throw new InvalidOperationException("Cannot use client options builder and client options at the same time.");
            }

            _options.ClientOptions = value ?? throw new ArgumentNullException(nameof(value));

            return this;
        }

        public ManagedMqttClientOptionsBuilder WithClientOptions(MqttClientOptionsBuilder builder)
        {
            if (_options.ClientOptions != null)
            {
                throw new InvalidOperationException("Cannot use client options builder and client options at the same time.");
            }

            _clientOptionsBuilder = builder;
            return this;
        }

        public ManagedMqttClientOptionsBuilder WithClientOptions(Action<MqttClientOptionsBuilder> options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            if (_clientOptionsBuilder == null)
            {
                _clientOptionsBuilder = new MqttClientOptionsBuilder();
            }

            options(_clientOptionsBuilder);
            return this;
        }

        public ManagedMqttClientOptionsBuilder WithAutoReconnect()
        {
            _options.AutoReconnect = true;
            return this;
        }

        public ManagedMqttClientOptionsBuilder WithoutAutoReconnect()
        {
            _options.AutoReconnect = false;
            return this;
        }
        
        public ManagedMqttClientOptions Build()
        {
            if (_clientOptionsBuilder != null)
            {
                _options.ClientOptions = _clientOptionsBuilder.Build();
            }

            if (_options.ClientOptions == null)
            {
                throw new InvalidOperationException("The ClientOptions cannot be null.");
            }

            return _options;
        }
    }
}
