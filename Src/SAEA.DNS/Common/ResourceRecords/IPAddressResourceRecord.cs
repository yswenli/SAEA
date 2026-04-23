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
*修改时间：2019/11/29 20:45:22
*修改人： yswenli
*版本号： v26.4.23.1
*描述：IPAddressResourceRecord接口
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

        public IPAddressResourceRecord(IResourceRecord record) : base(record)
        {
            IPAddress = new IPAddress(Data);
        }

        public IPAddressResourceRecord(Domain domain, IPAddress ip, TimeSpan ttl = default(TimeSpan)) :
            base(Create(domain, ip, ttl))
        {
            IPAddress = ip;
        }

        public IPAddress IPAddress
        {
            get;
            private set;
        }

        public override string ToString()
        {
            return Stringify().Add("IPAddress").ToString();
        }
    }
}
