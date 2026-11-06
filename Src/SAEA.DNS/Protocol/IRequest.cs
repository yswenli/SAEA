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
*文件名： IRequest
*版本号： v26.4.23.1
*唯一标识：ec5e3c98-da10-49dc-869a-2859ee80cc1e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：IRequest接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:47
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.DNS.Common.ResourceRecords;
using System.Collections.Generic;

namespace SAEA.DNS.Protocol
{
    /// <summary>
    /// DNS 请求消息契约
    /// </summary>
    public interface IRequest : IMessage
    {
        /// <summary>
        /// 消息标识
        /// </summary>
        int Id { get; set; }

        /// <summary>
        /// 附加资源记录列表
        /// </summary>
        IList<IResourceRecord> AdditionalRecords { get; }

        /// <summary>
        /// 查询操作码
        /// </summary>
        OperationCode OperationCode { get; set; }

        /// <summary>
        /// 是否期望递归查询
        /// </summary>
        bool RecursionDesired { get; set; }
    }
}
