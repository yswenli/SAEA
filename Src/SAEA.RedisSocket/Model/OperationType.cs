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
*文件名： OperationType
*版本号： v26.4.23.1
*唯一标识：8dfd2407-5cdd-4f72-b2d7-bc9c83f79921
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/11/05 23:13:59
*描述：OperationType接口
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
    /// 命令的参数形态，用于集群重定向时按同一形态向新节点重放命令
    /// </summary>
    public enum OperationType : byte
    {
        /// <summary>
        /// 无参数，仅命令类型
        /// </summary>
        Do = 1,
        /// <summary>
        /// 命令类型加单个 key
        /// </summary>
        DoWithKey = 2,
        /// <summary>
        /// 命令类型加 key 与值
        /// </summary>
        DoWithKeyValue = 3,
        /// <summary>
        /// 对 key 设置秒级过期时间
        /// </summary>
        DoExpire = 4,
        /// <summary>
        /// 对 key 设置过期时间戳
        /// </summary>
        DoExpireAt = 5,
        /// <summary>
        /// 写入 key 值并同时设置过期时间
        /// </summary>
        DoExpireInsert = 6,
        /// <summary>
        /// 命令类型加标识、key 与值
        /// </summary>
        DoWithID = 7,
        /// <summary>
        /// 命令类型加 key 与起止下标
        /// </summary>
        DoRang = 8,
        /// <summary>
        /// 命令类型加 key 与分值区间，用于有序集合范围查询
        /// </summary>
        DoRangByScore = 9,
        /// <summary>
        /// 订阅类命令，参数为频道列表与消息回调
        /// </summary>
        DoSub = 10,
        /// <summary>
        /// 参数字典形式的批量写入
        /// </summary>
        DoBatchWithDic = 11,
        /// <summary>
        /// 参数列表形式的批量操作
        /// </summary>
        DoBatchWithList = 12,
        /// <summary>
        /// 变长字符串参数形式的操作
        /// </summary>
        DoBatchWithParams = 13,
        /// <summary>
        /// 标识加多个 key 的批量操作
        /// </summary>
        DoBatchWithIDKeys = 14,
        /// <summary>
        /// 标识加字典的批量写入
        /// </summary>
        DoBatchWithIDDic = 15,
        /// <summary>
        /// 标识加分值字典的有序集合批量写入
        /// </summary>
        DoBatchZaddWithIDDic = 16,
        /// <summary>
        /// 游标扫描
        /// </summary>
        DoScan = 17,
        /// <summary>
        /// 指定 key 的游标扫描
        /// </summary>
        DoScanKey = 18,
        /// <summary>
        /// 集群管理命令，参数为变长对象数组
        /// </summary>
        DoCluster = 19,
        /// <summary>
        /// 集群槽位分配命令
        /// </summary>
        DoClusterSetSlot = 20
    }
}