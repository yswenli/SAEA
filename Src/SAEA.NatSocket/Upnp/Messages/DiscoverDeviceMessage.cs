/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.NatSocket
*文件名： Class1
*版本号： V2.2.2.0
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
*版本号： V2.2.2.0
*描述：
*
*****************************************************************************/
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace SAEA.NatSocket.Upnp.Messages
{
    internal static class DiscoverDeviceMessage
    {
        /// <summary>
        /// The message sent to discover all uPnP devices on the network
        /// </summary>
        /// <returns></returns>
        public static string Encode(string serviceType)
        {
            const string s = "M-SEARCH * HTTP/1.1\r\n"
                             + "HOST: 239.255.255.250:1900\r\n"
                             + "MAN: \"ssdp:discover\"\r\n"
                             + "MX: 3\r\n"
                             // + "ST: urn:schemas-upnp-org:service:WANIPConnection:1\r\n\r\n";
                             + "ST: urn:schemas-upnp-org:service:{0}\r\n\r\n";
            //+ "ST:upnp:rootdevice\r\n\r\n";

            string ss = string.Format(CultureInfo.InvariantCulture, s, serviceType);
            return ss;
        }
    }
}
