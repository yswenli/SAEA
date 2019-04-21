/****************************************************************************
*项目名称：SAEA.MQTT.Interface
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Interface
*类 名 称：IMqttChannel
*版 本 号： v4.5.1.2
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 15:14:01
*描述：
*=====================================================================
*修改时间：2019/1/15 15:14:01
*修 改 人： yswenli
*版 本 号： v4.5.1.2
*描    述：
*****************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.Interface
{
    public interface IMqttChannel : IDisposable
    {
        string Endpoint { get; }

        Task ConnectAsync();
        Task DisconnectAsync();

        Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);
        Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);
    }
}
