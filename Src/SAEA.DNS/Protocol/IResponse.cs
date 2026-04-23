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
*命名空间：SAEA.DNS.Protocol
*文件名： IResponse
*版本号： v26.4.23.1
*唯一标识：c6ff47fd-fb41-44d0-b587-91415b30608e
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
using SAEA.DNS.Common.ResourceRecords;
using System.Collections.Generic;

namespace SAEA.DNS.Protocol
{
    public interface IResponse : IMessage
    {
        int Id { get; set; }
        IList<IResourceRecord> AnswerRecords { get; }
        IList<IResourceRecord> AuthorityRecords { get; }
        IList<IResourceRecord> AdditionalRecords { get; }
        bool RecursionAvailable { get; set; }
        bool AuthenticData { get; set; }
        bool CheckingDisabled { get; set; }
        bool AuthorativeServer { get; set; }
        bool Truncated { get; set; }
        OperationCode OperationCode { get; set; }
        ResponseCode ResponseCode { get; set; }
    }
}
