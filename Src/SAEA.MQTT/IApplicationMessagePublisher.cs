using System.Threading;
using System.Threading.Tasks;
using SAEA.MQTT.Client.Publishing;

namespace SAEA.MQTT
{
    public interface IApplicationMessagePublisher
    {
        Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken);
    }
}
