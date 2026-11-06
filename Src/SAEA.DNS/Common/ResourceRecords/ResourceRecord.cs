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
*文件名： ResourceRecord
*版本号： v26.4.23.1
*唯一标识：c4a513bc-f0ca-4681-9462-7091d44e5a55
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：ResourceRecord记录类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:47
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SAEA.Common;
using SAEA.DNS.Common.Utils;
using SAEA.DNS.Protocol;

namespace SAEA.DNS.Common.ResourceRecords
{
    /// <summary>
    /// 资源记录
    /// </summary>
    public class ResourceRecord : IResourceRecord
    {
        private Domain domain;
        private RecordType type;
        private RecordClass klass;
        private TimeSpan ttl;
        private byte[] data;

        /// <summary>
        /// 从指定偏移开始解析指定数量的资源记录
        /// </summary>
        /// <param name="message">完整 DNS 报文</param>
        /// <param name="offset">起始偏移</param>
        /// <param name="count">待解析的记录条数</param>
        /// <returns>解析出的记录列表，顺序与报文一致</returns>
        public static IList<ResourceRecord> GetAllFromArray(byte[] message, int offset, int count)
        {
            return GetAllFromArray(message, offset, count, out offset);
        }

        /// <summary>
        /// 从指定偏移开始解析指定数量的资源记录，并返回结束偏移
        /// </summary>
        /// <param name="message">完整 DNS 报文</param>
        /// <param name="offset">起始偏移</param>
        /// <param name="count">待解析的记录条数</param>
        /// <param name="endOffset">最后一条记录之后的报文偏移</param>
        /// <returns>解析出的记录列表，顺序与报文一致</returns>
        public static IList<ResourceRecord> GetAllFromArray(byte[] message, int offset, int count, out int endOffset)
        {
            IList<ResourceRecord> records = new List<ResourceRecord>(count);

            for (int i = 0; i < count; i++)
            {
                records.Add(FromArray(message, offset, out offset));
            }

            endOffset = offset;
            return records;
        }

        /// <summary>
        /// 从指定偏移解析一条资源记录
        /// </summary>
        /// <param name="message">完整 DNS 报文</param>
        /// <param name="offset">起始偏移</param>
        /// <returns>解析出的记录</returns>
        public static ResourceRecord FromArray(byte[] message, int offset)
        {
            return FromArray(message, offset, out offset);
        }

        /// <summary>
        /// 从指定偏移解析一条资源记录，并返回结束偏移
        /// </summary>
        /// <param name="message">完整 DNS 报文</param>
        /// <param name="offset">起始偏移</param>
        /// <param name="endOffset">该记录之后的报文偏移</param>
        /// <returns>解析出的记录</returns>
        public static ResourceRecord FromArray(byte[] message, int offset, out int endOffset)
        {
            Domain domain = Domain.FromArray(message, offset, out offset);
            Tail tail = StructHelper.GetStruct<Tail>(message, offset, Tail.SIZE);

            byte[] data = new byte[tail.DataLength];

            offset += Tail.SIZE;
            Array.Copy(message, offset, data, 0, data.Length);

            endOffset = offset + data.Length;

            return new ResourceRecord(domain, data, tail.Type, tail.Class, tail.TimeToLive);
        }

        /// <summary>
        /// 复用问题段的域名、类型与类别构造一条资源记录
        /// </summary>
        /// <param name="question">作为记录键的问题</param>
        /// <param name="data">数据段字节</param>
        /// <param name="ttl">缓存有效期，默认零时长</param>
        /// <returns>新建的资源记录</returns>
        public static ResourceRecord FromQuestion(Question question, byte[] data, TimeSpan ttl = default(TimeSpan))
        {
            return new ResourceRecord(question.Name, data, question.Type, question.Class, ttl);
        }

        /// <summary>
        /// 指定域名、数据段、类型、类别与有效期创建资源记录
        /// </summary>
        /// <param name="domain">记录所属域名</param>
        /// <param name="data">数据段字节，长度写入固定头</param>
        /// <param name="type">记录类型</param>
        /// <param name="klass">记录类别，默认 IN</param>
        /// <param name="ttl">缓存有效期，默认零时长</param>
        public ResourceRecord(Domain domain, byte[] data, RecordType type,
                RecordClass klass = RecordClass.IN, TimeSpan ttl = default(TimeSpan))
        {
            this.domain = domain;
            this.type = type;
            this.klass = klass;
            this.ttl = ttl;
            this.data = data;
        }

        /// <summary>
        /// 记录所属域名
        /// </summary>
        public Domain Name
        {
            get { return domain; }
        }

        /// <summary>
        /// 记录类型
        /// </summary>
        public RecordType Type
        {
            get { return type; }
        }

        /// <summary>
        /// 记录类别
        /// </summary>
        public RecordClass Class
        {
            get { return klass; }
        }

        /// <summary>
        /// 记录可缓存的有效期
        /// </summary>
        public TimeSpan TimeToLive
        {
            get { return ttl; }
        }

        /// <summary>
        /// 数据段字节长度
        /// </summary>
        public int DataLength
        {
            get { return data.Length; }
        }

        /// <summary>
        /// 数据段原始字节
        /// </summary>
        public byte[] Data
        {
            get { return data; }
        }

        /// <summary>
        /// 域名、固定头与数据段合计的字节数
        /// </summary>
        public int Size
        {
            get { return domain.Size + Tail.SIZE + data.Length; }
        }

        /// <summary>
        /// 序列化为 DNS 报文片段，固定头按大端写出
        /// </summary>
        /// <returns>域名、类型、类别、有效期、数据长度与数据段组成的字节数组</returns>
        public byte[] ToArray()
        {
            ByteStream result = new ByteStream(Size);

            result
                .Append(domain.ToArray())
                .Append(StructHelper.GetBytes<Tail>(new Tail()
                {
                    Type = Type,
                    Class = Class,
                    TimeToLive = ttl,
                    DataLength = data.Length
                }))
                .Append(data);

            return result.ToArray();
        }

        /// <summary>
        /// 输出记录名、类型、类别、有效期与数据长度的字段明细
        /// </summary>
        /// <returns>字段拼接后的文本</returns>
        public override string ToString()
        {
            return ObjectStringifier.New(this)
                .Add("Name", "Type", "Class", "TimeToLive", "DataLength")
                .ToString();
        }

        [Endian(EndianOrder.Big)]
        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct Tail
        {
            public const int SIZE = 10;

            private ushort type;
            private ushort klass;
            private uint ttl;
            private ushort dataLength;

            public RecordType Type
            {
                get { return (RecordType)type; }
                set { type = (ushort)value; }
            }

            public RecordClass Class
            {
                get { return (RecordClass)klass; }
                set { klass = (ushort)value; }
            }

            public TimeSpan TimeToLive
            {
                get { return TimeSpan.FromSeconds(ttl); }
                set { ttl = (uint)value.TotalSeconds; }
            }

            public int DataLength
            {
                get { return dataLength; }
                set { dataLength = (ushort)value; }
            }
        }
    }
}
