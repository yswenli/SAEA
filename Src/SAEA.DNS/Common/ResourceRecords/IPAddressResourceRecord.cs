/****************************************************************************
*项目名称：SAEA.DNS
*CLR 版本：3.0
*机器名称：WENLI-PC
*命名空间：SAEA.DNS.Common.ResourceRecords
*类 名 称：IPAddressResourceRecord
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
using SAEA.DNS.Protocol;
using System;
using System.Net;

namespace SAEA.DNS.Common.ResourceRecords
{
    /// <summary>
    /// IP地址资源记录
    /// </summary>
    public class IPAddressResourceRecord : BaseResourceRecord
    {
        private static IResourceRecord Create(Domain domain, IPAddress ip, TimeSpan ttl)
        {
            byte[] data = ip.GetAddressBytes();
            RecordType type = data.Length == 4 ? RecordType.A : RecordType.AAAA;

            return new ResourceRecord(domain, data, type, RecordClass.IN, ttl);
        }

        public IPAddressResourceRecord(IResourceRecord record) : base(record)
        {
            IPAddress = new IPAddress(Data);
        }

        public IPAddressResourceRecord(Domain domain, IPAddress ip, TimeSpan ttl = default(TimeSpan)) :
            base(Create(domain, ip, ttl))
        {
            IPAddress = ip;
        }

        public IPAddress IPAddress
        {
            get;
            private set;
        }

        public override string ToString()
        {
            return Stringify().Add("IPAddress").ToString();
        }
    }
}
