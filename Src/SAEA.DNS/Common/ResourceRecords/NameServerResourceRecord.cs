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
*修改时间：2026/11/6 12:40:47
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
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
        /// <summary>
        /// 从报文中解析 NS 记录
        /// </summary>
        /// <param name="record">已解析的记录头</param>
        /// <param name="message">完整 DNS 报文</param>
        /// <param name="dataOffset">数据段在报文中的起始偏移</param>
        public NameServerResourceRecord(IResourceRecord record, byte[] message, int dataOffset)
            : base(record)
        {
            NSDomainName = Domain.FromArray(message, dataOffset);
        }

        /// <summary>
        /// 用域名与名称服务器域名构造 NS 记录
        /// </summary>
        /// <param name="domain">记录所属域名</param>
        /// <param name="nsDomain">名称服务器域名</param>
        /// <param name="ttl">缓存有效期，默认零时长</param>
        public NameServerResourceRecord(Domain domain, Domain nsDomain, TimeSpan ttl = default(TimeSpan)) :
            base(new ResourceRecord(domain, nsDomain.ToArray(), RecordType.NS, RecordClass.IN, ttl))
        {
            NSDomainName = nsDomain;
        }

        /// <summary>
        /// 名称服务器域名
        /// </summary>
        public Domain NSDomainName
        {
            get;
            private set;
        }

        /// <summary>
        /// 输出记录名、类型、类别、有效期、数据长度与名称服务器域名
        /// </summary>
        /// <returns>字段拼接后的文本</returns>
        public override string ToString()
        {
            return Stringify().Add("NSDomainName").ToString();
        }
    }
}
