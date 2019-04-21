/****************************************************************************
*项目名称：SAEA.MQTT.Core
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Model
*类 名 称：TopicFilter
*版 本 号： v4.5.1.2
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/14 19:47:26
*描述：
*=====================================================================
*修改时间：2019/1/14 19:47:26
*修 改 人： yswenli
*版 本 号： v4.5.1.2
*描    述：
*****************************************************************************/
using SAEA.MQTT.Core.Protocol;

namespace SAEA.MQTT.Model
{
    public class TopicFilter
    {
        public TopicFilter(string topic, MqttQualityOfServiceLevel qualityOfServiceLevel)
        {
            Topic = topic;
            QualityOfServiceLevel = qualityOfServiceLevel;
        }

        public string Topic { get; set; }

        public MqttQualityOfServiceLevel QualityOfServiceLevel { get; set; }

        public override string ToString()
        {
            return Topic + "@" + QualityOfServiceLevel;
        }
    }
}
