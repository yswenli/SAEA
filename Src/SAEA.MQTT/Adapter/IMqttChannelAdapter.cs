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
*唯一标识：be2e600f-ff50-468a-b141-9fbcaf45234e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MQTT通道适配器类
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MQTT通道适配器类
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
    public interface IMqttChannelAdapter : IDisposable
    {
        string Endpoint { get; }

        bool IsSecureConnection { get; }

        X509Certificate2 ClientCertificate { get; }

        MqttPacketFormatterAdapter PacketFormatterAdapter { get; }

        long BytesSent { get; }

        long BytesReceived { get; }

        bool IsReadingPacket { get; }

        Task ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken);

        Task DisconnectAsync(TimeSpan timeout, CancellationToken cancellationToken);

        Task SendPacketAsync(MqttBasePacket packet, TimeSpan timeout, CancellationToken cancellationToken);

        Task<MqttBasePacket> ReceivePacketAsync(CancellationToken cancellationToken);

        void ResetStatistics();
    }
}
