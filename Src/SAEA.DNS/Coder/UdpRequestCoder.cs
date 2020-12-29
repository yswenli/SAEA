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
using SAEA.Common;
using SAEA.Common.Threading;
using SAEA.DNS.Protocol;
using SAEA.Sockets;
using SAEA.Sockets.Base;
using SAEA.Sockets.Model;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.DNS.Coder
{
    /// <summary>
    /// udp模式处理编解码
    /// </summary>
    public class UdpRequestCoder : IRequestCoder
    {
        private int timeout;
        private IRequestCoder fallback;
        private IPEndPoint dns;

        /// <summary>
        /// udp模式处理编解码
        /// </summary>
        /// <param name="dns"></param>
        /// <param name="fallback"></param>
        /// <param name="timeout"></param>
        public UdpRequestCoder(IPEndPoint dns, IRequestCoder fallback, int timeout = 5000)
        {
            this.dns = dns;
            this.fallback = fallback;
            this.timeout = timeout;
        }

        /// <summary>
        /// udp模式处理编解码
        /// </summary>
        /// <param name="dns"></param>
        /// <param name="timeout"></param>
        public UdpRequestCoder(IPEndPoint dns, int timeout = 5000)
        {
            this.dns = dns;
            this.fallback = new NullRequestCoder();
            this.timeout = timeout;
        }

        /// <summary>
        /// 编码处理
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<IResponse> Code2(IRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            using (UdpClient udp = new UdpClient())
            {
                await udp.SendAsync(request.ToArray(), request.Size, dns).WithCancellationTimeout(TimeSpan.FromMilliseconds(timeout), cancellationToken);

                UdpReceiveResult result = await udp.ReceiveAsync().WithCancellationTimeout(TimeSpan.FromMilliseconds(timeout), cancellationToken);

                if (!result.RemoteEndPoint.Equals(dns)) throw new IOException("Remote endpoint mismatch");

                byte[] buffer = result.Buffer;

                DnsResponseMessage response = DnsResponseMessage.FromArray(buffer);

                if (response.Truncated)
                {
                    return await fallback.Code(request, cancellationToken);
                }

                return new Model.DnsClientResponse(request, response, buffer);
            }
        }



        OrderSyncHelper<byte[]> _orderSyncHelper = new OrderSyncHelper<byte[]>(10 * 1000);


        /// <summary>
        /// 编码处理
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<IResponse> Code(IRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var udpClient = SocketFactory.CreateClientSocket(SocketOptionBuilder.Instance.SetSocket(SAEASocketType.Udp)
                 .SetIP(dns.Address.ToString())
                 .SetPort(dns.Port)
                 .UseIocp<BaseContext>()
                 .SetReadBufferSize(SocketOption.UDPMaxLength)
                 .SetWriteBufferSize(SocketOption.UDPMaxLength)
                 .ReusePort()
                 .Build()))
            {

                udpClient.OnReceive += UdpClient_OnReceive;

                var buffer = _orderSyncHelper.Wait(() =>
                {
                    udpClient.SendAsync(request.ToArray());
                });

                DnsResponseMessage response = DnsResponseMessage.FromArray(buffer);

                if (response.Truncated)
                {
                    return await fallback.Code(request, cancellationToken);
                }
                return new Model.DnsClientResponse(request, response, buffer);
            }
        }

        private void UdpClient_OnReceive(byte[] data)
        {
            _orderSyncHelper.Set(data);
        }
    }
}

