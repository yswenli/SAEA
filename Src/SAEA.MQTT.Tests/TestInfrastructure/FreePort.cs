using System.Net;
using System.Net.Sockets;

namespace SAEA.MQTT.Tests.TestInfrastructure
{
    internal static class FreePort
    {
        public static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}