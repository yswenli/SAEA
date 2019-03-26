/****************************************************************************
*项目名称：SAEA.MQTT.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Model
*类 名 称：MqttClientTcpOptions
*版 本 号： v4.3.2.5
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/14 19:16:21
*描述：
*=====================================================================
*修改时间：2019/1/14 19:16:21
*修 改 人： yswenli
*版 本 号： v4.3.2.5
*描    述：
*****************************************************************************/
using SAEA.MQTT.Common;
using SAEA.MQTT.Interface;

namespace SAEA.MQTT.Model
{
    public class MqttClientTcpOptions: IMqttClientChannelOptions
    {
        public string Server { get; set; }

        public int? Port { get; set; }

        public int BufferSize { get; set; } = 4096;

        public MqttClientTlsOptions TlsOptions { get; set; } = new MqttClientTlsOptions();

        public override string ToString()
        {
            return Server + ":" + this.GetPort();
        }
    }
}
