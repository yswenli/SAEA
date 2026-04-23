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
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MQTT内部工具类
*
*****************************************************************************/
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using SAEA.MQTT.Channel;

namespace SAEA.MQTT.Internal
{
    public class TestMqttChannel : IMqttChannel
    {
        readonly MemoryStream _stream;

        public TestMqttChannel(MemoryStream stream)
        {
            _stream = stream;
        }

        public string Endpoint { get; } = "<Test channel>";

        public bool IsSecureConnection { get; } = false;

        public X509Certificate2 ClientCertificate { get; }

        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }

        public Task DisconnectAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }

        public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _stream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _stream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public void Dispose()
        {
        }
    }
}
