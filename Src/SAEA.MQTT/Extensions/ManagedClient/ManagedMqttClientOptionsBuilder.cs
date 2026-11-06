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
*唯一标识：9225cdcf-2a25-475a-b4a2-070231058e17
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：ManagedMqttClientOptionsBuilder接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Server;

namespace SAEA.MQTT.Extensions.ManagedClient
{
    /// <summary>
    /// 托管客户端选项的构造器
    /// </summary>
    public class ManagedMqttClientOptionsBuilder
    {
        readonly ManagedMqttClientOptions _options = new ManagedMqttClientOptions();
        MqttClientOptionsBuilder _clientOptionsBuilder;

        /// <summary>
        /// 设置待发队列允许的最大消息数
        /// </summary>
        /// <param name="value">最大消息数，负数等同于不限制</param>
        /// <returns>当前构造器</returns>
        public ManagedMqttClientOptionsBuilder WithMaxPendingMessages(int value)
        {
            _options.MaxPendingMessages = value;
            return this;
        }

        /// <summary>
        /// 设置待发队列的溢出处理策略
        /// </summary>
        /// <param name="value">溢出处理策略</param>
        /// <returns>当前构造器</returns>
        public ManagedMqttClientOptionsBuilder WithPendingMessagesOverflowStrategy(MqttPendingMessagesOverflowStrategy value)
        {
            _options.PendingMessagesOverflowStrategy = value;
            return this;
        }

        /// <summary>
        /// 设置重连失败后再次尝试的间隔
        /// </summary>
        /// <param name="value">重连间隔</param>
        /// <returns>当前构造器</returns>
        public ManagedMqttClientOptionsBuilder WithAutoReconnectDelay(TimeSpan value)
        {
            _options.AutoReconnectDelay = value;
            return this;
        }

        /// <summary>
        /// 设置待发消息的持久化存储
        /// </summary>
        /// <param name="value">存储实现，为null表示不持久化</param>
        /// <returns>当前构造器</returns>
        public ManagedMqttClientOptionsBuilder WithStorage(IManagedMqttClientStorage value)
        {
            _options.Storage = value;
            return this;
        }

        /// <summary>
        /// 直接指定内部客户端选项，不能与客户端选项构造器方式混用
        /// </summary>
        /// <param name="value">内部客户端选项，不能为null</param>
        /// <returns>当前构造器</returns>
        public ManagedMqttClientOptionsBuilder WithClientOptions(IMqttClientOptions value)
        {
            if (_clientOptionsBuilder != null)
            {
                throw new InvalidOperationException("Cannot use client options builder and client options at the same time.");
            }

            _options.ClientOptions = value ?? throw new ArgumentNullException(nameof(value));

            return this;
        }

        /// <summary>
        /// 指定内部客户端选项构造器，不能与直接设置客户端选项的方式混用
        /// </summary>
        /// <param name="builder">客户端选项构造器</param>
        /// <returns>当前构造器</returns>
        public ManagedMqttClientOptionsBuilder WithClientOptions(MqttClientOptionsBuilder builder)
        {
            if (_options.ClientOptions != null)
            {
                throw new InvalidOperationException("Cannot use client options builder and client options at the same time.");
            }

            _clientOptionsBuilder = builder;
            return this;
        }

        /// <summary>
        /// 用回调向客户端选项构造器累加配置，可多次调用
        /// </summary>
        /// <param name="options">客户端选项配置回调，不能为null</param>
        /// <returns>当前构造器</returns>
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

        /// <summary>
        /// 启用断线自动重连
        /// </summary>
        /// <returns>当前构造器</returns>
        public ManagedMqttClientOptionsBuilder WithAutoReconnect()
        {
            _options.AutoReconnect = true;
            return this;
        }

        /// <summary>
        /// 关闭断线自动重连
        /// </summary>
        /// <returns>当前构造器</returns>
        public ManagedMqttClientOptionsBuilder WithoutAutoReconnect()
        {
            _options.AutoReconnect = false;
            return this;
        }
        
        /// <summary>
        /// 生成托管客户端选项，多次调用返回同一个实例
        /// </summary>
        /// <returns>已填充的托管客户端选项</returns>
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
