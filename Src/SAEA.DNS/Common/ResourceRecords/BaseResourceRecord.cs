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
*修改时间：2026/11/6 12:40:47
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.DNS.Common.Utils;
using SAEA.DNS.Protocol;
using System;

namespace SAEA.DNS.Common.ResourceRecords
{
    /// <summary>
    /// 资源记录包装基类，各成员均转发给被包装的记录
    /// </summary>
    public abstract class BaseResourceRecord : IResourceRecord
    {
        private IResourceRecord record;

        /// <summary>
        /// 包装一条已存在的资源记录
        /// </summary>
        /// <param name="record">被包装的资源记录</param>
        public BaseResourceRecord(IResourceRecord record)
        {
            this.record = record;
        }

        /// <summary>
        /// 记录所属域名
        /// </summary>
        public Domain Name
        {
            get { return record.Name; }
        }

        /// <summary>
        /// 记录类型
        /// </summary>
        public RecordType Type
        {
            get { return record.Type; }
        }

        /// <summary>
        /// 记录类别
        /// </summary>
        public RecordClass Class
        {
            get { return record.Class; }
        }

        /// <summary>
        /// 记录可缓存的有效期
        /// </summary>
        public TimeSpan TimeToLive
        {
            get { return record.TimeToLive; }
        }

        /// <summary>
        /// 数据段字节长度
        /// </summary>
        public int DataLength
        {
            get { return record.DataLength; }
        }

        /// <summary>
        /// 数据段原始字节
        /// </summary>
        public byte[] Data
        {
            get { return record.Data; }
        }

        /// <summary>
        /// 整条记录序列化后的字节数
        /// </summary>
        public int Size
        {
            get { return record.Size; }
        }

        /// <summary>
        /// 序列化为 DNS 报文片段
        /// </summary>
        /// <returns>域名、类型、类别、有效期与数据段组成的字节数组</returns>
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
