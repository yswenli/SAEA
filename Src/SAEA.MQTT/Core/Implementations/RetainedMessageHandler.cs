/****************************************************************************
*项目名称：SAEA.MQTT.Core.Implementations
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Core.Implementations
*类 名 称：RetainedMessageHandler
*版 本 号： v5.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 16:24:29
*描述：
*=====================================================================
*修改时间：2019/1/15 16:24:29
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
    public class RetainedMessageHandler : IMqttServerStorage
    {
        private string _filename = "C:\\SAEA.MQTT\\RetainedMessages.json";

        public RetainedMessageHandler() : this(Path.Combine(Directory.GetCurrentDirectory(), "SAEA.MQTT", "RetainedMessages.json"))
        {
        }

        public RetainedMessageHandler(string fileName)
        {
            _filename = fileName;
        }

        public Task SaveRetainedMessagesAsync(IList<MqttMessage> messages)
        {
            var directory = Path.GetDirectoryName(_filename);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_filename, SerializeHelper.Serialize(messages));
            return Task.FromResult(0);
        }

        public Task<IList<MqttMessage>> LoadRetainedMessagesAsync()
        {
            IList<MqttMessage> retainedMessages;
            if (File.Exists(_filename))
            {
                var json = File.ReadAllText(_filename);
                retainedMessages = SerializeHelper.Deserialize<List<MqttMessage>>(json);
            }
            else
            {
                retainedMessages = new List<MqttMessage>();
            }

            return Task.FromResult(retainedMessages);
        }
    }
}
