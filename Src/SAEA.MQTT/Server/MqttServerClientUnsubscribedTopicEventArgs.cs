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
*命名空间：SAEA.MQTT.Server
*文件名： MqttServerClientUnsubscribedTopicEventArgs
*版本号： v26.4.23.1
*唯一标识：51544f35-5cab-423b-973f-aa3a20506fea
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MQTT服务端类
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MQTT服务端类
*
*****************************************************************************/
using System;

namespace SAEA.MQTT.Server
{
    public class MqttServerClientUnsubscribedTopicEventArgs : EventArgs
    {
        public MqttServerClientUnsubscribedTopicEventArgs(string clientId, string topicFilter)
        {
            ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            TopicFilter = topicFilter ?? throw new ArgumentNullException(nameof(topicFilter));
        }

        public string ClientId { get; }

        public string TopicFilter { get; }
    }
}
