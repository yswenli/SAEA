using SAEA.Audio.Net;
using System.Net;

namespace SAEA.Audio.GAudio
{
    public class GAudioServer
    {
        SocketServer _audioServer;

        public GAudioServer(IPEndPoint endPoint)
        {
            _audioServer = new SocketServer(endPoint);
        }

        public GAudioServer(int port) : this(new IPEndPoint(IPAddress.Any, port))
        {

        }

        public void Start()
        {
            _audioServer.Start();
        }

        public void Stop()
        {
            _audioServer.Stop();
        }
    }
}
