/****************************************************************************
*项目名称：SAEA.MQTT.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Model
*类 名 称：MqttClientCredentials
*版 本 号： v4.5.6.7
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/14 19:13:58
*描述：
*=====================================================================
*修改时间：2019/1/14 19:13:58
*修 改 人： yswenli
*版 本 号： v4.5.6.7
*描    述：
*****************************************************************************/
using SAEA.MQTT.Interface;

namespace SAEA.MQTT.Model
{
    public class MqttClientCredentials: IMqttClientCredentials
    {
        public string Username { get; set; }

        public string Password { get; set; }
    }
}
