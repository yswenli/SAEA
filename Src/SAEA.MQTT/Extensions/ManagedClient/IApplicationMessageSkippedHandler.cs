using System.Threading.Tasks;

namespace SAEA.MQTT.Extensions.ManagedClient
{
    public interface IApplicationMessageSkippedHandler
    {
        Task HandleApplicationMessageSkippedAsync(ApplicationMessageSkippedEventArgs eventArgs);
    }
}
