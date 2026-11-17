/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RPCTest.Consumer.Model
*文件名： GroupInfo
*版本号： v7.0.0.1
*唯一标识：50d80ab2-699e-4851-ad1b-851564c2bb4c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/5/22 15:39:23
*描述：
*
*=====================================================================
*修改标记
*修改时间：2026/11/17 21:00:00
*修改人： yswenli
*版本号： net10.0
*描述：唯一标识改为本文件专属 GUID
*
*****************************************************************************/
using SAEA.RPC;
using SAEA.RPCTest.Provider.Model;

namespace SAEA.RPCTest.Provider
{
    /// <summary>
    /// 枚举服务示例，验证枚举参数与枚举返回值的远程调用
    /// </summary>
    [RPCService]
    public class EnumService
    {
        /// <summary>
        /// 接收枚举参数并返回固定枚举值
        /// </summary>
        /// <param name="est">服务评价等级，当前实现未使用</param>
        /// <returns>固定返回最大档位 Biggest</returns>
        public ReturnEnum GetEnum(EnumServiceType est)
        {
            return ReturnEnum.Biggest;
        }
    }
}
