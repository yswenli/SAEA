using SAEA.DNS;
using SAEA.DNS.Model;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.DNSTest
{
    class Server
    {
        static DnsServer _server;

        public async static Task InitAsync(int port = 53)
        {
            DnsRecords dnsDataFile = new DnsRecords();

            _server = new DnsServer(dnsDataFile);

            dnsDataFile.AddIPAddressResourceRecord("yswenli.net", "127.0.0.1");

            _server.OnRequested += (sender, e) =>
            {
                Console.WriteLine("[Requested]: {0}", e.Request);
            };

            _server.OnResponded += (sender, e) =>
            {
                Console.WriteLine("[Responded]: {0} => {1}", e.Request, e.Response);
            };

            _server.OnErrored += (sender, e) =>
            {
                Console.WriteLine("[Errored]: {0}", e.Exception.Message);
            };

            _server.OnListening += async (sender, e) =>
            {
                Console.WriteLine("[Listening]");

                await new DnsClient("127.0.0.1").Lookup("yswenli.net");
            };

            await _server.Start(port);
        }

        public static void Init(int port = 53)
        {
            var result = false;

            DnsRecords dnsDataFile = new DnsRecords();

            _server = new DnsServer(dnsDataFile);

            dnsDataFile.AddIPAddressResourceRecord("baidu.com", "127.0.0.1");

            _server.OnRequested += (sender, e) =>
            {
                Console.WriteLine("[Requested]: {0}", e.Request);                
            };

            _server.OnResponded += (sender, e) =>
            {
                Console.WriteLine("[Responded]: {0} => {1}", e.Request, e.Response);
            };

            _server.OnErrored += (sender, e) =>
            {
                Console.WriteLine("[Errored]: {0}", e.Exception.Message);
            };

            _server.OnListening += async (sender, e) =>
            {
                result = true;
                Console.WriteLine("[Listening]");
            };

            _server.Start(port);

            while (!result)
            {
                Thread.Sleep(100);
            }
        }


        public static void Dispose()
        {
            _server.Dispose();
        }

    }
}
