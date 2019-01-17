/****************************************************************************
*项目名称：SAEA.MQTT.Core.Implementations
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Core.Implementations
*类 名 称：ClientRetainedMessageHandler
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 17:04:51
*描述：
*=====================================================================
*修改时间：2019/1/15 17:04:51
*修 改 人： yswenli
*版 本 号： V3.6.2.2
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.MQTT.Interface;
using SAEA.MQTT.Model;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SAEA.MQTT.Core.Implementations
{
    public class ClientRetainedMessageHandler : IManagedMqttClientStorage
    {
        private const string Filename = @"RetainedMessages.json";

        public Task SaveQueuedMessagesAsync(IList<ManagedMqttApplicationMessage> messages)
        {
            File.WriteAllText(Filename, SerializeHelper.Serialize(messages));
            return Task.FromResult(0);
        }

        public Task<IList<ManagedMqttApplicationMessage>> LoadQueuedMessagesAsync()
        {
            IList<ManagedMqttApplicationMessage> retainedMessages;
            if (File.Exists(Filename))
            {
                var json = File.ReadAllText(Filename);
                retainedMessages = SerializeHelper.Deserialize<List<ManagedMqttApplicationMessage>>(json);
            }
            else
            {
                retainedMessages = new List<ManagedMqttApplicationMessage>();
            }

            return Task.FromResult(retainedMessages);
        }
    }
}
