/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.NatSocket
*文件名： Class1
*版本号： V1.0.0.0
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
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SAEA.NatSocket.Utils
{
    internal class IPAddressesProvider : IIPAddressesProvider
    {
        #region IIPAddressesProvider Members

        public IEnumerable<IPAddress> UnicastAddresses()
        {
            return IPAddresses(p => p.UnicastAddresses.Select(x => x.Address));
        }

        public IEnumerable<IPAddress> DnsAddresses()
        {
            return IPAddresses(p => p.DnsAddresses);
        }

        public IEnumerable<IPAddress> GatewayAddresses()
        {
            return IPAddresses(p => p.GatewayAddresses.Select(x => x.Address));
        }

        #endregion

        private static IEnumerable<IPAddress> IPAddresses(Func<IPInterfaceProperties, IEnumerable<IPAddress>> ipExtractor)
        {
            return from networkInterface in NetworkInterface.GetAllNetworkInterfaces()
                   where
                       networkInterface.OperationalStatus == OperationalStatus.Up ||
                       networkInterface.OperationalStatus == OperationalStatus.Unknown
                   let properties = networkInterface.GetIPProperties()
                   from address in ipExtractor(properties)
                   where address.AddressFamily == AddressFamily.InterNetwork
                   select address;
        }
    }
}
