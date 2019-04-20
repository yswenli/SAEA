/****************************************************************************
*项目名称：SAEA.MQTT.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Model
*类 名 称：MqttClientMessageQueueInterceptorContext
*版 本 号： v4.3.3.7
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 10:23:56
*描述：
*=====================================================================
*修改时间：2019/1/15 10:23:56
*修 改 人： yswenli
*版 本 号： v4.3.3.7
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.MQTT.Model
{
    public class MqttClientMessageQueueInterceptorContext
    {
        public MqttClientMessageQueueInterceptorContext(string senderClientId, string receiverClientId, MqttMessage applicationMessage)
        {
            SenderClientId = senderClientId;
            ReceiverClientId = receiverClientId;
            ApplicationMessage = applicationMessage;
        }

        public string SenderClientId { get; }

        public string ReceiverClientId { get; }

        public MqttMessage ApplicationMessage { get; set; }

        public bool AcceptEnqueue { get; set; } = true;
    }
}
