/****************************************************************************
 * 
   ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
   ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
 

*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Model
*文件名： OperationType
*版本号： v26.4.23.1
*唯一标识：a22caf84-4c61-456e-98cc-cbb6cb2c6d6e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/11/5 20:45:02
*描述：
*
*=====================================================================
*修改标记
*创建时间：2018/11/5 20:45:02
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/


namespace SAEA.RedisSocket.Model
{
    /// <summary>
    /// 操作类型
    /// </summary>
    public enum OperationType : byte
    {
        Do = 1,
        DoWithKey = 2,
        DoWithKeyValue = 3,
        DoExpire = 4,
        DoExpireAt = 5,
        DoExpireInsert = 6,
        DoWithID = 7,
        DoRang = 8,
        DoRangByScore = 9,
        DoSub = 10,
        DoBatchWithDic = 11,
        DoBatchWithList = 12,
        DoBatchWithParams = 13,
        DoBatchWithIDKeys = 14,
        DoBatchWithIDDic = 15,
        DoBatchZaddWithIDDic = 16,
        DoScan = 17,
        DoScanKey = 18,
        DoCluster = 19,
        DoClusterSetSlot = 20
    }
}