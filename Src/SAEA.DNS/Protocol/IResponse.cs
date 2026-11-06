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
*唯一标识：e56f5207-6f78-417d-88f3-4b84b2c7ab6e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：IResponse接口
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
    /// DNS 响应消息契约
    /// </summary>
    public interface IResponse : IMessage
    {
        /// <summary>
        /// 消息标识，与对应请求一致
        /// </summary>
        int Id { get; set; }

        /// <summary>
        /// 应答资源记录列表
        /// </summary>
        IList<IResourceRecord> AnswerRecords { get; }

        /// <summary>
        /// 授权资源记录列表
        /// </summary>
        IList<IResourceRecord> AuthorityRecords { get; }

        /// <summary>
        /// 附加资源记录列表
        /// </summary>
        IList<IResourceRecord> AdditionalRecords { get; }

        /// <summary>
        /// 服务器是否支持递归查询
        /// </summary>
        bool RecursionAvailable { get; set; }

        /// <summary>
        /// 应答数据是否已通过 DNSSEC 验证
        /// </summary>
        bool AuthenticData { get; set; }

        /// <summary>
        /// 是否禁用 DNSSEC 校验
        /// </summary>
        bool CheckingDisabled { get; set; }

        /// <summary>
        /// 是否为授权服务器给出的应答
        /// </summary>
        bool AuthorativeServer { get; set; }

        /// <summary>
        /// 响应是否因超出传输长度而被截断
        /// </summary>
        bool Truncated { get; set; }

        /// <summary>
        /// 查询操作码
        /// </summary>
        OperationCode OperationCode { get; set; }

        /// <summary>
        /// 响应码
        /// </summary>
        ResponseCode ResponseCode { get; set; }
    }
}
