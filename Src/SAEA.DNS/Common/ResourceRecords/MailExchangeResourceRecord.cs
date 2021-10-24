/****************************************************************************
*项目名称：SAEA.DNS
*CLR 版本：3.0
*机器名称：WENLI-PC
*命名空间：SAEA.DNS.Common.ResourceRecords
*类 名 称：MailExchangeResourceRecord
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

        public MailExchangeResourceRecord(Domain domain, int preference, Domain exchange, TimeSpan ttl = default(TimeSpan)) :
            base(Create(domain, preference, exchange, ttl))
        {
            Preference = preference;
            ExchangeDomainName = exchange;
        }

        public int Preference
        {
            get;
            private set;
        }

        public Domain ExchangeDomainName
        {
            get;
            private set;
        }

        public override string ToString()
        {
            return Stringify().Add("Preference", "ExchangeDomainName").ToString();
        }
    }
}
