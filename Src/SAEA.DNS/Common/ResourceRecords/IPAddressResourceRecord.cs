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
*文件名： IPAddressResourceRecord
*版本号： v26.4.23.1
*唯一标识：5eb49e81-5264-4aa9-8204-ad3ad3ea54b3
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：IPAddressResourceRecord接口
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

        /// <summary>
        /// 从已解析记录的数据段构造地址记录
        /// </summary>
        /// <param name="record">已解析的记录，数据段为 4 或 16 字节 IP</param>
        public IPAddressResourceRecord(IResourceRecord record) : base(record)
        {
            IPAddress = new IPAddress(Data);
        }

        /// <summary>
        /// 用域名与 IP 构造地址记录，按地址字节数自动选择 A 或 AAAA 类型
        /// </summary>
        /// <param name="domain">记录所属域名</param>
        /// <param name="ip">IPv4 或 IPv6 地址</param>
        /// <param name="ttl">缓存有效期，默认零时长</param>
        public IPAddressResourceRecord(Domain domain, IPAddress ip, TimeSpan ttl = default(TimeSpan)) :
            base(Create(domain, ip, ttl))
        {
            IPAddress = ip;
        }

        /// <summary>
        /// 记录中保存的 IP 地址
        /// </summary>
        public IPAddress IPAddress
        {
            get;
            private set;
        }

        /// <summary>
        /// 输出记录名、类型、类别、有效期、数据长度与 IP 地址
        /// </summary>
        /// <returns>字段拼接后的文本</returns>
        public override string ToString()
        {
            return Stringify().Add("IPAddress").ToString();
        }
    }
}
