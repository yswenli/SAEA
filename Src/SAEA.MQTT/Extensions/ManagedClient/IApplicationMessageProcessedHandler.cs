using System.Threading.Tasks;

namespace SAEA.MQTT.Extensions.ManagedClient
{
    public interface IApplicationMessageProcessedHandler
    {
        Task HandleApplicationMessageProcessedAsync(ApplicationMessageProcessedEventArgs eventArgs);
    }
}
