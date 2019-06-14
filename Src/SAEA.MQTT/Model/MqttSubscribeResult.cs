/****************************************************************************
*项目名称：SAEA.MQTT.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Model
*类 名 称：MqttSubscribeResult
*版 本 号： v4.5.6.7
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 10:10:59
*描述：
*=====================================================================
*修改时间：2019/1/15 10:10:59
*修 改 人： yswenli
*版 本 号： v4.5.6.7
*描    述：
*****************************************************************************/
using SAEA.MQTT.Core.Protocol;

namespace SAEA.MQTT.Model
{
    public class MqttSubscribeResult
    {
        public MqttSubscribeResult(TopicFilter topicFilter, MqttSubscribeReturnCode returnCode)
        {
            TopicFilter = topicFilter;
            ReturnCode = returnCode;
        }

        public TopicFilter TopicFilter { get; }

        public MqttSubscribeReturnCode ReturnCode { get; }
    }
}
