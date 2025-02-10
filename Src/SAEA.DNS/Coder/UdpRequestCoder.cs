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
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.DNS.Protocol;
using SAEA.Sockets;
using SAEA.Sockets.Base;
using SAEA.Sockets.Model;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.DNS.Coder
{
    /// <summary>
    /// udp模式处理编解码
    /// </summary>
    public class UdpRequestCoder : IRequestCoder
    {
        private int _timeout;
        private IRequestCoder _fallback;
        private IPEndPoint _ipEndpoint;
        OrderSyncHelper<byte[]> _orderSyncHelper;

        /// <summary>
        /// udp模式处理编解码
        /// </summary>
        /// <param name="dns"></param>
        /// <param name="fallback"></param>
        /// <param name="timeout"></param>
        public UdpRequestCoder(IPEndPoint dns, IRequestCoder fallback, int timeout = 5000)
        {
            _ipEndpoint = dns;
            _fallback = fallback;
            _timeout = timeout;
            _orderSyncHelper = new OrderSyncHelper<byte[]>(_timeout);
        }

        /// <summary>
        /// udp模式处理编解码
        /// </summary>
        /// <param name="dns"></param>
        /// <param name="timeout"></param>
        public UdpRequestCoder(IPEndPoint dns, int timeout = 5000) : this(dns, new NullRequestCoder(), timeout)
        {

        }

        /// <summary>
        /// 编码处理
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<IResponse> Code(IRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var udpClient = SocketFactory.CreateClientSocket(SocketOptionBuilder.Instance.SetSocket(SAEASocketType.Udp)
                 .SetIPEndPoint(_ipEndpoint)
                 .UseIocp<BaseCoder>()
                 .SetReadBufferSize(SocketOption.UDPMaxLength)
                 .SetWriteBufferSize(SocketOption.UDPMaxLength)
                 .ReusePort()
                 .Build()))
            {
                udpClient.OnReceive += UdpClient_OnReceive;

                udpClient.Connect();

                var buffer = _orderSyncHelper.Wait(() =>
                {
                    udpClient.SendAsync(request.ToArray());
                });

                DnsResponseMessage response = DnsResponseMessage.FromArray(buffer);

                if (response.Truncated)
                {
                    return await _fallback.Code(request, cancellationToken);
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

