/****************************************************************************
*项目名称：SAEA.DNS
*CLR 版本：3.0
*机器名称：WENLI-PC
*命名空间：SAEA.DNS.Coder
*类 名 称：UdpRequestCoder
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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using SAEA.DNS.Protocol;
using SAEA.DNS.Common.Utils;
using SAEA.DNS.Model;

namespace SAEA.DNS.Coder
{
    public class UdpRequestCoder : IRequestCoder
    {
        private int timeout;
        private IRequestCoder fallback;
        private IPEndPoint dns;

        public UdpRequestCoder(IPEndPoint dns, IRequestCoder fallback, int timeout = 5000)
        {
            this.dns = dns;
            this.fallback = fallback;
            this.timeout = timeout;
        }

        public UdpRequestCoder(IPEndPoint dns, int timeout = 5000)
        {
            this.dns = dns;
            this.fallback = new NullRequestCoder();
            this.timeout = timeout;
        }

        public async Task<IResponse> Resolve(IRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            using (UdpClient udp = new UdpClient())
            {
                await udp
                    .SendAsync(request.ToArray(), request.Size, dns)
                    .WithCancellationTimeout(TimeSpan.FromMilliseconds(timeout), cancellationToken);

                UdpReceiveResult result = await udp
                    .ReceiveAsync()
                    .WithCancellationTimeout(TimeSpan.FromMilliseconds(timeout), cancellationToken);

                if (!result.RemoteEndPoint.Equals(dns)) throw new IOException("Remote endpoint mismatch");
                byte[] buffer = result.Buffer;
                Response response = Response.FromArray(buffer);

                if (response.Truncated)
                {
                    return await fallback.Resolve(request, cancellationToken);
                }

                return new DnsResponse(request, response, buffer);
            }
        }
    }
}
