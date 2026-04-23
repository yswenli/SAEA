/****************************************************************************
 * 
   ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| _f 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
   ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ _f 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MQTT.Client.Unsubscribing
*文件名： MqttClientUnsubscribeOptions
*版本号： v26.4.23.1
*唯一标识：182ab2e5-f8b3-4df0-83f3-3cb5f631bb62
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/3/11 16:46:45
*描述：MQTT客户端取消订阅
*
*=====================================================================
*修改标记
*修改时间：2021/3/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MQTT客户端取消订阅
*
*****************************************************************************/

using System.Collections.Generic;
using SAEA.MQTT.Packets;

namespace SAEA.MQTT.Client.Unsubscribing
{
    public class MqttClientUnsubscribeOptions
    {
        public List<string> TopicFilters { get; set; } = new List<string>();

        public List<MqttUserProperty> UserProperties { get; set; } = new List<MqttUserProperty>();
    }
}
