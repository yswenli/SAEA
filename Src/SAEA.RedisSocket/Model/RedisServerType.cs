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
*修改时间：2018/10/23 20:05:14
*修改人： yswenli
*版本号： v26.4.23.1
*描述：RedisServerType接口
*
*****************************************************************************/
namespace SAEA.RedisSocket.Model
{
    /// <summary>
    /// redis server type
    /// </summary>
    public enum RedisServerType
    {
        Master = 0,
        Slave = 1,
        ClusterMaster = 2,
        ClusterSlave = 3
    }
}
