using JT808.Protocol;

using SAEA.Sockets.Core;
using SAEA.Sockets.Shortcut;

using System;
using System.Collections.Generic;
using System.Linq;

namespace SAEA.Sockets.TcpTest
{
    public class JClient2
    {
        JUnpacker _jUnpacker;

        TCPClient<JUnpacker> _client;

        List<JT808Package> _data;

        public JClient2()
        {
            _jUnpacker = new JUnpacker();

            _client = new TCPClient<JUnpacker>("127.0.0.1", 39808);

            _client.OnError += _client_OnError;

            _client.OnDisconnect += _client_OnDisconnect;

            _data = new List<JT808Package>();
        }

        private void _client_OnDisconnect(TCPClient<JUnpacker> arg1, Exception arg2)
        {
            Console.WriteLine($"ClientSokcet_OnDisconnected:{arg1}");
        }

        private void _client_OnError(TCPClient<JUnpacker> arg1, Exception arg2)
        {
            Console.WriteLine($"ClientSokcet_OnError:{arg2.Message}");
        }

        public void Connect()
        {
            _client.Connect();
        }

        public void Disconnect()
        {
            _client.Disconnect();
        }


        public void Send(JT808Package jT808Package)
        {
            var data = new JT808Serializer().Serialize(jT808Package);
            _client.SocketStream.Write(data, 0, data.Length);
        }


        public JT808Package Receive()
        {
            do
            {
                if (_data.Count > 0)
                {
                    var result = _data.First();
                    _data.Remove(result);
                    return result;
                }
                var buffer = new byte[1024];
                var rlen = _client.SocketStream.Read(buffer, 0, 1024);
                if (rlen > 0)
                {
                    var data = buffer.AsSpan().Slice(0, rlen).ToArray();
                    var b = _jUnpacker.Decode(data);
                    if (b == null)
                    {
                        continue;
                    }
                    var package = new JT808Serializer().Deserialize<JT808Package>(b.AsSpan());
                    _data.Add(package);
                }
                else
                {
                    return null;
                }
            }
            while (true);
        }

    }
}
