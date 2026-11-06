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
*命名空间：SAEA.MQTT.Internal
*文件名： TestMqttChannel
*版本号： v26.4.23.1
*唯一标识：a2c9b4f6-b073-48f2-a58e-7d85da7966e1
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MQTT内部工具类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using SAEA.MQTT.Channel;

namespace SAEA.MQTT.Internal
{
    /// <summary>
    /// 基于内存流的内存通道，用于脱离网络模拟 IMqttChannel 收发
    /// </summary>
    public class TestMqttChannel : IMqttChannel
    {
        readonly MemoryStream _stream;

        /// <summary>
        /// 创建基于指定内存流的测试通道
        /// </summary>
        /// <param name="stream">承载读写数据的内存流，不能为 null</param>
        public TestMqttChannel(MemoryStream stream)
        {
            _stream = stream;
        }

        /// <summary>
        /// 通道端点描述，固定为占位文本 "&lt;Test channel&gt;"
        /// </summary>
        public string Endpoint { get; } = "<Test channel>";

        /// <summary>
        /// 是否为安全连接，测试通道恒为 false
        /// </summary>
        public bool IsSecureConnection { get; } = false;

        /// <summary>
        /// 客户端证书，测试通道不使用证书，恒为 null
        /// </summary>
        public X509Certificate2 ClientCertificate { get; }

        /// <summary>
        /// 测试实现，不建立连接，直接返回已完成任务
        /// </summary>
        /// <param name="cancellationToken">未使用</param>
        /// <returns>已完成的空任务</returns>
        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }

        /// <summary>
        /// 测试实现，不断开连接，直接返回已完成任务
        /// </summary>
        /// <param name="cancellationToken">未使用</param>
        /// <returns>已完成的空任务</returns>
        public Task DisconnectAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }

        /// <summary>
        /// 从内存流读取数据
        /// </summary>
        /// <param name="buffer">接收数据的缓冲区</param>
        /// <param name="offset">写入缓冲区的起始偏移</param>
        /// <param name="count">最多读取的字节数</param>
        /// <param name="cancellationToken">取消读取的令牌</param>
        /// <returns>实际读取的字节数</returns>
        public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _stream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        /// <summary>
        /// 将数据写入内存流
        /// </summary>
        /// <param name="buffer">待写入的数据缓冲区</param>
        /// <param name="offset">缓冲区中的起始偏移</param>
        /// <param name="count">写入的字节数</param>
        /// <param name="cancellationToken">取消写入的令牌</param>
        /// <returns>写入完成的异步任务</returns>
        public Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _stream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        /// <summary>
        /// 测试实现，不释放底层内存流
        /// </summary>
        public void Dispose()
        {
        }
    }
}
