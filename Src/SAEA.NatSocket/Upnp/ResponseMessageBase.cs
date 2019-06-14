/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.NatSocket
*文件名： Class1
*版本号： v4.5.6.7
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
*版本号： v4.5.6.7
*描述：
*
*****************************************************************************/
using SAEA.Common;
using System;
using System.Xml;

namespace SAEA.NatSocket.Upnp
{
    internal abstract class ResponseMessageBase
    {
        private readonly XmlDocument _document;
        protected string ServiceType;
        private readonly string _typeName;

        protected ResponseMessageBase(XmlDocument response, string serviceType, string typeName)
        {
            _document = response;
            ServiceType = serviceType;
            _typeName = typeName;
        }

        protected XmlNode GetNode()
        {
            var nsm = new XmlNamespaceManager(_document.NameTable);
            nsm.AddNamespace("responseNs", ServiceType);

            string typeName = _typeName;
            string messageName = StringHelper.Substring(typeName, 0, typeName.Length - "Message".Length);
            XmlNode node = _document.SelectSingleNode("//responseNs:" + messageName, nsm);
            if (node == null) throw new InvalidOperationException("The response is invalid: " + messageName);

            return node;
        }
    }
}
