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
*命名空间：SAEA.MQTT.Extensions.ManagedClient
*文件名： ManagedMqttClientOptions
*版本号： v26.4.23.1
*唯一标识：1c11b71d-e2a4-4bfe-8535-bde6bfd1c619
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Server;

namespace SAEA.MQTT.Extensions.ManagedClient
{
    public class ManagedMqttClientOptions : IManagedMqttClientOptions
    {
        public IMqttClientOptions ClientOptions { get; set; }

        public bool AutoReconnect { get; set; } = true;

        public TimeSpan AutoReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);

        public TimeSpan ConnectionCheckInterval { get; set; } = TimeSpan.FromSeconds(1);

        public IManagedMqttClientStorage Storage { get; set; }

        public int MaxPendingMessages { get; set; } = int.MaxValue;

        public MqttPendingMessagesOverflowStrategy PendingMessagesOverflowStrategy { get; set; } = MqttPendingMessagesOverflowStrategy.DropNewMessage;
    }
}
