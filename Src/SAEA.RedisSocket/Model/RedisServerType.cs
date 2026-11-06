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
*命名空间：SAEA.RedisSocket.Model
*文件名： RedisServerType
*版本号： v26.4.23.1
*唯一标识：37ba6cf4-22fc-40ac-a469-33586f9d635b
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/23 20:05:14
*描述：RedisServerType接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:46
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.RedisSocket.Model
{
    /// <summary>
    /// redis 服务端的角色类型
    /// </summary>
    public enum RedisServerType
    {
        /// <summary>
        /// 单机主节点
        /// </summary>
        Master = 0,
        /// <summary>
        /// 单机从节点
        /// </summary>
        Slave = 1,
        /// <summary>
        /// 集群主节点
        /// </summary>
        ClusterMaster = 2,
        /// <summary>
        /// 集群从节点
        /// </summary>
        ClusterSlave = 3
    }
}
