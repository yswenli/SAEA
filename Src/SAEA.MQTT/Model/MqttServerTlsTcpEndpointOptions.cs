/****************************************************************************
*项目名称：SAEA.MQTT.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Model
*类 名 称：MqttServerTlsTcpEndpointOptions
*版 本 号： v4.5.1.2
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 10:25:26
*描述：
*=====================================================================
*修改时间：2019/1/15 10:25:26
*修 改 人： yswenli
*版 本 号： v4.5.1.2
*描    述：
*****************************************************************************/
using System.Security.Authentication;

namespace SAEA.MQTT.Model
{
    public class MqttServerTlsTcpEndpointOptions : MqttServerTcpEndpointBaseOptions
    {
        public MqttServerTlsTcpEndpointOptions()
        {
            Port = 8883;
        }

        public byte[] Certificate { get; set; }


        public SslProtocols SslProtocol { get; set; } = SslProtocols.Tls12;
    }
}
