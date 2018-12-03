/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.NatSocket
*文件名： Class1
*版本号： V3.3.3.5
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
*版本号： V3.3.3.5
*描述：
*
*****************************************************************************/
using SAEA.NatSocket.Base;
using SAEA.NatSocket.Enums;
using System.Collections.Generic;
using System.Net;

namespace SAEA.NatSocket.Upnp.Messages.Requests
{
    internal class CreatePortMappingRequestMessage : RequestMessageBase
    {
        private readonly Mapping _mapping;

        public CreatePortMappingRequestMessage(Mapping mapping)
        {
            _mapping = mapping;
        }

        public override IDictionary<string, object> ToXml()
        {
            string remoteHost = _mapping.PublicIP.Equals(IPAddress.None)
                                    ? string.Empty
                                    : _mapping.PublicIP.ToString();

            return new Dictionary<string, object>
                       {
                           {"NewRemoteHost", remoteHost},
                           {"NewExternalPort", _mapping.PublicPort},
                           {"NewProtocol", _mapping.Protocol == Protocol.Tcp ? "TCP" : "UDP"},
                           {"NewInternalPort", _mapping.PrivatePort},
                           {"NewInternalClient", _mapping.PrivateIP},
                           {"NewEnabled", 1},
                           {"NewPortMappingDescription", _mapping.Description},
                           {"NewLeaseDuration", _mapping.Lifetime}
                       };
        }
    }
}
