using SAEA.Common;
using SAEA.DNS;
using SAEA.DNS.Model;

using System;
using System.Threading;

namespace SAEA.DNSTest
{
    class Server
    {
        static DnsServer _server;

        public static void Init(int port = 53)
        {
            DnsRecords dnsRecords = new DnsRecords();

            _server = new DnsServer(dnsRecords);

            //添加映射记录
            dnsRecords.AddIPAddressResourceRecord("yswenli.net", "127.0.0.1");

            _server.OnRequested += (sender, e) =>
            {
                ConsoleHelper.WriteLine("[DNSServer Requested]: {0}", ConsoleColor.Blue, e.Request);
            };

            _server.OnResponded += (sender, e) =>
            {
                ConsoleHelper.WriteLine("[DNSServer Responded]: {0} => {1}", ConsoleColor.Green, e.Request, e.Response);
            };

            _server.OnErrored += (sender, e) =>
            {
                ConsoleHelper.WriteLine("[DNSServer Errored]: {0}", ConsoleColor.Red, e.Exception.Message);
            };

            var result = false;

            _server.OnListening += async (sender, e) =>
            {
                ConsoleHelper.WriteLine("[DNSServer Listening]", ConsoleColor.Green);

                result = true;

                //await new DnsClient("127.0.0.1").Lookup("yswenli.net");
            };

            //启动dns服务
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
