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
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：ILowLevelMqttClient接口
*
*****************************************************************************/
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Packets;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTT.LowLevelClient
{
    public interface ILowLevelMqttClient : IDisposable
    {
        Task ConnectAsync(IMqttClientOptions options, CancellationToken cancellationToken);

        Task DisconnectAsync(CancellationToken cancellationToken);

        Task SendAsync(MqttBasePacket packet, CancellationToken cancellationToken);

        Task<MqttBasePacket> ReceiveAsync(CancellationToken cancellationToken);
    }
}
