/****************************************************************************
*项目名称：SAEA.MQTT.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Model
*类 名 称：CheckSubscriptionsResult
*版 本 号： V4.1.2.2
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 15:53:46
*描述：
*=====================================================================
*修改时间：2019/1/15 15:53:46
*修 改 人： yswenli
*版 本 号： V4.1.2.2
*描    述：
*****************************************************************************/
using SAEA.MQTT.Core.Protocol;

namespace SAEA.MQTT.Model
{
    public class CheckSubscriptionsResult
    {
        public bool IsSubscribed { get; set; }

        public MqttQualityOfServiceLevel QualityOfServiceLevel { get; set; }
    }
}
