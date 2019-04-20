/****************************************************************************
*项目名称：SAEA.MQTT.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Model
*类 名 称：MqttMessageInterceptorContext
*版 本 号： v4.3.3.7
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 10:23:30
*描述：
*=====================================================================
*修改时间：2019/1/15 10:23:30
*修 改 人： yswenli
*版 本 号： v4.3.3.7
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.MQTT.Model
{
    public class MqttMessageInterceptorContext
    {
        public MqttMessageInterceptorContext(string clientId, MqttMessage applicationMessage)
        {
            ClientId = clientId;
            ApplicationMessage = applicationMessage;
        }

        public string ClientId { get; }

        public MqttMessage ApplicationMessage { get; set; }

        public bool AcceptPublish { get; set; } = true;

        public bool CloseConnection { get; set; }
    }
}
