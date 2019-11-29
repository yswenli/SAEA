using SAEA.DNS;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace SAEA.DNSTest
{
    class Client
    {
        public async static Task LookupAsync(params string[] args)
        {
            DnsClient client = new DnsClient("127.0.0.1", 53);

            foreach (string domain in args)
            {
                IList<IPAddress> ips = await client.Lookup(domain);

                Console.WriteLine("{0} => {1}", domain, string.Join(", ", ips));
            }
        }
    }
}
