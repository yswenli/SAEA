/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Model
*文件名： OperationType
*版本号： V3.3.3.4
*唯一标识：a22caf84-4c61-456e-98cc-cbb6cb2c6d6e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/11/5 20:45:02
*描述：
*
*=====================================================================
*修改标记
*创建时间：2018/11/5 20:45:02
*修改人： yswenli
*版本号： V3.3.3.4
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
        DoInOne = 2,
        DoWithKey = 3,
        DoWithKeyValue = 4,
        DoExpire = 5,
        DoExpireInsert = 6,
        DoHash = 7,
        DoRang = 8,
        DoSub = 9,
        DoBatchWithDic = 10,
        DoBatchWithParams = 11,
        DoBatchWithIDKeys = 12,
        DoBatchWithIDDic = 13,
        DoScan = 14,
        DoScanKey = 15,
        DoCluster = 16,
        DoClusterSetSlot = 17
    }
}
