/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.NatSocket
*文件名： Class1
*版本号： V2.2.1.1
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
*版本号： V2.2.1.1
*描述：
*
*****************************************************************************/
using SAEA.NatSocket.Enums;
using SAEA.NatSocket.Utils;
using System;
using System.Xml;

namespace SAEA.NatSocket.Upnp.Messages.Responses
{
    internal class GetPortMappingEntryResponseMessage : ResponseMessageBase
    {
        internal GetPortMappingEntryResponseMessage(XmlDocument response, string serviceType, bool genericMapping)
            : base(response, serviceType, genericMapping ? "GetGenericPortMappingEntryResponseMessage" : "GetSpecificPortMappingEntryResponseMessage")
        {
            XmlNode data = GetNode();

            RemoteHost = (genericMapping) ? data.GetXmlElementText("NewRemoteHost") : string.Empty;
            ExternalPort = (genericMapping) ? Convert.ToInt32(data.GetXmlElementText("NewExternalPort")) : ushort.MaxValue;
            if (genericMapping)
                Protocol = data.GetXmlElementText("NewProtocol").Equals("TCP", StringComparison.InvariantCultureIgnoreCase)
                               ? Protocol.Tcp
                               : Protocol.Udp;
            else
                Protocol = Protocol.Udp;

            InternalPort = Convert.ToInt32(data.GetXmlElementText("NewInternalPort"));
            InternalClient = data.GetXmlElementText("NewInternalClient");
            Enabled = data.GetXmlElementText("NewEnabled") == "1";
            PortMappingDescription = data.GetXmlElementText("NewPortMappingDescription");
            LeaseDuration = Convert.ToInt32(data.GetXmlElementText("NewLeaseDuration"));
        }

        public string RemoteHost { get; private set; }
        public int ExternalPort { get; private set; }
        public Protocol Protocol { get; private set; }
        public int InternalPort { get; private set; }
        public string InternalClient { get; private set; }
        public bool Enabled { get; private set; }
        public string PortMappingDescription { get; private set; }
        public int LeaseDuration { get; private set; }
    }
}
