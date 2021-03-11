using System;
using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    public interface IMqttServerStartedHandler
    {
        Task HandleServerStartedAsync(EventArgs eventArgs);
    }
}
