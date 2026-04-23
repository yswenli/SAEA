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
*命名空间：SAEA.DNS.Coder
*文件名： UdpRequestCoder
*版本号： v26.4.23.1
*唯一标识：9bb2ded6-e600-4b33-b095-9c95e7b4a1a4
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：UdpRequestCoder编解码类
*
*=====================================================================
*修改标记
*修改时间：2019/11/29 20:45:22
*修改人： yswenli
*版本号： v26.4.23.1
*描述：UdpRequestCoder编解码类
*
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

