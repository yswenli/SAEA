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
*命名空间：SAEA.MQTT.Channel
*文件名： IMqttChannel
*版本号： v26.4.23.1
*唯一标识：a37594a6-876c-4ccb-aaf6-f55af1131e6f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttChannel通道类
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

namespace SAEA.MQTT.Channel
{
    /// <summary>
    /// MQTT传输通道抽象
    /// </summary>
    public interface IMqttChannel : IDisposable
    {
        /// <summary>
        /// 通道连接的对端地址
        /// </summary>
        string Endpoint { get; }
        /// <summary>
        /// 是否为加密连接
        /// </summary>
        bool IsSecureConnection { get; }
        /// <summary>
        /// 客户端使用的证书，未配置时为null
        /// </summary>
        X509Certificate2 ClientCertificate { get; }

        /// <summary>
        /// 建立连接
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>连接完成的异步任务</returns>
        Task ConnectAsync(CancellationToken cancellationToken);
        /// <summary>
        /// 断开连接
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>断开完成的异步任务</returns>
        Task DisconnectAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 从通道读取数据
        /// </summary>
        /// <param name="buffer">接收缓冲区</param>
        /// <param name="offset">写入缓冲区的起始偏移</param>
        /// <param name="count">本次最多读取的字节数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>实际读取的字节数，返回0表示对端已关闭连接</returns>
        Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);
        /// <summary>
        /// 向通道写入数据
        /// </summary>
        /// <param name="buffer">待发送缓冲区</param>
        /// <param name="offset">发送数据的起始偏移</param>
        /// <param name="count">发送的字节数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>发送完成的异步任务</returns>
        Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);
    }
}
