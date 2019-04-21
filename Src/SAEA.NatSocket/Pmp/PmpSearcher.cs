/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.NatSocket
*文件名： Class1
*版本号： v4.5.1.2
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： v4.5.1.2
*描述：
*
*****************************************************************************/
using SAEA.NatSocket.Base;
using SAEA.NatSocket.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SAEA.NatSocket.Pmp
{
    internal class PmpSearcher : Searcher
    {
        private readonly IIPAddressesProvider _ipprovider;
        private Dictionary<UdpClient, IEnumerable<IPEndPoint>> _gatewayLists;
        private int _timeout;

        internal PmpSearcher(IIPAddressesProvider ipprovider)
        {
            _ipprovider = ipprovider;
            _timeout = 250;
            CreateSocketsAndAddGateways();
        }

        private void CreateSocketsAndAddGateways()
        {
            Sockets = new List<UdpClient>();
            _gatewayLists = new Dictionary<UdpClient, IEnumerable<IPEndPoint>>();

            try
            {
                List<IPEndPoint> gatewayList = _ipprovider.GatewayAddresses()
                    .Select(ip => new IPEndPoint(ip, PmpConstants.ServerPort))
                    .ToList();

                if (!gatewayList.Any())
                {
                    gatewayList.AddRange(
                        _ipprovider.DnsAddresses()
                            .Select(ip => new IPEndPoint(ip, PmpConstants.ServerPort)));
                }

                if (!gatewayList.Any()) return;

                foreach (IPAddress address in _ipprovider.UnicastAddresses())
                {
                    UdpClient client;

                    try
                    {
                        client = new UdpClient(new IPEndPoint(address, 0));
                    }
                    catch (SocketException)
                    {
                        continue; // Move on to the next address.
                    }

                    _gatewayLists.Add(client, gatewayList);
                    Sockets.Add(client);
                }
            }
            catch (Exception e)
            {
                NatDiscoverer.TraceSource.LogError("There was a problem finding gateways: " + e);
                // NAT-PMP does not use multicast, so there isn't really a good fallback.
            }
        }

        protected override void Discover(UdpClient client, CancellationToken cancelationToken)
        {
            // Sort out the time for the next search first. The spec says the 
            // timeout should double after each attempt. Once it reaches 64 seconds
            // (and that attempt fails), assume no devices available
            NextSearch = DateTime.UtcNow.AddMilliseconds(_timeout);
            _timeout *= 2;

            if (_timeout >= 3000)
            {
                _timeout = 250;
                NextSearch = DateTime.UtcNow.AddSeconds(10);
                return;
            }

            // The nat-pmp search message. Must be sent to GatewayIP:53531
            var buffer = new[] { PmpConstants.Version, PmpConstants.OperationExternalAddressRequest };
            foreach (IPEndPoint gatewayEndpoint in _gatewayLists[client])
            {
                if (cancelationToken.IsCancellationRequested) return;

                client.Send(buffer, buffer.Length, gatewayEndpoint);
            }
        }

        private bool IsSearchAddress(IPAddress address)
        {
            return _gatewayLists.Values.SelectMany(x => x)
                .Any(x => x.Address.Equals(address));
        }

        public override NatDevice AnalyseReceivedResponse(IPAddress localAddress, byte[] response, IPEndPoint endpoint)
        {
            if (!IsSearchAddress(endpoint.Address)
                || response.Length != 12
                || response[0] != PmpConstants.Version
                || response[1] != PmpConstants.ServerNoop)
                return null;

            int errorcode = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(response, 2));
            if (errorcode != 0)
                NatDiscoverer.TraceSource.LogError("Non zero error: {0}", errorcode);

            var publicIp = new IPAddress(new[] { response[8], response[9], response[10], response[11] });
            //NextSearch = DateTime.Now.AddMinutes(5);

            _timeout = 250;
            return new PmpNatDevice(endpoint.Address, publicIp);
        }
    }
}
