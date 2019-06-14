using System;
using SAEA.WebSocket.Model;

namespace SAEA.WebSocket.Model
{
    public interface IWSServer
    {
        event Action<string, WSProtocal> OnMessage;

        void Start(int backlog = 10 * 1000);
       

        void Reply(string id, WSProtocal data);


        void Disconnect(string id, WSProtocal data);


        void Stop();
    }
}