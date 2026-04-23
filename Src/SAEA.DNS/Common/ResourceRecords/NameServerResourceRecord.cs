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
*命名空间：SAEA.DNS.Common.ResourceRecords
*文件名： NameServerResourceRecord
*版本号： v26.4.23.1
*唯一标识：c9a4e2fe-69c9-41c6-abc2-ebcdb4df14cf
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：NameServerResourceRecord记录类
*
*=====================================================================
*修改标记
*修改时间：2019/11/29 20:45:22
*修改人： yswenli
*版本号： v26.4.23.1
*描述：NameServerResourceRecord记录类
*
*****************************************************************************/
using SAEA.DNS.Protocol;
using System;

namespace SAEA.DNS.Common.ResourceRecords
{
    /// <summary>
    /// 名称服务器资源记录
    /// </summary>
    public class NameServerResourceRecord : BaseResourceRecord
    {
        public NameServerResourceRecord(IResourceRecord record, byte[] message, int dataOffset)
            : base(record)
        {
            NSDomainName = Domain.FromArray(message, dataOffset);
        }

        public NameServerResourceRecord(Domain domain, Domain nsDomain, TimeSpan ttl = default(TimeSpan)) :
            base(new ResourceRecord(domain, nsDomain.ToArray(), RecordType.NS, RecordClass.IN, ttl))
        {
            NSDomainName = nsDomain;
        }

        public Domain NSDomainName
        {
            get;
            private set;
        }

        public override string ToString()
        {
            return Stringify().Add("NSDomainName").ToString();
        }
    }
}
