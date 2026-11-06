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
*命名空间：SAEA.MQTT.LowLevelClient
*文件名： ILowLevelMqttClient
*版本号： v26.4.23.1
*唯一标识：a95c1783-48d1-4558-abc7-c1db6ffb6755
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：ILowLevelMqttClient接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Packets;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.LowLevelClient
{
    /// <summary>
    /// 底层MQTT客户端，直接收发MQTT报文，不维护会话与重发状态
    /// </summary>
    public interface ILowLevelMqttClient : IDisposable
    {
        /// <summary>
        /// 建立连接并保存连接选项，已连接时再次调用会抛出异常
        /// </summary>
        /// <param name="options">连接选项，不能为null</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>连接完成的异步任务</returns>
        Task ConnectAsync(IMqttClientOptions options, CancellationToken cancellationToken);

        /// <summary>
        /// 断开连接，未连接时直接返回
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>断开完成的异步任务</returns>
        Task DisconnectAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 发送一个MQTT报文，失败时自动断开连接
        /// </summary>
        /// <param name="packet">待发送报文，不能为null</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>发送完成的异步任务</returns>
        Task SendAsync(MqttBasePacket packet, CancellationToken cancellationToken);

        /// <summary>
        /// 接收一个MQTT报文，失败时自动断开连接
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>接收到的报文</returns>
        Task<MqttBasePacket> ReceiveAsync(CancellationToken cancellationToken);
    }
}
