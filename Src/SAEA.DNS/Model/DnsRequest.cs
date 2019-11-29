/****************************************************************************
*项目名称：SAEA.DNS
*CLR 版本：3.0
*机器名称：WENLI-PC
*命名空间：SAEA.DNS.Model
*类 名 称：DnsRequest
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
using SAEA.DNS.Coder;
using SAEA.DNS.Common.ResourceRecords;
using SAEA.DNS.Protocol;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.DNS.Model
{
    /// <summary>
    /// 请求
    /// </summary>
    public class DnsRequest : IRequest
    {
        private const int DEFAULT_PORT = 53;

        private IRequestCoder resolver;

        private IRequest request;

        public DnsRequest(IPEndPoint dns, IRequest request = null) :
            this(new UdpRequestCoder(dns), request)
        { }

        public DnsRequest(IPAddress ip, int port = DEFAULT_PORT, IRequest request = null) :
            this(new IPEndPoint(ip, port), request)
        { }

        public DnsRequest(string ip, int port = DEFAULT_PORT, IRequest request = null) :
            this(IPAddress.Parse(ip), port, request)
        { }

        public DnsRequest(IRequestCoder resolver, IRequest request = null)
        {
            this.resolver = resolver;
            this.request = request == null ? new Request() : new Request(request);
        }

        public int Id
        {
            get { return request.Id; }
            set { request.Id = value; }
        }

        public IList<IResourceRecord> AdditionalRecords
        {
            get { return new ReadOnlyCollection<IResourceRecord>(request.AdditionalRecords); }
        }

        public OperationCode OperationCode
        {
            get { return request.OperationCode; }
            set { request.OperationCode = value; }
        }

        public bool RecursionDesired
        {
            get { return request.RecursionDesired; }
            set { request.RecursionDesired = value; }
        }

        public IList<Question> Questions
        {
            get { return request.Questions; }
        }

        public int Size
        {
            get { return request.Size; }
        }

        public byte[] ToArray()
        {
            return request.ToArray();
        }

        public override string ToString()
        {
            return request.ToString();
        }

        /// <summary>
        /// 使用提供的DNS信息将此请求解析为响应。给定的请求策略用于检索响应。
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<IResponse> Resolve(CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                IResponse response = await resolver.Resolve(this, cancellationToken);

                if (response.Id != this.Id)
                {
                    throw new ResponseException(response, "请求/响应ID不匹配");
                }
                if (response.ResponseCode != ResponseCode.NoError)
                {
                    throw new ResponseException(response);
                }

                return response;
            }
            catch (ArgumentException e)
            {
                throw new ResponseException("无效响应", e);
            }
            catch (IndexOutOfRangeException e)
            {
                throw new ResponseException("无效响应", e);
            }
        }
    }
}
