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
*文件名： BaseResourceRecord
*版本号： v26.4.23.1
*唯一标识：9a610e76-cbc4-4279-9722-daac1cb454b0
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：BaseResourceRecord记录类
*
*=====================================================================
*修改标记
*修改时间：2019/11/29 20:45:22
*修改人： yswenli
*版本号： v26.4.23.1
*描述：BaseResourceRecord记录类
*
*****************************************************************************/
using SAEA.DNS.Common.Utils;
using SAEA.DNS.Protocol;
using System;

namespace SAEA.DNS.Common.ResourceRecords
{
    public abstract class BaseResourceRecord : IResourceRecord
    {
        private IResourceRecord record;

        public BaseResourceRecord(IResourceRecord record)
        {
            this.record = record;
        }

        public Domain Name
        {
            get { return record.Name; }
        }

        public RecordType Type
        {
            get { return record.Type; }
        }

        public RecordClass Class
        {
            get { return record.Class; }
        }

        public TimeSpan TimeToLive
        {
            get { return record.TimeToLive; }
        }

        public int DataLength
        {
            get { return record.DataLength; }
        }

        public byte[] Data
        {
            get { return record.Data; }
        }

        public int Size
        {
            get { return record.Size; }
        }

        public byte[] ToArray()
        {
            return record.ToArray();
        }

        internal ObjectStringifier Stringify()
        {
            return ObjectStringifier.New(this)
                .Add("Name", "Type", "Class", "TimeToLive", "DataLength");
        }
    }
}
