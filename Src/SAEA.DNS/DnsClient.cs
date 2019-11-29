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

        /// <summary>
        /// DnsClient
        /// </summary>
        /// <param name="dns"></param>
        public DnsClient(IPEndPoint dns) :
            this(new UdpRequestCoder(dns, new TcpRequestCoder(dns)))
        { }

        /// <summary>
        /// DnsClient
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        public DnsClient(IPAddress ip, int port = DEFAULT_PORT) :
            this(new IPEndPoint(ip, port))
        { }

        /// <summary>
        /// DnsClient
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        public DnsClient(string ip = "119.29.29.29", int port = DEFAULT_PORT) :
            this(IPAddress.Parse(ip), port)
        { }

        /// <summary>
        /// DnsClient
        /// </summary>
        /// <param name="resolver"></param>
        public DnsClient(IRequestCoder resolver)
        {
            this.resolver = resolver;
        }

        /// <summary>
        /// 通过数据转换
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public DnsRequest FromArray(byte[] message)
        {
            Protocol.DnsRequestMessage request = Protocol.DnsRequestMessage.FromArray(message);
            return new Model.DnsRequest(resolver, request);
        }

        /// <summary>
        /// 创建一个DnsRequest
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public DnsRequest Create(IRequest request = null)
        {
            return new Model.DnsRequest(resolver, request);
        }

        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="type"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<IList<IPAddress>> Lookup(string domain, RecordType type = RecordType.A, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (type != RecordType.A && type != RecordType.AAAA)
            {
                throw new ArgumentException("Invalid record type " + type);
            }

            IResponse response = await Query(domain, type, cancellationToken);
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

        /// <summary>
        /// 反转查询
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<string> Reverse(string ip, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Reverse(IPAddress.Parse(ip), cancellationToken);
        }

        /// <summary>
        /// 反转查询
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<string> Reverse(IPAddress ip, CancellationToken cancellationToken = default(CancellationToken))
        {
            IResponse response = await Query(Domain.PointerName(ip), RecordType.PTR, cancellationToken);
            IResourceRecord ptr = response.AnswerRecords.FirstOrDefault(r => r.Type == RecordType.PTR);

            if (ptr == null)
            {
                throw new ResponseException(response, "No matching records");
            }

            return ((PointerResourceRecord)ptr).PointerDomainName.ToString();
        }

        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="type"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<IResponse> Query(string domain, RecordType type, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Query(new Domain(domain), type, cancellationToken);
        }

        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="type"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<IResponse> Query(Domain domain, RecordType type, CancellationToken cancellationToken = default(CancellationToken))
        {
            Model.DnsRequest request = Create();

            Question question = new Question(domain, type);

            request.Questions.Add(question);
            request.OperationCode = OperationCode.Query;
            request.RecursionDesired = true;

            return request.Query(cancellationToken);
        }
    }
}
