using SAEA.DNS;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace SAEA.DNSTest
{
    class Client
    {
        public async static Task LookupAsync(string dnsServer = "119.29.29.29", params string[] args)
        {
            DnsClient client = new DnsClient(dnsServer, 53);

            foreach (string domain in args)
            {
                IList<IPAddress> ips = await client.LookupAsync(domain);

                Console.WriteLine("[DNSClient] {0} => {1}", domain, string.Join(", ", ips));
            }
        }
    }
}
