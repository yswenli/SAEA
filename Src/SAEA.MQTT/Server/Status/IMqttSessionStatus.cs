using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAEA.MQTT.Server.Status
{
    public interface IMqttSessionStatus
    {
        string ClientId { get; }

        long PendingApplicationMessagesCount { get; }

        IDictionary<object, object> Items { get; }

        Task ClearPendingApplicationMessagesAsync();

        Task DeleteAsync();
    }
}