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
*文件名： CanonicalNameResourceRecord
*版本号： v26.4.23.1
*唯一标识：1d144421-93b6-42fb-a58b-48d4e6067004
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：CanonicalNameResourceRecord接口
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
    /// 规范名称（CNAME）资源记录
    /// </summary>
    public class CanonicalNameResourceRecord : BaseResourceRecord
    {
        /// <summary>
        /// 从报文中解析 CNAME 记录
        /// </summary>
        /// <param name="record">已解析的记录头</param>
        /// <param name="message">完整 DNS 报文</param>
        /// <param name="dataOffset">数据段在报文中的起始偏移</param>
        public CanonicalNameResourceRecord(IResourceRecord record, byte[] message, int dataOffset)
            : base(record)
        {
            CanonicalDomainName = Domain.FromArray(message, dataOffset);
        }

        /// <summary>
        /// 用域名构造 CNAME 记录
        /// </summary>
        /// <param name="domain">记录所属域名</param>
        /// <param name="cname">别名指向的规范域名</param>
        /// <param name="ttl">缓存有效期，默认零时长</param>
        public CanonicalNameResourceRecord(Domain domain, Domain cname, TimeSpan ttl = default(TimeSpan)) :
            base(new ResourceRecord(domain, cname.ToArray(), RecordType.CNAME, RecordClass.IN, ttl))
        {
            CanonicalDomainName = cname;
        }

        /// <summary>
        /// 别名指向的规范域名
        /// </summary>
        public Domain CanonicalDomainName
        {
            get;
            private set;
        }

        /// <summary>
        /// 输出记录名、类型、类别、有效期、数据长度与规范域名
        /// </summary>
        /// <returns>字段拼接后的文本</returns>
        public override string ToString()
        {
            return Stringify().Add("CanonicalDomainName").ToString();
        }
    }
}
