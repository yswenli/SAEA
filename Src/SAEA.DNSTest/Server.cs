using SAEA.DNS;
using SAEA.DNS.Model;
using System;
using System.Threading.Tasks;

namespace SAEA.DNSTest
{
    class Server
    {
        public async static Task InitAsync(int port = 53)
        {
            DnsDataFile dnsDataFile = new DnsDataFile();

            DnsServer server = new DnsServer(dnsDataFile, "8.8.8.8");

            dnsDataFile.AddIPAddressResourceRecord("google.com", "127.0.0.1");

            server.Requested += (sender, e) => Console.WriteLine("Requested: {0}", e.Request);

            server.Responded += (sender, e) => Console.WriteLine("Responded: {0} => {1}", e.Request, e.Response);

            server.Errored += (sender, e) => Console.WriteLine("Errored: {0}", e.Exception.Message);

            server.Listening += (sender, e) => Console.WriteLine("Listening");

            server.Listening += async (sender, e) =>
            {
                DnsClient client = new DnsClient("127.0.0.1", port);

                await client.Lookup("google.com");

                await client.Lookup("cnn.com");

                server.Dispose();
            };

            await server.Listen(port);
        }
    }
}
