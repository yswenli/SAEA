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
*文件名： MailExchangeResourceRecord
*版本号： v26.4.23.1
*唯一标识：8f82a8f8-d469-4aec-ba7a-45bdee4b5c5a
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：MailExchangeResourceRecord接口
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
    /// 邮件交换资源记录
    /// </summary>
    public class MailExchangeResourceRecord : BaseResourceRecord
    {
        private const int PREFERENCE_SIZE = 2;

        private static IResourceRecord Create(Domain domain, int preference, Domain exchange, TimeSpan ttl)
        {
            byte[] pref = BitConverter.GetBytes((ushort)preference);
            byte[] data = new byte[pref.Length + exchange.Size];

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(pref);
            }

            pref.CopyTo(data, 0);
            exchange.ToArray().CopyTo(data, pref.Length);

            return new ResourceRecord(domain, data, RecordType.MX, RecordClass.IN, ttl);
        }

        /// <summary>
        /// 从报文中解析 MX 记录，先取 2 字节大端优先值再取交换域名
        /// </summary>
        /// <param name="record">已解析的记录头</param>
        /// <param name="message">完整 DNS 报文</param>
        /// <param name="dataOffset">数据段在报文中的起始偏移</param>
        public MailExchangeResourceRecord(IResourceRecord record, byte[] message, int dataOffset)
            : base(record)
        {
            byte[] preference = new byte[MailExchangeResourceRecord.PREFERENCE_SIZE];
            Array.Copy(message, dataOffset, preference, 0, preference.Length);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(preference);
            }

            dataOffset += MailExchangeResourceRecord.PREFERENCE_SIZE;

            Preference = BitConverter.ToUInt16(preference, 0);
            ExchangeDomainName = Domain.FromArray(message, dataOffset);
        }

        /// <summary>
        /// 用域名、优先级与交换域名构造 MX 记录，优先值按大端写入数据段
        /// </summary>
        /// <param name="domain">记录所属域名</param>
        /// <param name="preference">邮件服务器优先级，数值越小越优先</param>
        /// <param name="exchange">邮件交换域名</param>
        /// <param name="ttl">缓存有效期，默认零时长</param>
        public MailExchangeResourceRecord(Domain domain, int preference, Domain exchange, TimeSpan ttl = default(TimeSpan)) :
            base(Create(domain, preference, exchange, ttl))
        {
            Preference = preference;
            ExchangeDomainName = exchange;
        }

        /// <summary>
        /// 邮件服务器优先级，数值越小越优先
        /// </summary>
        public int Preference
        {
            get;
            private set;
        }

        /// <summary>
        /// 邮件交换域名
        /// </summary>
        public Domain ExchangeDomainName
        {
            get;
            private set;
        }

        /// <summary>
        /// 输出记录名、类型、类别、有效期、数据长度、优先级与交换域名
        /// </summary>
        /// <returns>字段拼接后的文本</returns>
        public override string ToString()
        {
            return Stringify().Add("Preference", "ExchangeDomainName").ToString();
        }
    }
}
