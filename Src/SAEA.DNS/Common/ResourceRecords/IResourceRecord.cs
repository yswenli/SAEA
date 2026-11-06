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
*文件名： IResourceRecord
*版本号： v26.4.23.1
*唯一标识：2c4ebe58-2012-4279-a694-71d2a2c7ea43
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：IResourceRecord接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:47
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.DNS.Protocol;
using System;

namespace SAEA.DNS.Common.ResourceRecords
{
    /// <summary>
    /// 资源记录契约，在报文条目之上补充有效期与数据段
    /// </summary>
    public interface IResourceRecord : IMessageEntry {
        /// <summary>
        /// 记录可缓存的有效期
        /// </summary>
        TimeSpan TimeToLive { get; }
        /// <summary>
        /// 数据段字节长度
        /// </summary>
        int DataLength { get; }
        /// <summary>
        /// 数据段原始字节
        /// </summary>
        byte[] Data { get; }
    }
}
