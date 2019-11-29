/****************************************************************************
*项目名称：SAEA.DNS
*CLR 版本：3.0
*机器名称：WENLI-PC
*命名空间：SAEA.DNS.Model
*类 名 称：DnsClientRequest
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
using SAEA.DNS.Protocol;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.DNS.Model
{
    /// <summary>
    /// 请求
    /// </summary>
    public class DnsClientRequest : DnsRequestMessage, IRequest
    {
        private const int DEFAULT_PORT = 53;

        private IRequestCoder _coder;

        private IRequest _request;

        /// <summary>
        /// 请求
        /// </summary>
        public DnsClientRequest() : base()
        {

        }

        /// <summary>
        /// 请求
        /// </summary>
        /// <param name="dns"></param>
        /// <param name="request"></param>
        public DnsClientRequest(IPEndPoint dns, IRequest request = null) :
            this(new UdpRequestCoder(dns), request)
        { }

        /// <summary>
        /// 请求
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="request"></param>
        public DnsClientRequest(IPAddress ip, int port = DEFAULT_PORT, IRequest request = null) :
            this(new IPEndPoint(ip, port), request)
        { }

        /// <summary>
        /// 请求
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="request"></param>
        public DnsClientRequest(string ip, int port = DEFAULT_PORT, IRequest request = null) :
            this(IPAddress.Parse(ip), port, request)
        { }

        /// <summary>
        /// 请求
        /// </summary>
        /// <param name="coder"></param>
        /// <param name="request"></param>
        public DnsClientRequest(IRequestCoder coder, IRequest request = null)
        {
            this._coder = coder;
            this._request = request == null ? new DnsRequestMessage() : new DnsRequestMessage(request);
        }        

        /// <summary>
        /// 使用提供的DNS信息将此请求解析为响应,给定的请求策略用于检索响应。
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<IResponse> Query(CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                IResponse response = await _coder.Code(this, cancellationToken);

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
