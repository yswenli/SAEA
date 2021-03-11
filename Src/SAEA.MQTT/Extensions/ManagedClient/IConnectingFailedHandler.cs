using System.Threading.Tasks;

namespace SAEA.MQTT.Extensions.ManagedClient
{
    public interface IConnectingFailedHandler
    {
        Task HandleConnectingFailedAsync(ManagedProcessFailedEventArgs eventArgs);
    }
}
