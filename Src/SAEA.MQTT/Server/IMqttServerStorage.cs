using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    public interface IMqttServerStorage
    {
        Task SaveRetainedMessagesAsync(IList<MqttApplicationMessage> messages);

        Task<IList<MqttApplicationMessage>> LoadRetainedMessagesAsync();
    }
}
