/****************************************************************************
*项目名称：SAEA.MQTT.Common
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Common
*类 名 称：MqttApplicationMessageExtensions
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 11:00:39
*描述：
*=====================================================================
*修改时间：2019/1/15 11:00:39
*修 改 人： yswenli
*版 本 号： V3.6.2.2
*描    述：
*****************************************************************************/
using SAEA.MQTT.Core.Packets;
using SAEA.MQTT.Model;
using System;
using System.Text;

namespace SAEA.MQTT.Common
{
    public static class MqttApplicationMessageExtensions
    {
        public static MqttApplicationMessage ToApplicationMessage(this MqttPublishPacket publishPacket)
        {
            return new MqttApplicationMessage
            {
                Topic = publishPacket.Topic,
                Payload = publishPacket.Payload,
                QualityOfServiceLevel = publishPacket.QualityOfServiceLevel,
                Retain = publishPacket.Retain
            };
        }

        public static MqttPublishPacket ToPublishPacket(this MqttApplicationMessage applicationMessage)
        {
            if (applicationMessage == null)
            {
                return null;
            }

            return new MqttPublishPacket
            {
                Topic = applicationMessage.Topic,
                Payload = applicationMessage.Payload,
                QualityOfServiceLevel = applicationMessage.QualityOfServiceLevel,
                Retain = applicationMessage.Retain,
                Dup = false
            };
        }

        public static string ConvertPayloadToString(this MqttApplicationMessage applicationMessage)
        {
            if (applicationMessage == null) throw new ArgumentNullException(nameof(applicationMessage));

            if (applicationMessage.Payload == null)
            {
                return null;
            }

            if (applicationMessage.Payload.Length == 0)
            {
                return string.Empty;
            }

            return Encoding.UTF8.GetString(applicationMessage.Payload, 0, applicationMessage.Payload.Length);
        }
    }
}
