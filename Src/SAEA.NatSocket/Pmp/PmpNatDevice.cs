/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.NatSocket
*文件名： Class1
*版本号： V3.2.1.1
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
*版本号： V3.2.1.1
*描述：
*
*****************************************************************************/
using SAEA.NatSocket.Base;
using SAEA.NatSocket.Enums;
using SAEA.NatSocket.Exceptions;
using SAEA.NatSocket.Utils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.NatSocket.Pmp
{
    internal sealed class PmpNatDevice : NatDevice
    {
        private readonly IPAddress _publicAddress;

        internal PmpNatDevice(IPAddress localAddress, IPAddress publicAddress)
        {
            LocalAddress = localAddress;
            _publicAddress = publicAddress;
        }

        internal IPAddress LocalAddress { get; private set; }

        public override async Task CreatePortMapAsync(Mapping mapping)
        {
            await InternalCreatePortMapAsync(mapping, true)
                .TimeoutAfter(TimeSpan.FromSeconds(4));
            RegisterMapping(mapping);
        }

        public override async Task DeletePortMapAsync(Mapping mapping)
        {
            await InternalCreatePortMapAsync(mapping, false)
                .TimeoutAfter(TimeSpan.FromSeconds(4));
            UnregisterMapping(mapping);
        }


        public override Task<IEnumerable<Mapping>> GetAllMappingsAsync()
        {
            throw new NotSupportedException();
        }

        public override Task<IPAddress> GetExternalIPAsync()
        {

            return Task.Run(() => _publicAddress)

                .TimeoutAfter(TimeSpan.FromSeconds(4));
        }

        public override Task<Mapping> GetSpecificMappingAsync(Protocol protocol, int port)
        {
            throw new NotSupportedException("NAT-PMP does not specify a way to get a specific port map");
        }


        private async Task<Mapping> InternalCreatePortMapAsync(Mapping mapping, bool create)
        {
            var package = new List<byte>();

            package.Add(PmpConstants.Version);
            package.Add(mapping.Protocol == Protocol.Tcp ? PmpConstants.OperationCodeTcp : PmpConstants.OperationCodeUdp);
            package.Add(0); //reserved
            package.Add(0); //reserved
            package.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)mapping.PrivatePort)));
            package.AddRange(
                BitConverter.GetBytes(create ? IPAddress.HostToNetworkOrder((short)mapping.PublicPort) : (short)0));
            package.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(mapping.Lifetime)));

            try
            {
                byte[] buffer = package.ToArray();
                int attempt = 0;
                int delay = PmpConstants.RetryDelay;

                using (var udpClient = new UdpClient())
                {
                    CreatePortMapListen(udpClient, mapping);

                    while (attempt < PmpConstants.RetryAttempts)
                    {
                        await
                            udpClient.SendAsync(buffer, buffer.Length,
                                                new IPEndPoint(LocalAddress, PmpConstants.ServerPort));

                        attempt++;
                        delay *= 2;
                        Thread.Sleep(delay);
                    }
                }
            }
            catch (Exception e)
            {
                string type = create ? "create" : "delete";
                string message = String.Format("Failed to {0} portmap (protocol={1}, private port={2})",
                                               type,
                                               mapping.Protocol,
                                               mapping.PrivatePort);
                NatDiscoverer.TraceSource.LogError(message);
                var pmpException = e as MappingException;
                throw new MappingException(message, pmpException);
            }

            return mapping;
        }


        private void CreatePortMapListen(UdpClient udpClient, Mapping mapping)
        {
            var endPoint = new IPEndPoint(LocalAddress, PmpConstants.ServerPort);

            while (true)
            {
                byte[] data = udpClient.Receive(ref endPoint);

                if (data.Length < 16)
                    continue;

                if (data[0] != PmpConstants.Version)
                    continue;

                var opCode = (byte)(data[1] & 127);

                var protocol = Protocol.Tcp;
                if (opCode == PmpConstants.OperationCodeUdp)
                    protocol = Protocol.Udp;

                short resultCode = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(data, 2));
                int epoch = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(data, 4));

                short privatePort = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(data, 8));
                short publicPort = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(data, 10));

                var lifetime = (uint)IPAddress.NetworkToHostOrder(BitConverter.ToInt32(data, 12));

                if (privatePort < 0 || publicPort < 0 || resultCode != PmpConstants.ResultCodeSuccess)
                {
                    var errors = new[]
                                     {
                                         "Success",
                                         "Unsupported Version",
                                         "Not Authorized/Refused (e.g. box supports mapping, but user has turned feature off)"
                                         ,
                                         "Network Failure (e.g. NAT box itself has not obtained a DHCP lease)",
                                         "Out of resources (NAT box cannot create any more mappings at this time)",
                                         "Unsupported opcode"
                                     };
                    throw new MappingException(resultCode, errors[resultCode]);
                }

                if (lifetime == 0) return; //mapping was deleted

                //mapping was created
                //TODO: verify that the private port+protocol are a match
                mapping.PublicPort = publicPort;
                mapping.Protocol = protocol;
                mapping.Expiration = DateTime.Now.AddSeconds(lifetime);
                return;
            }
        }


        public override string ToString()
        {
            return String.Format("Local Address: {0}\nPublic IP: {1}\nLast Seen: {2}",
                                 LocalAddress, _publicAddress, LastSeen);
        }
    }
}
