/****************************************************************************
 * 
   ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.DNS
*文件名： DnsClient
*版本号： v26.4.23.1
*唯一标识：a0d56bbd-3314-449c-8be9-837582577510
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：
*
*=====================================================================
*修改标记
*修改时间：2019/11/29 20:45:22
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
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

        private IRequestCoder _coder;

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
        /// <param name="coder"></param>
        public DnsClient(IRequestCoder coder)
        {
            this._coder = coder;
        }

        /// <summary>
        /// 通过数据转换
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public DnsClientRequest FromArray(byte[] message)
        {
            DnsRequestMessage request = DnsRequestMessage.FromArray(message);
            return new DnsClientRequest(_coder, request);
        }

        /// <summary>
        /// 创建一个DnsRequest
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public DnsClientRequest Create(IRequest request = null)
        {
            return new DnsClientRequest(_coder, request);
        }

        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="type"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<IList<IPAddress>> LookupAsync(string domain, RecordType type = RecordType.A, CancellationToken cancellationToken = default(CancellationToken))
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
            DnsClientRequest request = Create();

            Question question = new Question(domain, type);

            request.Questions.Add(question);
            request.OperationCode = OperationCode.Query;
            request.RecursionDesired = true;

            return request.Query(cancellationToken);
        }
    }
}
