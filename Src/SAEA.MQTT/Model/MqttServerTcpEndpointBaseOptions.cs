/****************************************************************************
*项目名称：SAEA.MQTT.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Model
*类 名 称：MqttServerTcpEndpointBaseOptions
*版 本 号： v5.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 10:24:46
*描述：
*=====================================================================
*修改时间：2019/1/15 10:24:46
*修 改 人： yswenli
*版 本 号： v5.0.0.1
*描    述：
*****************************************************************************/
using System.Net;

namespace SAEA.MQTT.Model
{
    public abstract class MqttServerTcpEndpointBaseOptions
    {
        public bool IsEnabled { get; set; }

        public int Port { get; set; } = 1883;

        public int ConnectionBacklog { get; set; } = 10;

        public IPAddress BoundInterNetworkAddress { get; set; } = IPAddress.Any;

        public IPAddress BoundInterNetworkV6Address { get; set; } = IPAddress.IPv6Any;
    }
}
