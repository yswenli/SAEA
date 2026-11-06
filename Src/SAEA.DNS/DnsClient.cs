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
*唯一标识：7f0cb00a-6ba0-4108-9ddf-a74bceef284c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：DnsClient接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:47
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
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
    /// DNS 客户端，向指定服务器发起查询
    /// </summary>
    public class DnsClient
    {
        private const int DEFAULT_PORT = 53;

        private IRequestCoder _coder;

        /// <summary>
        /// 用服务器地址创建客户端，UDP 响应被截断时自动改用 TCP 重试
        /// </summary>
        /// <param name="dns">DNS 服务器地址</param>
        public DnsClient(IPEndPoint dns) :
            this(new UdpRequestCoder(dns, new TcpRequestCoder(dns)))
        { }

        /// <summary>
        /// 用服务器 IP 与端口创建客户端
        /// </summary>
        /// <param name="ip">DNS 服务器 IP</param>
        /// <param name="port">DNS 服务器端口，默认 53</param>
        public DnsClient(IPAddress ip, int port = DEFAULT_PORT) :
            this(new IPEndPoint(ip, port))
        { }

        /// <summary>
        /// 用服务器地址字符串与端口创建客户端
        /// </summary>
        /// <param name="ip">DNS 服务器 IP 字符串，默认 119.29.29.29</param>
        /// <param name="port">DNS 服务器端口，默认 53</param>
        public DnsClient(string ip = "119.29.29.29", int port = DEFAULT_PORT) :
            this(IPAddress.Parse(ip), port)
        { }

        /// <summary>
        /// 用自定义编解码器创建客户端
        /// </summary>
        /// <param name="coder">请求编解码器</param>
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
