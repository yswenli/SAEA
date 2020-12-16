/****************************************************************************
*项目名称：SAEA.MQTT.Core.Implementations
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Core.Implementations
*类 名 称：ClientRetainedMessageHandler
*版 本 号： v5.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 17:04:51
*描述：
*=====================================================================
*修改时间：2019/1/15 17:04:51
*修 改 人： yswenli
*版 本 号： v5.0.0.1
*描    述：
*****************************************************************************/
using SAEA.Common.Serialization;
using SAEA.MQTT.Interface;
using SAEA.MQTT.Model;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SAEA.MQTT.Core.Implementations
{
    public class ClientRetainedMessageHandler : IMqttManagedClientStorage
    {
        private const string Filename = @"RetainedMessages.json";

        public Task SaveQueuedMessagesAsync(IList<MqttManagedMessage> messages)
        {
            File.WriteAllText(Filename, SerializeHelper.Serialize(messages));
            return Task.FromResult(0);
        }

        public Task<IList<MqttManagedMessage>> LoadQueuedMessagesAsync()
        {
            IList<MqttManagedMessage> retainedMessages;
            if (File.Exists(Filename))
            {
                var json = File.ReadAllText(Filename);
                retainedMessages = SerializeHelper.Deserialize<List<MqttManagedMessage>>(json);
            }
            else
            {
                retainedMessages = new List<MqttManagedMessage>();
            }

            return Task.FromResult(retainedMessages);
        }
    }
}
