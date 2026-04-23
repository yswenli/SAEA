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
*文件名： ResourceRecordFactory
*版本号： v26.4.23.1
*唯一标识：14a578b1-234d-47c1-a1ae-aaaf985f735c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：ResourceRecordFactory工厂类
*
*=====================================================================
*修改标记
*修改时间：2019/11/29 20:45:22
*修改人： yswenli
*版本号： v26.4.23.1
*描述：ResourceRecordFactory工厂类
*
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
