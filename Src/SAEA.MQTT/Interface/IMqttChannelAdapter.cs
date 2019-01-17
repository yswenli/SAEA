/****************************************************************************
*项目名称：SAEA.MQTT.Interface
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Interface
*类 名 称：IMqttChannelAdapter
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 10:12:48
*描述：
*=====================================================================
*修改时间：2019/1/15 10:12:48
*修 改 人： yswenli
*版 本 号： V3.6.2.2
*描    述：
*****************************************************************************/
using SAEA.MQTT.Common.Serializer;
using SAEA.MQTT.Core.Packets;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.Interface
{
    public interface IMqttChannelAdapter : IDisposable
    {
        string Endpoint { get; }

        IMqttPacketSerializer PacketSerializer { get; }

        event EventHandler ReadingPacketStarted;

        event EventHandler ReadingPacketCompleted;

        Task ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken);

        Task DisconnectAsync(TimeSpan timeout, CancellationToken cancellationToken);

        Task SendPacketAsync(MqttBasePacket packet, CancellationToken cancellationToken);

        Task<MqttBasePacket> ReceivePacketAsync(TimeSpan timeout, CancellationToken cancellationToken);
    }
}
