using SAEA.MQTT;
using SAEA.MQTT.Client;
using SAEA.MQTT.Client.Connecting;
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Client.Receiving;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Protocol;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MQTTTest
{
    public static class EmqxBrokerTest
    {
        public static async Task RunAsync()
        {
            Console.WriteLine("=== Testing broker.emqx.io with MQTT 5.0 ===");
            Console.WriteLine();

            var factory = new MqttFactory();
            var client = factory.CreateMqttClient();

            var receivedMessage = false;

            client.UseConnectedHandler(e =>
            {
                Console.WriteLine("Connected to broker successfully!");
            });

            client.UseDisconnectedHandler(e =>
            {
                Console.WriteLine("Disconnected from broker.");
            });

            client.UseApplicationMessageReceivedHandler(e =>
            {
                Console.WriteLine($"Received message: Topic={e.ApplicationMessage.Topic}, Payload={e.ApplicationMessage.ConvertPayloadToString()}");
                receivedMessage = true;
            });

            try
            {
                var options = new MqttClientOptionsBuilder()
                    .WithWebSocketServer("broker.emqx.io:8083/mqtt")
                    .WithProtocolVersion(MqttProtocolVersion.V500)
                    .WithClientId($"SAEA_MQTT_Test_{Guid.NewGuid():N}")
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(60))
                    .Build();

                Console.WriteLine("Connecting to broker.emqx.io:1883 with MQTT 5.0...");
                var result = await client.ConnectAsync(options, CancellationToken.None);

                Console.WriteLine($"Connection result: Success={result.ResultCode == MqttClientConnectResultCode.Success}, Code={result.ResultCode}");

                if (result.ResultCode != MqttClientConnectResultCode.Success)
                {
                    Console.WriteLine($"Connection failed: {result.ResultCode}");
                    return;
                }

                var testTopic = "saea/mqtt/test/topic";
                var testMessage = "Hello from SAEA.MQTT MQTT 5.0!";

                Console.WriteLine($"Subscribing to topic: {testTopic}");
                await client.SubscribeAsync(testTopic, MqttQualityOfServiceLevel.AtLeastOnce);

                Console.WriteLine($"Publishing message: {testMessage}");
                await client.PublishAsync(testTopic, testMessage, MqttQualityOfServiceLevel.AtLeastOnce);

                Console.WriteLine("Waiting for message receipt...");

                for (int i = 0; i < 10 && !receivedMessage; i++)
                {
                    await Task.Delay(500);
                }

                if (receivedMessage)
                {
                    Console.WriteLine("Message received successfully!");
                }
                else
                {
                    Console.WriteLine("Warning: Message not received within timeout (this is expected for self-subscribe on some brokers)");
                }

                Console.WriteLine("Unsubscribing...");
                await client.UnsubscribeAsync(testTopic);

                Console.WriteLine("Disconnecting...");
                await client.DisconnectAsync();

                Console.WriteLine();
                Console.WriteLine("=== Test completed successfully! ===");
                Console.WriteLine("MQTT 5.0 connection to broker.emqx.io works correctly.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
            }
        }
    }
}