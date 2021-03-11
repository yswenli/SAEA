using SAEA.MQTT.Diagnostics;
using System.Threading.Tasks;

namespace SAEA.MQTT.Internal
{
    public static class TaskExtensions
    {
        public static void Forget(this Task task, IMqttNetScopedLogger logger)
        {
            task?.ContinueWith(t =>
                {
                    logger.Error(t.Exception, "Unhandled exception.");
                },
                TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
