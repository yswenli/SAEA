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
*命名空间：SAEA.MQTT.Client
*文件名： IMqttClient
*版本号： v26.4.23.1
*唯一标识：13709b4a-512f-4c67-87e7-a1196481bdd2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttClient接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.MQTT.Client.Connecting;
using SAEA.MQTT.Client.Disconnecting;
using SAEA.MQTT.Client.ExtendedAuthenticationExchange;
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Client.Subscribing;
using SAEA.MQTT.Client.Unsubscribing;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.Client
{
    /// <summary>
    /// MQTT 客户端，提供连接、发布、订阅、取消订阅与断开等操作
    /// </summary>
    public interface IMqttClient : IApplicationMessageReceiver, IApplicationMessagePublisher, IDisposable
    {
        /// <summary>
        /// 当前是否已连接且未处于断开过程中
        /// </summary>
        bool IsConnected { get; }
        /// <summary>
        /// 最近一次 ConnectAsync 使用的选项，尚未调用过 ConnectAsync 时为 null
        /// </summary>
        IMqttClientOptions Options { get; }

        /// <summary>
        /// 连接建立后的回调处理器，null 表示不处理
        /// </summary>
        IMqttClientConnectedHandler ConnectedHandler { get; set; }

        /// <summary>
        /// 连接断开后的回调处理器，null 表示不处理
        /// </summary>
        IMqttClientDisconnectedHandler DisconnectedHandler { get; set; }

        /// <summary>
        /// 连接服务端并完成 MQTT 认证
        /// </summary>
        /// <param name="options">连接选项，不能为 null，且必须设置 ChannelOptions</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>服务端返回的连接认证结果；认证失败抛出异常</returns>
        Task<MqttClientAuthenticateResult> ConnectAsync(IMqttClientOptions options, CancellationToken cancellationToken);
        /// <summary>
        /// 发送 DISCONNECT 报文并释放连接资源
        /// </summary>
        /// <param name="options">断开选项，不能为 null</param>
        /// <param name="cancellationToken">取消令牌，仅作用于发送 DISCONNECT 报文</param>
        /// <returns>断开完成的任务</returns>
        Task DisconnectAsync(MqttClientDisconnectOptions options, CancellationToken cancellationToken);
        /// <summary>
        /// 发送 PINGREQ 并等待服务端的 PINGRESP
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>收到 PINGRESP 后完成的任务</returns>
        Task PingAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 回复服务端的 AUTH 报文，继续 MQTT 5.0 增强认证交换
        /// </summary>
        /// <param name="data">要发送的认证数据，不能为 null</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>报文发出后完成的任务</returns>
        Task SendExtendedAuthenticationExchangeDataAsync(MqttExtendedAuthenticationExchangeData data, CancellationToken cancellationToken);
        /// <summary>
        /// 发送订阅请求并等待服务端应答
        /// </summary>
        /// <param name="options">订阅选项，不能为 null</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>包含各主题过滤器授权 QoS 的订阅结果</returns>
        Task<MqttClientSubscribeResult> SubscribeAsync(MqttClientSubscribeOptions options, CancellationToken cancellationToken);
        /// <summary>
        /// 发送取消订阅请求并等待服务端应答
        /// </summary>
        /// <param name="options">取消订阅选项，不能为 null</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>包含各主题过滤器处理结果的取消订阅结果</returns>
        Task<MqttClientUnsubscribeResult> UnsubscribeAsync(MqttClientUnsubscribeOptions options, CancellationToken cancellationToken);
    }
}