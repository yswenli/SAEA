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
*文件名： OperationCode
*版本号： v26.4.23.1
*唯一标识：8f3ff9fc-d3f6-4a7d-8a43-304ddaba73f3
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：OperationCode接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:47
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.DNS.Protocol
{
    /// <summary>
    /// DNS 操作码，标识报文头的查询类型
    /// </summary>
    public enum OperationCode
    {
        /// <summary>
        /// 标准查询
        /// </summary>
        Query = 0,
        /// <summary>
        /// 反向查询（已废弃）
        /// </summary>
        IQuery,
        /// <summary>
        /// 服务器状态查询
        /// </summary>
        Status,
        // Reserved = 3
        /// <summary>
        /// 区域变更通知
        /// </summary>
        Notify = 4,
        /// <summary>
        /// 动态更新
        /// </summary>
        Update,
    }
}
