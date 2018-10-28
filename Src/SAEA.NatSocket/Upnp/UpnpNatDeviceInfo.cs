/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.NatSocket
*文件名： Class1
*版本号： V3.0.0.1
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
*版本号： V3.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.NatSocket.Base;
using SAEA.NatSocket.Utils;
using System;
using System.Net;

namespace SAEA.NatSocket.Upnp
{
    internal class UpnpNatDeviceInfo
    {
        public UpnpNatDeviceInfo(IPAddress localAddress, Uri locationUri, string serviceControlUrl, string serviceType)
        {
            LocalAddress = localAddress;
            ServiceType = serviceType;
            HostEndPoint = new IPEndPoint(IPAddress.Parse(locationUri.Host), locationUri.Port);

            if (Uri.IsWellFormedUriString(serviceControlUrl, UriKind.Absolute))
            {
                var u = new Uri(serviceControlUrl);
                IPEndPoint old = HostEndPoint;
                serviceControlUrl = u.PathAndQuery;

                NatDiscoverer.TraceSource.LogInfo("{0}: Absolute URI detected. Host address is now: {1}", old,
                                                  HostEndPoint);
                NatDiscoverer.TraceSource.LogInfo("{0}: New control url: {1}", HostEndPoint, serviceControlUrl);
            }

            var builder = new UriBuilder("http", locationUri.Host, locationUri.Port);
            ServiceControlUri = new Uri(builder.Uri, serviceControlUrl); ;
        }

        public IPEndPoint HostEndPoint { get; private set; }
        public IPAddress LocalAddress { get; private set; }
        public string ServiceType { get; private set; }
        public Uri ServiceControlUri { get; private set; }
    }
}
