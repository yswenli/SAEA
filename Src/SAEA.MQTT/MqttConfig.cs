/****************************************************************************
*项目名称：SAEA.MQTT
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT
*类 名 称：MqttConfig
*版 本 号： v4.3.1.2
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 16:44:39
*描述：
*=====================================================================
*修改时间：2019/1/15 16:44:39
*修 改 人： yswenli
*版 本 号： v4.3.1.2
*描    述：
*****************************************************************************/

using SAEA.Common;

namespace SAEA.MQTT
{
    public class MqttConfig
    {
        public string Server { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }

        public int Port { get; set; }

        public int SslPort { get; set; }

        public int WebSocketPort { get; set; }

        public int SslWebSocketPort { get; set; }

        public static MqttConfig Parse(string json)
        {
            return SerializeHelper.Deserialize<MqttConfig>(json);
        }

        public new string ToString()
        {
            return SerializeHelper.Serialize(this);
        }
    }
}
