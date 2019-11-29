/****************************************************************************
*项目名称：SAEA.DNS
*CLR 版本：3.0
*机器名称：WENLI-PC
*命名空间：SAEA.DNS
*类 名 称：DnsClient
*版 本 号：v5.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/11/28 22:43:28
*描述：
*=====================================================================
*修改时间：2019/11/28 22:43:28
*修 改 人： yswenli
*版 本 号： v5.0.0.1
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using SAEA.DNS.Coder;
using SAEA.DNS.Model;
using SAEA.DNS.Protocol;
using SAEA.DNS.Common.ResourceRecords;

namespace SAEA.DNS
{
    /// <summary>
    /// DnsClient
    /// </summary>
    public class DnsClient
    {
        private const int DEFAULT_PORT = 53;

        private static readonly Random RANDOM = new Random();

        private IRequestCoder resolver;

        public DnsClient(IPEndPoint dns) :
            this(new UdpRequestCoder(dns, new TcpRequestCoder(dns)))
        { }

        public DnsClient(IPAddress ip, int port = DEFAULT_PORT) :
            this(new IPEndPoint(ip, port))
        { }

        public DnsClient(string ip, int port = DEFAULT_PORT) :
            this(IPAddress.Parse(ip), port)
        { }

        public DnsClient(IRequestCoder resolver)
        {
            this.resolver = resolver;
        }

        public DnsRequest FromArray(byte[] message)
        {
            Request request = Request.FromArray(message);
            return new DnsRequest(resolver, request);
        }

        public DnsRequest Create(IRequest request = null)
        {
            return new DnsRequest(resolver, request);
        }

        public async Task<IList<IPAddress>> Lookup(string domain, RecordType type = RecordType.A, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (type != RecordType.A && type != RecordType.AAAA)
            {
                throw new ArgumentException("Invalid record type " + type);
            }

            IResponse response = await Resolve(domain, type, cancellationToken);
            IList<IPAddress> ips = response.AnswerRecords
                .Where(r => r.Type == type)
                .Cast<IPAddressResourceRecord>()
                .Select(r => r.IPAddress)
                .ToList();

            if (ips.Count == 0)
            {
                throw new ResponseException(response, "No matching records");
            }

            return ips;
        }

        public Task<string> Reverse(string ip, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Reverse(IPAddress.Parse(ip), cancellationToken);
        }

        public async Task<string> Reverse(IPAddress ip, CancellationToken cancellationToken = default(CancellationToken))
        {
            IResponse response = await Resolve(Domain.PointerName(ip), RecordType.PTR, cancellationToken);
            IResourceRecord ptr = response.AnswerRecords.FirstOrDefault(r => r.Type == RecordType.PTR);

            if (ptr == null)
            {
                throw new ResponseException(response, "No matching records");
            }

            return ((PointerResourceRecord)ptr).PointerDomainName.ToString();
        }

        public Task<IResponse> Resolve(string domain, RecordType type, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Resolve(new Domain(domain), type, cancellationToken);
        }

        public Task<IResponse> Resolve(Domain domain, RecordType type, CancellationToken cancellationToken = default(CancellationToken))
        {
            DnsRequest request = Create();
            Question question = new Question(domain, type);

            request.Questions.Add(question);
            request.OperationCode = OperationCode.Query;
            request.RecursionDesired = true;

            return request.Resolve(cancellationToken);
        }
    }
}
