/****************************************************************************
*项目名称：SAEA.DNS
*CLR 版本：3.0
*机器名称：WENLI-PC
*命名空间：SAEA.DNS.Common.ResourceRecords
*类 名 称：ResourceRecordFactory
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
using System.Collections.Generic;

namespace SAEA.DNS.Common.ResourceRecords
{
    /// <summary>
    /// 资源记录工厂
    /// </summary>
    public static class ResourceRecordFactory
    {
        public static IList<IResourceRecord> GetAllFromArray(byte[] message, int offset, int count)
        {
            return GetAllFromArray(message, offset, count, out offset);
        }

        public static IList<IResourceRecord> GetAllFromArray(byte[] message, int offset, int count, out int endOffset)
        {
            IList<IResourceRecord> result = new List<IResourceRecord>(count);

            for (int i = 0; i < count; i++)
            {
                result.Add(FromArray(message, offset, out offset));
            }

            endOffset = offset;
            return result;
        }

        public static IResourceRecord FromArray(byte[] message, int offset)
        {
            return FromArray(message, offset, out offset);
        }

        public static IResourceRecord FromArray(byte[] message, int offset, out int endOffest)
        {
            ResourceRecord record = ResourceRecord.FromArray(message, offset, out endOffest);
            int dataOffset = endOffest - record.DataLength;

            switch (record.Type)
            {
                case RecordType.A:
                case RecordType.AAAA:
                    return new IPAddressResourceRecord(record);
                case RecordType.NS:
                    return new NameServerResourceRecord(record, message, dataOffset);
                case RecordType.CNAME:
                    return new CanonicalNameResourceRecord(record, message, dataOffset);
                case RecordType.SOA:
                    return new StartOfAuthorityResourceRecord(record, message, dataOffset);
                case RecordType.PTR:
                    return new PointerResourceRecord(record, message, dataOffset);
                case RecordType.MX:
                    return new MailExchangeResourceRecord(record, message, dataOffset);
                case RecordType.TXT:
                    return new TextResourceRecord(record);
                default:
                    return record;
            }
        }
    }
}
