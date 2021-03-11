using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAEA.MQTT.Extensions.ManagedClient
{
    public interface IManagedMqttClientStorage
    {
        Task SaveQueuedMessagesAsync(IList<ManagedMqttApplicationMessage> messages);

        Task<IList<ManagedMqttApplicationMessage>> LoadQueuedMessagesAsync();
    }
}
