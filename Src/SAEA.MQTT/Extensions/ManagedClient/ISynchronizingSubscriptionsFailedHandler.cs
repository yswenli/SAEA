using System.Threading.Tasks;

namespace SAEA.MQTT.Extensions.ManagedClient
{
    public interface ISynchronizingSubscriptionsFailedHandler
    {
        Task HandleSynchronizingSubscriptionsFailedAsync(ManagedProcessFailedEventArgs eventArgs);
    }
}
