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
*命名空间：SAEA.MQTT.Adapter
*文件名： IMqttChannelAdapter
*版本号： v26.4.23.1
*唯一标识：d6d31ed5-458d-4d85-a49f-4dc4e5270b5c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttChannelAdapter适配器类
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
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Packets;

namespace SAEA.MQTT.Adapter
{
    /// <summary>
    /// MQTT 信道适配器，屏蔽底层传输实现，统一完成报文的收发与计数
    /// </summary>
    public interface IMqttChannelAdapter : IDisposable
    {
        /// <summary>
        /// 对端地址，格式由具体信道实现决定
        /// </summary>
        string Endpoint { get; }

        /// <summary>
        /// 当前连接是否为加密连接
        /// </summary>
        bool IsSecureConnection { get; }

        /// <summary>
        /// 客户端证书，非双向认证场景为 null
        /// </summary>
        X509Certificate2 ClientCertificate { get; }

        /// <summary>
        /// 报文编解码器，同时记录当前协商的协议版本
        /// </summary>
        MqttPacketFormatterAdapter PacketFormatterAdapter { get; }

        /// <summary>
        /// 累计已发送字节数，调用 ResetStatistics 后归零
        /// </summary>
        long BytesSent { get; }

        /// <summary>
        /// 累计已接收字节数，调用 ResetStatistics 后归零
        /// </summary>
        long BytesReceived { get; }

        /// <summary>
        /// 是否正在读取报文
        /// </summary>
        bool IsReadingPacket { get; }

        /// <summary>
        /// 建立连接
        /// </summary>
        /// <param name="timeout">超时时间，为 TimeSpan.Zero 时不限时</param>
        /// <param name="cancellationToken">取消标记</param>
        /// <returns>连接完成的异步任务</returns>
        Task ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken);

        /// <summary>
        /// 关闭连接
        /// </summary>
        /// <param name="timeout">超时时间，为 TimeSpan.Zero 时不限时</param>
        /// <param name="cancellationToken">取消标记</param>
        /// <returns>关闭完成的异步任务</returns>
        Task DisconnectAsync(TimeSpan timeout, CancellationToken cancellationToken);

        /// <summary>
        /// 编码并发送一个 MQTT 报文
        /// </summary>
        /// <param name="packet">待发送报文</param>
        /// <param name="timeout">发送超时时间，为 TimeSpan.Zero 时不限时</param>
        /// <param name="cancellationToken">取消标记</param>
        /// <returns>发送完成的异步任务</returns>
        Task SendPacketAsync(MqttBasePacket packet, TimeSpan timeout, CancellationToken cancellationToken);

        /// <summary>
        /// 读取并解码一个 MQTT 报文
        /// </summary>
        /// <param name="cancellationToken">取消标记</param>
        /// <returns>解码后的报文，连接关闭或读写被取消时返回 null</returns>
        Task<MqttBasePacket> ReceivePacketAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 将收发字节计数清零
        /// </summary>
        void ResetStatistics();
    }
}
