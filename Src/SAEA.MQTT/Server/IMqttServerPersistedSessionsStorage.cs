using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    public interface IMqttServerPersistedSessionsStorage
    {
        Task<IList<IMqttServerPersistedSession>> LoadPersistedSessionsAsync();
    }
}
