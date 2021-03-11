using System;
using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    public interface IMqttServerStoppedHandler
    {
        Task HandleServerStoppedAsync(EventArgs eventArgs);
    }
}
