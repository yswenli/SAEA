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
*文件名： StartOfAuthorityResourceRecord
*版本号： v26.4.23.1
*唯一标识：ac767cb3-ed33-462e-a2c3-3f68c2969182
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：StartOfAuthorityResourceRecord接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:47
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Common;
using SAEA.DNS.Common.Utils;
using SAEA.DNS.Protocol;
using System;
using System.Runtime.InteropServices;

namespace SAEA.DNS.Common.ResourceRecords
{
    /// <summary>
    /// 开始权限资源记录
    /// </summary>
    public class StartOfAuthorityResourceRecord : BaseResourceRecord
    {
        private static IResourceRecord Create(Domain domain, Domain master, Domain responsible, long serial,
                TimeSpan refresh, TimeSpan retry, TimeSpan expire, TimeSpan minTtl, TimeSpan ttl)
        {
            ByteStream data = new ByteStream(Options.SIZE + master.Size + responsible.Size);

            Options tail = new Options()
            {
                SerialNumber = serial,
                RefreshInterval = refresh,
                RetryInterval = retry,
                ExpireInterval = expire,
                MinimumTimeToLive = minTtl
            };

            data
                .Append(master.ToArray())
                .Append(responsible.ToArray())
                .Append(StructHelper.GetBytes(tail));

            return new ResourceRecord(domain, data.ToArray(), RecordType.SOA, RecordClass.IN, ttl);
        }

        /// <summary>
        /// 从报文中解析 SOA 记录，依次读取主域名、责任人域名与 20 字节尾部字段
        /// </summary>
        /// <param name="record">已解析的记录头</param>
        /// <param name="message">完整 DNS 报文</param>
        /// <param name="dataOffset">数据段在报文中的起始偏移</param>
        public StartOfAuthorityResourceRecord(IResourceRecord record, byte[] message, int dataOffset)
            : base(record)
        {
            MasterDomainName = Domain.FromArray(message, dataOffset, out dataOffset);
            ResponsibleDomainName = Domain.FromArray(message, dataOffset, out dataOffset);

            Options tail = StructHelper.GetStruct<Options>(message, dataOffset, Options.SIZE);

            SerialNumber = tail.SerialNumber;
            RefreshInterval = tail.RefreshInterval;
            RetryInterval = tail.RetryInterval;
            ExpireInterval = tail.ExpireInterval;
            MinimumTimeToLive = tail.MinimumTimeToLive;
        }

        /// <summary>
        /// 用完整的 SOA 参数构造记录
        /// </summary>
        /// <param name="domain">记录所属域名</param>
        /// <param name="master">主域名服务器域名</param>
        /// <param name="responsible">责任人邮箱域名</param>
        /// <param name="serial">区文件版本号</param>
        /// <param name="refresh">从服务器刷新间隔</param>
        /// <param name="retry">刷新失败后的重试间隔</param>
        /// <param name="expire">从服务器停止应答后的失效时长</param>
        /// <param name="minTtl">否定应答的默认缓存时长</param>
        /// <param name="ttl">记录缓存有效期，默认零时长</param>
        public StartOfAuthorityResourceRecord(Domain domain, Domain master, Domain responsible, long serial,
                TimeSpan refresh, TimeSpan retry, TimeSpan expire, TimeSpan minTtl, TimeSpan ttl = default(TimeSpan)) :
            base(Create(domain, master, responsible, serial, refresh, retry, expire, minTtl, ttl))
        {
            MasterDomainName = master;
            ResponsibleDomainName = responsible;

            SerialNumber = serial;
            RefreshInterval = refresh;
            RetryInterval = retry;
            ExpireInterval = expire;
            MinimumTimeToLive = minTtl;
        }

        /// <summary>
        /// 用域名与 Options 尾部字段构造记录
        /// </summary>
        /// <param name="domain">记录所属域名</param>
        /// <param name="master">主域名服务器域名</param>
        /// <param name="responsible">责任人邮箱域名</param>
        /// <param name="options">版本号与各时间间隔，默认全零</param>
        /// <param name="ttl">记录缓存有效期，默认零时长</param>
        public StartOfAuthorityResourceRecord(Domain domain, Domain master, Domain responsible,
                Options options = default(Options), TimeSpan ttl = default(TimeSpan)) :
            this(domain, master, responsible, options.SerialNumber, options.RefreshInterval, options.RetryInterval,
                    options.ExpireInterval, options.MinimumTimeToLive, ttl)
        { }

        /// <summary>
        /// 主域名服务器域名
        /// </summary>
        public Domain MasterDomainName
        {
            get;
            private set;
        }

        /// <summary>
        /// 责任人邮箱域名，报文中以点代替 @
        /// </summary>
        public Domain ResponsibleDomainName
        {
            get;
            private set;
        }

        /// <summary>
        /// 区文件版本号
        /// </summary>
        public long SerialNumber
        {
            get;
            private set;
        }

        /// <summary>
        /// 从服务器刷新间隔
        /// </summary>
        public TimeSpan RefreshInterval
        {
            get;
            private set;
        }

        /// <summary>
        /// 刷新失败后的重试间隔
        /// </summary>
        public TimeSpan RetryInterval
        {
            get;
            private set;
        }

        /// <summary>
        /// 从服务器停止应答后的失效时长
        /// </summary>
        public TimeSpan ExpireInterval
        {
            get;
            private set;
        }

        /// <summary>
        /// 否定应答的默认缓存时长
        /// </summary>
        public TimeSpan MinimumTimeToLive
        {
            get;
            private set;
        }

        /// <summary>
        /// 输出记录名、类型、类别、有效期、数据长度、主域名、责任人域名与版本号
        /// </summary>
        /// <returns>字段拼接后的文本</returns>
        public override string ToString()
        {
            return Stringify().Add("MasterDomainName", "ResponsibleDomainName", "SerialNumber").ToString();
        }

        /// <summary>
        /// SOA 记录固定长度的尾部字段，共 20 字节，按大端布局
        /// </summary>
        [Endian(EndianOrder.Big)]
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct Options
        {
            /// <summary>
            /// 结构体的字节长度
            /// </summary>
            public const int SIZE = 20;

            private uint serialNumber;
            private uint refreshInterval;
            private uint retryInterval;
            private uint expireInterval;
            private uint ttl;

            /// <summary>
            /// 区文件版本号，按 32 位无符号存储
            /// </summary>
            public long SerialNumber
            {
                get { return serialNumber; }
                set { serialNumber = (uint)value; }
            }

            /// <summary>
            /// 从服务器刷新间隔，读写均以秒换算
            /// </summary>
            public TimeSpan RefreshInterval
            {
                get { return TimeSpan.FromSeconds(refreshInterval); }
                set { refreshInterval = (uint)value.TotalSeconds; }
            }

            /// <summary>
            /// 刷新失败后的重试间隔，读写均以秒换算
            /// </summary>
            public TimeSpan RetryInterval
            {
                get { return TimeSpan.FromSeconds(retryInterval); }
                set { retryInterval = (uint)value.TotalSeconds; }
            }

            /// <summary>
            /// 从服务器停止应答后的失效时长，读写均以秒换算
            /// </summary>
            public TimeSpan ExpireInterval
            {
                get { return TimeSpan.FromSeconds(expireInterval); }
                set { expireInterval = (uint)value.TotalSeconds; }
            }

            /// <summary>
            /// 否定应答的默认缓存时长，读写均以秒换算
            /// </summary>
            public TimeSpan MinimumTimeToLive
            {
                get { return TimeSpan.FromSeconds(ttl); }
                set { ttl = (uint)value.TotalSeconds; }
            }
        }
    }
}
