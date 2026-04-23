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
*文件名： PointerResourceRecord
*版本号： v26.4.23.1
*唯一标识：17e62cf5-7289-496f-9206-4873481e3218
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：
*
*=====================================================================
*修改标记
*修改时间：2019/11/29 20:45:22
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using SAEA.DNS.Protocol;
using System;
using System.Net;

namespace SAEA.DNS.Common.ResourceRecords
{
    /// <summary>
    /// 指针资源记录
    /// </summary>
    public class PointerResourceRecord : BaseResourceRecord 
    {
        public PointerResourceRecord(IResourceRecord record, byte[] message, int dataOffset)
            : base(record) {
            PointerDomainName = Domain.FromArray(message, dataOffset);
        }

        public PointerResourceRecord(IPAddress ip, Domain pointer, TimeSpan ttl = default(TimeSpan)) :
            base(new ResourceRecord(Domain.PointerName(ip), pointer.ToArray(), RecordType.PTR, RecordClass.IN, ttl)) {
            PointerDomainName = pointer;
        }

        public Domain PointerDomainName {
            get;
            private set;
        }

        public override string ToString() {
            return Stringify().Add("PointerDomainName").ToString();
        }
    }
}
