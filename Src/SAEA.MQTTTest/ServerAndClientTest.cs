/****************************************************************************
*项目名称：SAEA.MQTTTest
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.MQTTTest
*类 名 称：ServerAndClientTest
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/3/10 19:38:09
*描述：
*=====================================================================
*修改时间：2021/3/10 19:38:09
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using SAEA.MQTT;
using SAEA.MQTT.Client;
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Server;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTTTest
{
    public static class ServerAndClientTest
    {
        public static async Task RunAsync()
        {
            MqttNetConsoleLogger.ForwardToConsole();

            var factory = new MqttFactory();
            var server = factory.CreateMqttServer();
            var client = factory.CreateMqttClient();

            var serverOptions = new MqttServerOptionsBuilder().Build();
            await server.StartAsync(serverOptions);

            var clientOptions = new MqttClientOptionsBuilder().WithTcpServer("localhost").Build();
            await client.ConnectAsync(clientOptions);

            await Task.Delay(Timeout.Infinite);
        }
    }
}
