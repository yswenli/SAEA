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
            DnsDataFile dnsDataFile = new DnsDataFile();

            _server = new DnsServer(dnsDataFile);

            dnsDataFile.AddIPAddressResourceRecord("baidu.com", "127.0.0.1");

            _server.Requested += (sender, e) => Console.WriteLine("Requested: {0}", e.Request);

            _server.Responded += (sender, e) => Console.WriteLine("Responded: {0} => {1}", e.Request, e.Response);

            _server.Errored += (sender, e) => Console.WriteLine("Errored: {0}", e.Exception.Message);

            _server.Listening += (sender, e) => Console.WriteLine("Listening");

            _server.Listening += async (sender, e) =>
            {

                DnsClient client = new DnsClient("127.0.0.1", port);

                await client.Lookup("baidu.com");
            };

            await _server.Listen(port);
        }

        public static void Init(int port = 53)
        {
            var result = false;

            DnsDataFile dnsDataFile = new DnsDataFile();

            _server = new DnsServer(dnsDataFile);

            dnsDataFile.AddIPAddressResourceRecord("baidu.com", "127.0.0.1");

            _server.Requested += (sender, e) => Console.WriteLine("Requested: {0}", e.Request);

            _server.Responded += (sender, e) => Console.WriteLine("Responded: {0} => {1}", e.Request, e.Response);

            _server.Errored += (sender, e) => Console.WriteLine("Errored: {0}", e.Exception.Message);

            _server.Listening += (sender, e) => Console.WriteLine("Listening");

            _server.Listening += async (sender, e) =>
            {
                result = true;

                //DnsClient client = new DnsClient();

                //await client.Lookup("baidu.com");
            };

            _server.Listen(port);

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
